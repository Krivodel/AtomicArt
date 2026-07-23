using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IGenerationAttachmentSource
{
    GenerationAttachmentMetadataDto Metadata { get; }

    ValueTask<Stream> OpenReadAsync(CancellationToken ct);
}
