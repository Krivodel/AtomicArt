using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class UnknownGenerationItemStatusDescriptorFactory : IUnknownGenerationItemStatusDescriptorFactory
{
    public IGenerationItemStatusDescriptor Create(GenerationItemStatus status)
    {
        return new UnknownGenerationItemStatusDescriptor(status);
    }
}
