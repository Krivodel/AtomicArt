using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;
using Xunit;

using AtomicArt.Domain.Generation;

namespace AtomicArt.Domain.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddDomainServices_WithServices_RegistersGenerationDomainRules()
    {
        ServiceCollection services = [];

        services.AddDomainServices();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IGenerationModelRules)
            && descriptor.ImplementationType == typeof(MetadataGenerationModelRules)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(GenerationModelRules)
            && descriptor.ImplementationType == typeof(GenerationModelRules)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
