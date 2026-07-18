using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public interface IUnknownGenerationItemStatusDescriptorFactory
{
    IGenerationItemStatusDescriptor Create(GenerationItemStatus status);
}
