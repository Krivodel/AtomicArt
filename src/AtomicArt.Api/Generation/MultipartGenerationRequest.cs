using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Api.Generation;

public sealed class MultipartGenerationRequest : IAsyncDisposable
{
    public GenerationRequestMetadataDto Metadata { get; }
    public IReadOnlyList<IGenerationAttachmentSource> Attachments { get; }

    private readonly IReadOnlyList<TemporaryGenerationAttachmentSource> _sources;

    internal MultipartGenerationRequest(
        GenerationRequestMetadataDto metadata,
        IReadOnlyList<TemporaryGenerationAttachmentSource> sources)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        Attachments = sources.Cast<IGenerationAttachmentSource>().ToList();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (TemporaryGenerationAttachmentSource source in _sources)
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }
    }
}
