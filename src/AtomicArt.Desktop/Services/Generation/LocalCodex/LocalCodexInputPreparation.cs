using System.Text.Json;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

internal sealed class LocalCodexInputPreparation : IDisposable
{
    internal IReadOnlyList<object> Inputs => _inputs;

    private readonly IGenerationReferenceStore _references;
    private readonly IGenerationImageFormatRegistry _formats;
    private readonly ILocalizationTextProvider _text;
    private readonly Guid _leaseId = Guid.NewGuid();
    private readonly List<object> _inputs = [];
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    internal LocalCodexInputPreparation(
        IGenerationReferenceStore references,
        IGenerationImageFormatRegistry formats,
        ILocalizationTextProvider text)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _formats = formats ?? throw new ArgumentNullException(nameof(formats));
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public void Dispose()
    {
        _references.Release(_leaseId);
    }

    internal async Task PrepareAsync(
        ImageGenerationRequestDto request, GenerationChatHistory? history, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (history is not null)
        {
            await _references.RetainReadPathsAsync(_leaseId, history.Messages.SelectMany(message => message.Images), ct)
                .ConfigureAwait(false);

            foreach (GenerationChatMessage message in history.Messages)
            {
                string role = message.IsAssistant ? "assistant" : "user";
                _inputs.Add(new { type = "text", text = JsonSerializer.Serialize(new { role, text = message.Text }) });

                foreach (GenerationReference reference in message.Images)
                {
                    await AddImageAsync(reference, ct).ConfigureAwait(false);
                }
            }
        }

        string prompt = LocalCodexGenerationClient.FormatPrompt(request, _text);
        string text = _inputs.Count == 0 ? prompt : JsonSerializer.Serialize(new { role = "user", text = prompt });
        _inputs.Add(new { type = "text", text });

        foreach (AttachedImageDto attachment in request.AttachedImages)
        {
            if (!_formats.TryGetByContentType(attachment.ContentType, out IGenerationImageFormat? format)
                || format is null || !format.MatchesSignature(attachment.Content))
            {
                throw new GenerationAttemptException(
                    "Invalid Codex reference image.", GenerationProtocolErrorCodes.InvalidParameters, false);
            }
        }

        IReadOnlyList<GenerationReference> references = await _references.SaveAsync(_leaseId, request.AttachedImages, ct)
            .ConfigureAwait(false);
        await _references.RetainReadPathsAsync(_leaseId, references, ct).ConfigureAwait(false);

        foreach (GenerationReference reference in references)
        {
            await AddImageAsync(reference, ct).ConfigureAwait(false);
        }
    }

    private async Task AddImageAsync(GenerationReference reference, CancellationToken ct)
    {
        if (!_files.TryGetValue(reference.Hash, out string? path))
        {
            path = await _references.GetExistingFilePathAsync(reference, ct).ConfigureAwait(false)
                ?? throw new GenerationAttemptException(
                    "A Codex conversation image is unavailable.", GenerationProtocolErrorCodes.InvalidParameters, false);
            _files.Add(reference.Hash, path);
        }

        _inputs.Add(new { type = "localImage", path });
    }
}
