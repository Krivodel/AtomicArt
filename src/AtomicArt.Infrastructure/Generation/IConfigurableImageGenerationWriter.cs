using AtomicArt.Application.Features.Generation.Interfaces;

namespace AtomicArt.Infrastructure.Generation;

internal interface IConfigurableImageGenerationWriter : IImageGenerationWriter
{
    string Name { get; }
}
