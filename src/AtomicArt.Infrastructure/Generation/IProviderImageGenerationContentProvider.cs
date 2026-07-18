using AtomicArt.Application.Features.Generation.Interfaces;

namespace AtomicArt.Infrastructure.Generation;

internal interface IProviderImageGenerationContentProvider : IImageGenerationContentProvider
{
    string Provider { get; }
}
