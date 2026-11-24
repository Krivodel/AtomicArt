using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Options;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

public sealed class LocalCodexGenerationClient(
    LocalCodexSettings settings,
    LocalCodexAppServer appServer,
    IGenerationStreamingResultStore resultStore,
    IGenerationImageFormatRegistry formatRegistry,
    TrustedFileStreamFactory fileStreamFactory,
    IAttachedImageCodec imageCodec,
    ILocalizationTextProvider textProvider,
    IOptions<GenerationClientOptions> options,
    IGenerationReferenceStore referenceStore)
{
    private const int MaximumImageBytes = 32 * 1024 * 1024;
    private const int MaximumBase64Length = (MaximumImageBytes + 2) / 3 * 4;

    private static readonly string UntrustedImagePath = TrustedPathGuard.CreateFailureMessage(
        "Codex result path", "the current Codex thread's results directory");

    private readonly IGenerationReferenceStore _referenceStore =
        referenceStore ?? throw new ArgumentNullException(nameof(referenceStore));

    public async Task<(GenerationBatchDto Batch, GenerationChatResult ChatResult)> CreateGenerationAsync(
        ImageGenerationRequestDto request, Guid generationId, LocalCodexTurnOptions? turnOptions,
        GenerationChatHistory? history, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        LocalCodexOptions configuration = settings.Current;

        if (!configuration.Enabled)
        {
            throw new GenerationAttemptException(
                "Local Codex is disabled.", GenerationClientFailureCodes.CodexUnavailable, false);
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.ProviderResponseTimeoutSeconds));
        DirectoryInfo workspace = Directory.CreateTempSubdirectory("AtomicArt-Codex-");
        DateTime startedAtUtc = DateTime.UtcNow;
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            IReadOnlyList<object> inputs = await PrepareInputsAsync(request, history, workspace.FullName, timeout.Token)
                .ConfigureAwait(false);
            (byte[] bytes, string? modelId, string text) = await appServer.RunAsync(
                configuration,
                workspace.FullName,
                async (protocol, home) =>
                {
                    (string threadId, string? effectiveModelId, JsonElement image, string answer) = await protocol.GenerateAsync(
                        workspace.FullName, inputs, turnOptions, timeout.Token, history)
                        .ConfigureAwait(false);
                    byte[] imageBytes = await ReadImageAsync(home, threadId, image, timeout.Token).ConfigureAwait(false);

                    return (imageBytes, effectiveModelId, answer);
                },
                timeout.Token).ConfigureAwait(false);

            AttachedImageCodecInfo info = imageCodec.ReadInfo(bytes) ?? throw InvalidImage();
            await using GenerationTemporaryResult result = resultStore.CreateTemporaryResult();
            await result.Stream.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
            Guid storageLeaseId = Guid.NewGuid();
            GenerationReference imageReference;

            try
            {
                AttachedImageDto attachment = new("image.png", GenerationImageContentTypes.Png, bytes);
                IReadOnlyList<GenerationReference> references = await _referenceStore
                    .SaveAsync(storageLeaseId, new AttachedImageDto[] { attachment }, timeout.Token).ConfigureAwait(false);
                imageReference = references.Single();
                await result.CommitAsync(generationId, generationId, GenerationImageContentTypes.Png, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                _referenceStore.Release(storageLeaseId);
                throw;
            }

            GenerationItemDto item = new(
                generationId,
                request.ModelId,
                LocalCodexImageModel.FormatDisplayName(turnOptions?.ModelId == modelId
                    ? turnOptions?.ModelDisplayName
                    : modelId),
                request.Prompt,
                request.AspectRatio,
                FormattableString.Invariant($"{info.Width}x{info.Height}"),
                startedAtUtc,
                GenerationItemStatus.Generated,
                result.FinalPath,
                CompletedAtUtc: DateTime.UtcNow,
                GenerationDuration: stopwatch.Elapsed);

            return (new GenerationBatchDto(generationId, new GenerationItemDto[] { item }), new GenerationChatResult(text, imageReference, storageLeaseId));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new GenerationAttemptException(
                "Codex image generation timed out.", GenerationProviderFailureErrorCodes.Timeout, false);
        }
        finally
        {
            appServer.DeleteWorkspace(workspace);
        }
    }

    internal static string FormatPrompt(ImageGenerationRequestDto request, ILocalizationTextProvider textProvider)
    {
        if (GenerationAspectRatios.IsAuto(request.AspectRatio))
        {
            return request.Prompt;
        }

        string aspectRatioLabel = textProvider.Get(GalleryLocalizationKeys.Metadata.AspectRatio);

        return $"{request.Prompt}\n\n{aspectRatioLabel}: {request.AspectRatio}";
    }

    private static void ValidateRequest(ImageGenerationRequestDto request)
    {
        ImageModelOption model = LocalCodexImageModel.Option;

        if (!LocalCodexImageModel.Matches(request.ModelId)
            || string.IsNullOrWhiteSpace(request.Prompt)
            || !model.AspectRatios.Contains(request.AspectRatio, StringComparer.Ordinal)
            || request.GenerationCount != 1
            || request.AttachedImages.Count > model.MaxAttachedImages
            || request.AttachedImages.Any(image => image.Content.Length > model.MaxAttachedImageBytes
            || !model.SupportedAttachmentContentTypes.Contains(image.ContentType, StringComparer.Ordinal))
            || request.AttachedImages.Sum(image => (long)image.Content.Length) > model.MaxTotalAttachedImageBytes)
        {
            throw new GenerationAttemptException(
                "Invalid local Codex request.", GenerationProtocolErrorCodes.InvalidParameters, false);
        }
    }

    private static GenerationAttemptException InvalidImage()
    {
        return new GenerationAttemptException(
            "Codex did not return a valid image.", GenerationProviderFailureErrorCodes.InvalidResponse, false);
    }

    private async Task<IReadOnlyList<object>> PrepareInputsAsync(
        ImageGenerationRequestDto request, GenerationChatHistory? history, string workspace, CancellationToken ct)
    {
        List<object> inputs = [];
        Dictionary<string, string> historyFiles = new(StringComparer.Ordinal);

        if (history is not null)
        {
            foreach (GenerationChatMessage message in history.Messages)
            {
                string role = message.IsAssistant ? "assistant" : "user";
                inputs.Add(new { type = "text", text = JsonSerializer.Serialize(new { role, text = message.Text }) });

                foreach (GenerationReference reference in message.Images)
                {
                    if (!historyFiles.TryGetValue(reference.Hash, out string? path))
                    {
                        AttachedImageDto image = await _referenceStore.LoadAsync(reference, ct).ConfigureAwait(false)
                            ?? throw new GenerationAttemptException(
                                "A Codex conversation image is unavailable.", GenerationProtocolErrorCodes.InvalidParameters, false);
                        path = await WriteAttachmentAsync(image, workspace, inputs.Count, ct).ConfigureAwait(false);
                        historyFiles.Add(reference.Hash, path);
                    }

                    inputs.Add(new { type = "localImage", path });
                }
            }
        }

        string prompt = LocalCodexGenerationClient.FormatPrompt(request, textProvider);
        string text = inputs.Count == 0 ? prompt : JsonSerializer.Serialize(new { role = "user", text = prompt });
        inputs.Add(new { type = "text", text });

        foreach (AttachedImageDto attachment in request.AttachedImages)
        {
            string path = await WriteAttachmentAsync(attachment, workspace, inputs.Count, ct).ConfigureAwait(false);
            inputs.Add(new { type = "localImage", path });
        }

        return inputs;
    }

    private async Task<string> WriteAttachmentAsync(AttachedImageDto attachment, string workspace, int index, CancellationToken ct)
    {
        if (!formatRegistry.TryGetByContentType(attachment.ContentType, out IGenerationImageFormat? format)
            || format is null || !format.MatchesSignature(attachment.Content))
        {
            throw new GenerationAttemptException(
                "Invalid Codex reference image.", GenerationProtocolErrorCodes.InvalidParameters, false);
        }

        string path = Path.Combine(workspace, $"attachment-{index}{format.Extension}");
        await File.WriteAllBytesAsync(path, attachment.Content, ct).ConfigureAwait(false);

        return path;
    }

    private async Task<byte[]> ReadImageAsync(string home, string threadId, JsonElement image, CancellationToken ct)
    {
        if (image.TryGetProperty("result", out JsonElement inline)
            && inline.ValueKind == JsonValueKind.String
            && inline.GetString() is { Length: > 0 and <= MaximumBase64Length } encoded)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);

                return bytes.Length <= MaximumImageBytes ? bytes : throw InvalidImage();
            }
            catch (FormatException)
            {
                throw InvalidImage();
            }
        }

        if (!Guid.TryParse(threadId, out _)
            || !image.TryGetProperty("savedPath", out JsonElement savedPath)
            || savedPath.ValueKind != JsonValueKind.String
            || savedPath.GetString() is not { Length: > 0 } path
            || !Path.IsPathFullyQualified(path))
        {
            throw InvalidImage();
        }

        string threadDirectory = Path.Combine(home, "generated_images", threadId);

        if (!fileStreamFactory.TryOpenExistingFileForRead(
            path, new[] { threadDirectory }, threadDirectory, UntrustedImagePath, out FileStream? stream, out _)
            || stream is null)
        {
            throw InvalidImage();
        }

        await using (stream.ConfigureAwait(false))
        {
            long length = stream.Length;

            if (length is <= 0 or > MaximumImageBytes)
            {
                throw InvalidImage();
            }

            byte[] bytes = new byte[(int)length];
            await stream.ReadExactlyAsync(bytes, ct).ConfigureAwait(false);

            return bytes;
        }
    }
}
