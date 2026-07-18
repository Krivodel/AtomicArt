using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GenerationItemStatusDescriptorRegistry : IGenerationItemStatusDescriptorRegistry
{
    private readonly IReadOnlyDictionary<GenerationItemStatus, IGenerationItemStatusDescriptor> _descriptors;
    private readonly IUnknownGenerationItemStatusDescriptorFactory _unknownDescriptorFactory;

    public GenerationItemStatusDescriptorRegistry(
        IEnumerable<IGenerationItemStatusDescriptor> descriptors,
        IUnknownGenerationItemStatusDescriptorFactory unknownDescriptorFactory)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        _descriptors = descriptors.ToDictionary(descriptor => descriptor.Status);
        _unknownDescriptorFactory = unknownDescriptorFactory
            ?? throw new ArgumentNullException(nameof(unknownDescriptorFactory));
    }

    public IGenerationItemStatusDescriptor Get(GenerationItemStatus status)
    {
        if (_descriptors.TryGetValue(status, out IGenerationItemStatusDescriptor? descriptor))
        {
            return descriptor;
        }

        return _unknownDescriptorFactory.Create(status);
    }
}
