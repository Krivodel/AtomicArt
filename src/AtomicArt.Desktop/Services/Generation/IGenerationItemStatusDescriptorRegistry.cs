using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationItemStatusDescriptorRegistry
{
    IGenerationItemStatusDescriptor Get(GenerationItemStatus status);
}
