using AtomicArt.Application.Features.Generation.Interfaces;

namespace AtomicArt.Infrastructure.Generation;

internal interface IProviderStreamingImageGenerationProvider
    : IStreamingImageGenerationProvider
{
    string Provider { get; }
}
