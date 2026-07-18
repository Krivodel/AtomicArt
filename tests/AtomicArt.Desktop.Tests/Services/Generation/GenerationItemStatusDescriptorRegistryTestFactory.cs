using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal static class GenerationItemStatusDescriptorRegistryTestFactory
{
    public static IGenerationItemStatusDescriptorRegistry Create()
    {
        return new GenerationItemStatusDescriptorRegistry(
            CreateRegisteredDescriptors(),
            new UnknownGenerationItemStatusDescriptorFactory());
    }

    internal static IReadOnlyList<IGenerationItemStatusDescriptor> CreateRegisteredDescriptors()
    {
        Type markerType = typeof(IRegisteredGenerationItemStatusDescriptor);
        IReadOnlyList<Type> descriptorTypes = typeof(IGenerationItemStatusDescriptor)
            .Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && markerType.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        return descriptorTypes
            .Select(CreateDescriptor)
            .ToList();
    }

    private static IGenerationItemStatusDescriptor CreateDescriptor(Type descriptorType)
    {
        object descriptor = Activator.CreateInstance(descriptorType)
            ?? throw new InvalidOperationException(
                $"Generation item status descriptor '{descriptorType.FullName}' could not be created.");

        return (IGenerationItemStatusDescriptor)descriptor;
    }
}
