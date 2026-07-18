using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddDesktopServices_WithGenerationStatusRegistry_ResolvesRegistry()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IGenerationItemStatusDescriptorRegistry registry =
            serviceProvider.GetRequiredService<IGenerationItemStatusDescriptorRegistry>();
        IGenerationItemStatusDescriptor descriptor = registry.Get(GenerationItemStatus.Generated);

        registry.Should().NotBeNull();
        descriptor.Status.Should().Be(GenerationItemStatus.Generated);
    }

    [Fact]
    public void AddDesktopServices_WithGenerationStatusDescriptors_RegistersOnlyFixedStatusDescriptors()
    {
        ServiceCollection services = CreateServices();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        GenerationItemStatus[] expectedStatuses = Enum.GetValues<GenerationItemStatus>();

        IReadOnlyList<IGenerationItemStatusDescriptor> descriptors = serviceProvider
            .GetServices<IGenerationItemStatusDescriptor>()
            .ToList();

        services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(UnknownGenerationItemStatusDescriptor)
            || descriptor.ImplementationType == typeof(UnknownGenerationItemStatusDescriptor));
        descriptors.Should().OnlyContain(descriptor => descriptor is IRegisteredGenerationItemStatusDescriptor);
        descriptors.Should().NotContain(descriptor => descriptor is UnknownGenerationItemStatusDescriptor);
        descriptors
            .Select(descriptor => descriptor.Status)
            .Should()
            .BeEquivalentTo(expectedStatuses);
    }

    [Fact]
    public void AddDesktopServices_WithUnknownStatusDescriptorFactory_ResolvesFactory()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IUnknownGenerationItemStatusDescriptorFactory factory =
            serviceProvider.GetRequiredService<IUnknownGenerationItemStatusDescriptorFactory>();
        IGenerationItemStatusDescriptor descriptor =
            factory.Create((GenerationItemStatus)999);

        factory.Should().BeOfType<UnknownGenerationItemStatusDescriptorFactory>();
        descriptor.Status.Should().Be((GenerationItemStatus)999);
        descriptor.VisualState.Should().Be(GenerationItemVisualState.Unknown);
    }

    [Fact]
    public void AddDesktopServices_WithPricePreviewEstimator_RegistersEstimatorWithoutQuoteApiClient()
    {
        ServiceCollection services = CreateServices();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GenerationPricePreviewEstimator estimator =
            serviceProvider.GetRequiredService<GenerationPricePreviewEstimator>();
        string[] removedServiceTypeNames =
        [
            "IGenerationQuoteApiClient",
            "GenerationQuoteApiClient",
            "INanoBanana2QuoteRefreshController",
            "NanoBanana2QuoteRefreshController",
            "NanoBanana2QuoteRefresher"
        ];

        estimator.Should().NotBeNull();
        bool containsRemovedQuoteService = services.Any(descriptor =>
            ContainsTypeName(removedServiceTypeNames, descriptor.ServiceType)
            || ContainsTypeName(removedServiceTypeNames, descriptor.ImplementationType));

        containsRemovedQuoteService.Should().BeFalse();
    }

    [Fact]
    public void AddDesktopServices_WithGenerationConcurrencyLimiter_RegistersSingletonLimiter()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IGenerationConcurrencyLimiter firstLimiter =
            serviceProvider.GetRequiredService<IGenerationConcurrencyLimiter>();
        IGenerationConcurrencyLimiter secondLimiter =
            serviceProvider.GetRequiredService<IGenerationConcurrencyLimiter>();

        firstLimiter.Should().BeSameAs(secondLimiter);
    }

    [Fact]
    public void AddDesktopServices_WithAttachmentPreparationLimiter_RegistersSingletonLimiter()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        AttachedImagePreparationConcurrencyLimiter firstLimiter =
            serviceProvider.GetRequiredService<AttachedImagePreparationConcurrencyLimiter>();
        AttachedImagePreparationConcurrencyLimiter secondLimiter =
            serviceProvider.GetRequiredService<AttachedImagePreparationConcurrencyLimiter>();

        firstLimiter.Should().BeSameAs(secondLimiter);
    }

    [Fact]
    public void AddDesktopServices_WithGenerationRunDispatcher_RegistersTransientDispatcher()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IGenerationRunDispatcher firstDispatcher =
            serviceProvider.GetRequiredService<IGenerationRunDispatcher>();
        IGenerationRunDispatcher secondDispatcher =
            serviceProvider.GetRequiredService<IGenerationRunDispatcher>();

        firstDispatcher.Should().NotBeSameAs(secondDispatcher);
    }

    [Fact]
    public void AddDesktopServices_WithViewTemplates_RegistersMappingsInPriorityOrder()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        Type[] expectedViewModelTypes =
        [
            typeof(GalleryViewModel),
            typeof(IModelPanelViewModel),
            typeof(SettingsViewModel),
            typeof(ApiBaseAddressSettingViewModel),
            typeof(SecretSettingViewModel),
            typeof(ScaleSettingViewModel),
            typeof(GpuResourceCacheSettingViewModel),
            typeof(GenerationMetadataViewModel)
        ];

        IReadOnlyList<Type> viewModelTypes = serviceProvider
            .GetServices<ViewTemplateRegistration>()
            .Select(registration => registration.ViewModelType)
            .ToList();

        viewModelTypes.Should().Equal(expectedViewModelTypes);
    }

    [Fact]
    public void AddDesktopServices_WithGoogleApiKeySetting_RegistersNewSettingWithoutOldSetting()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IReadOnlyList<ISettingsDefinition> settings = serviceProvider
            .GetServices<ISettingsDefinition>()
            .ToList();

        settings.Should().ContainSingle(setting => setting is GoogleApiKeySettingDefinition);
        settings.Select(setting => setting.GetType().Name)
            .Should()
            .NotContain("NanoBanana2ApiKeySettingDefinition");
    }

    [Fact]
    public void AddDesktopServices_WithApiBaseAddressSetting_RegistersSettingFirst()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IReadOnlyList<ISettingsDefinition> settings = serviceProvider
            .GetRequiredService<ISettingsDefinitionCatalog>()
            .GetSettings();

        settings.First().Should().BeOfType<ApiBaseAddressSettingDefinition>();
    }

    [Fact]
    public void AddDesktopServices_WithApiEndpointService_RegistersSingleton()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IApiEndpointService firstService = serviceProvider.GetRequiredService<IApiEndpointService>();
        IApiEndpointService secondService = serviceProvider.GetRequiredService<IApiEndpointService>();

        firstService.Should().BeSameAs(secondService);
        firstService.BaseAddress.ToString().Should().Be(TestApiConfiguration.BaseAddress);
    }

    [Fact]
    public void AddDesktopServices_WithUiScaleOptions_RegistersExpectedScaleOptions()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        ISettingsDefinitionCatalog catalog = serviceProvider.GetRequiredService<ISettingsDefinitionCatalog>();
        IReadOnlyList<UiScaleOption> scaleOptions = catalog.GetScaleOptions();

        scaleOptions.Should().Equal(
            new UiScaleOption("60%", 0.6),
            new UiScaleOption("80%", 0.8),
            new UiScaleOption("100%", 1.0),
            new UiScaleOption("110%", 1.1),
            new UiScaleOption("125%", 1.25),
            new UiScaleOption("150%", 1.5));
    }

    [Fact]
    public void DesktopAssembly_WithModelCatalogApiSource_DoesNotContainLocalModelOptionProvider()
    {
        IReadOnlyList<string> typeNames = typeof(DependencyInjection)
            .Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToList();

        typeNames.Should().NotContain("NanoBanana2ImageModelOptionProvider");
        typeNames.Should().NotContain("IImageModelOptionProvider");
    }

    [Fact]
    public void DesktopAppSettings_WithDefaultApiBaseAddress_UsesLocalApiPort()
    {
        string path = TestRepositoryFiles.Find(Path.Combine("src", "AtomicArt.Desktop", "appsettings.json"));
        string json = File.ReadAllText(path);
        using JsonDocument document = JsonDocument.Parse(json);
        string? baseAddress = document.RootElement
            .GetProperty("Api")
            .GetProperty("BaseAddress")
            .GetString();

        baseAddress.Should().Be("http://localhost:5000/");
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return CreateServices().BuildServiceProvider();
    }

    private static bool ContainsTypeName(IReadOnlyList<string> typeNames, Type? type)
    {
        if (type is null)
        {
            return false;
        }

        return typeNames.Contains(type.Name, StringComparer.Ordinal);
    }

    private static ServiceCollection CreateServices()
    {
        ServiceCollection services = new();
        services.AddSingleton(TestApiConfiguration.Create());
        services.AddDesktopServices();

        return services;
    }
}
