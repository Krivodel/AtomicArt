using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Domain;

namespace AtomicArt.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_WithDomainServices_RegistersGenerationApplicationServices()
    {
        ServiceCollection services = [];

        services.AddDomainServices();
        services.AddApplicationServices();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IImageModelRegistry)
            && descriptor.ImplementationType == typeof(ImageModelRegistry)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAttachedImageFormatRegistry)
            && descriptor.ImplementationType == typeof(AttachedImageFormatRegistry)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAttachedImageFormat)
            && descriptor.ImplementationInstance is AttachedImageFormat
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IImageModelDefinitionFactory)
            && descriptor.ImplementationType == typeof(MetadataImageModelDefinitionFactory)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType.Namespace == "AtomicArt.Domain.Generation");
    }

    [Fact]
    public void AddApplicationServices_WithServices_DoesNotRegisterGenerationDomainRules()
    {
        ServiceCollection services = [];

        services.AddApplicationServices();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IImageModelRegistry)
            && descriptor.ImplementationType == typeof(ImageModelRegistry)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAttachedImageFormatRegistry)
            && descriptor.ImplementationType == typeof(AttachedImageFormatRegistry)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IImageModelDefinitionFactory)
            && descriptor.ImplementationType == typeof(MetadataImageModelDefinitionFactory)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().NotContain(descriptor =>
            descriptor.ServiceType.Namespace == "AtomicArt.Domain.Generation");
    }

    [Fact]
    public void ApplicationAssembly_WithPhaseFour_DoesNotContainGenerationQuoteQueryTypes()
    {
        IReadOnlyList<string> typeNames = typeof(DependencyInjection)
            .Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToList();

        typeNames.Should().NotContain("GetGenerationQuoteQuery");
        typeNames.Should().NotContain("GetGenerationQuoteHandler");
        typeNames.Should().NotContain("GetGenerationQuoteValidator");
    }
}
