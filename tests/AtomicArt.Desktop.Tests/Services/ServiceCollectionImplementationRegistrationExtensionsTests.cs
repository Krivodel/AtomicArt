using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ServiceCollectionImplementationRegistrationExtensionsTests
{
    [Fact]
    public void AddSharedSingletonImplementation_WithConcreteType_ResolvesSameInstance()
    {
        ServiceCollection services = new();
        services.AddSharedSingletonImplementation(typeof(Stream), typeof(MemoryStream));
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        MemoryStream concreteInstance = serviceProvider.GetRequiredService<MemoryStream>();
        Stream serviceInstance = serviceProvider.GetRequiredService<Stream>();

        serviceInstance.Should().BeSameAs(concreteInstance);
    }

    [Fact]
    public void AddSharedSingletonAliases_WithConcreteType_ResolvesSameInstanceForEveryAlias()
    {
        ServiceCollection services = new();
        services.AddSharedSingletonAliases<MemoryStream>(typeof(Stream), typeof(IDisposable));
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        MemoryStream concreteInstance = serviceProvider.GetRequiredService<MemoryStream>();
        Stream streamInstance = serviceProvider.GetRequiredService<Stream>();
        IDisposable disposableInstance = serviceProvider.GetRequiredService<IDisposable>();

        streamInstance.Should().BeSameAs(concreteInstance);
        disposableInstance.Should().BeSameAs(concreteInstance);
    }

    [Fact]
    public void AddSingletonImplementations_WithConcreteTypes_ResolvesSingletonCollection()
    {
        ServiceCollection services = new();
        Type[] implementationTypes =
        [
            typeof(MemoryStream),
            typeof(CancellationTokenSource)
        ];
        services.AddSingletonImplementations(typeof(IDisposable), implementationTypes);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        IReadOnlyList<IDisposable> firstInstances = serviceProvider
            .GetServices<IDisposable>()
            .ToList();
        IReadOnlyList<IDisposable> secondInstances = serviceProvider
            .GetServices<IDisposable>()
            .ToList();

        firstInstances.Select(instance => instance.GetType()).Should().Equal(implementationTypes);
        secondInstances.Should().Equal(firstInstances);
    }
}
