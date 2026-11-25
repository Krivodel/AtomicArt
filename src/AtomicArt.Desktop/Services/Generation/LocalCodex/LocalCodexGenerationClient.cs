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
            using LocalCodexInputPreparation input = new(_referenceStore, formatRegistry, textProvider);
            await input.PrepareAsync(request, history, timeout.Token).ConfigureAwait(false);
            (LocalCodexTurnResult turn, byte[]? bytes, string resultHome) = await appServer.RunAsync(
                configuration,
                workspace.FullName,
                async (protocol, home) =>
                {
                    LocalCodexTurnResult reply = await protocol.GenerateAsync(
                        workspace.FullName, input.Inputs, turnOptions, timeout.Token)
                        .ConfigureAwait(false);
                    byte[]? imageBytes = reply.Image is { } image
                        ? await ReadImageAsync(home, reply.ThreadId, image, timeout.Token).ConfigureAwait(false) : null;

                    return (reply, imageBytes, home);
                },
                timeout.Token).ConfigureAwait(false);
            input.Dispose();

            string? imagePath = null;
            string resolution = request.Resolution;
            GenerationChatResult result;

            if (turn.Image is { } resultImage && bytes is not null)
            {
                AttachedImageCodecInfo info = imageCodec.ReadInfo(bytes) ?? throw InvalidImage();
                resolution = FormattableString.Invariant($"{info.Width}x{info.Height}");
                (result, imagePath) = await StoreImageAsync(bytes, turn, timeout.Token).ConfigureAwait(false);
                appServer.DeleteGeneratedImage(resultHome, turn.ThreadId, resultImage);
            }
            else
            {
                result = new GenerationChatResult(turn.Text, null) { IsTextReply = true };
            }

            GenerationItemDto item = new(
                generationId,
                request.ModelId,
                LocalCodexImageModel.FormatDisplayName(turnOptions?.ModelId == turn.ModelId
                    ? turnOptions?.ModelDisplayName
                    : turn.ModelId),
                request.Prompt,
                request.AspectRatio,
                resolution,
                startedAtUtc,
                GenerationItemStatus.Generated,
                imagePath,
                CompletedAtUtc: DateTime.UtcNow,
                GenerationDuration: stopwatch.Elapsed);

            return (new GenerationBatchDto(generationId, new GenerationItemDto[] { item }), result);
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

    private async Task<(GenerationChatResult Result, string Path)> StoreImageAsync(
        byte[] bytes, LocalCodexTurnResult turn, CancellationToken ct)
    {
        Guid storageLeaseId = Guid.NewGuid();

        try
        {
            AttachedImageDto attachment = new("image.png", GenerationImageContentTypes.Png, bytes);
            IReadOnlyList<GenerationReference> references = await _referenceStore
                .SaveAsync(storageLeaseId, new AttachedImageDto[] { attachment }, ct).ConfigureAwait(false);
            await _referenceStore.PromoteAsync(storageLeaseId, references, ct).ConfigureAwait(false);
            GenerationReference reference = references.Single();
            string path = await _referenceStore.GetExistingFilePathAsync(reference, ct).ConfigureAwait(false)
                ?? throw InvalidImage();
            return (new GenerationChatResult(turn.Text, reference, storageLeaseId)
            {
                ImageGeneration = turn.GetImageCall(reference.Hash)
            }, path);
        }
        catch (Exception)
        {
            _referenceStore.Release(storageLeaseId);
            throw;
        }
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

        string threadDirectory = LocalCodexAppServer.GetGeneratedImageDirectory(home, threadId);

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
