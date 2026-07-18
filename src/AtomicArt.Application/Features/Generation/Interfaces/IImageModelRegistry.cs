using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IImageModelRegistry
{
    IReadOnlyList<ImageModelOption> GetModels();

    IImageModelDefinition? GetById(string modelId);
}
