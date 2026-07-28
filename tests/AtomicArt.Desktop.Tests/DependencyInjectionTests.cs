using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Services.Windowing;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Desktop.Views;
using AtomicArt.Tests.Common;
using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddDesktopServices_WithWindowPlacement_RegistersSharedServicesAndStateSection()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        WindowStateService windowStateService =
            serviceProvider.GetRequiredService<WindowStateService>();
        IWindowAttachmentService attachmentService =
            serviceProvider.GetRequiredService<IWindowAttachmentService>();
        IStateSectionRegistry stateSectionRegistry =
            serviceProvider.GetRequiredService<IStateSectionRegistry>();

        attachmentService.Should().BeSameAs(windowStateService);
        stateSectionRegistry
            .GetRequired<WindowPlacementState>()
            .Should()
            .BeOfType<WindowPlacementStateSection>();
    }

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
    public void AddDesktopServices_WithVirtualFileDrop_RegistersExpectedLifetimes()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IVirtualFileDropAttachmentService firstAttachmentService =
            serviceProvider.GetRequiredService<IVirtualFileDropAttachmentService>();
        IVirtualFileDropAttachmentService secondAttachmentService =
            serviceProvider.GetRequiredService<IVirtualFileDropAttachmentService>();
        IDragDropImageService firstImageService =
            serviceProvider.GetRequiredService<IDragDropImageService>();
        IDragDropImageService secondImageService =
            serviceProvider.GetRequiredService<IDragDropImageService>();

        firstAttachmentService.Should().BeSameAs(secondAttachmentService);
        firstImageService.Should().NotBeSameAs(secondImageService);
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
    public void AddDesktopServices_WithGalleryPreviewPipeline_RegistersSceneScopedServices()
    {
        ServiceCollection services = CreateServices();

        ServiceDescriptor previewLoader = services.Single(descriptor =>
            descriptor.ServiceType == typeof(IGalleryPreviewBitmapLoader));
        ServiceDescriptor previewProvider = services.Single(descriptor =>
            descriptor.ServiceType == typeof(IGalleryPreviewBitmapProvider));
        ServiceDescriptor previewSourceScheduler = services.Single(descriptor =>
            descriptor.ServiceType == typeof(GalleryPreviewSourceScheduler));
        ServiceDescriptor cardControlFactory = services.Single(descriptor =>
            descriptor.ServiceType == typeof(IGalleryCardControlFactory));

        previewLoader.Lifetime.Should().Be(ServiceLifetime.Scoped);
        previewProvider.Lifetime.Should().Be(ServiceLifetime.Scoped);
        previewSourceScheduler.Lifetime.Should().Be(ServiceLifetime.Scoped);
        cardControlFactory.Lifetime.Should().Be(ServiceLifetime.Scoped);
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
            typeof(DataRootSettingViewModel),
            typeof(ApiBaseAddressSettingViewModel),
            typeof(SecretSettingViewModel),
            typeof(NumericSettingViewModel),
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
    public void AddDesktopServices_WithPromptTextSizeSetting_RegistersRuntimeAndEditor()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        PromptTextSizeSettingDefinition definition = serviceProvider
            .GetRequiredService<ISettingsDefinitionCatalog>()
            .GetRequired<PromptTextSizeSettingDefinition>();
        IPromptTextSizeService textSizeService = serviceProvider
            .GetRequiredService<IPromptTextSizeService>();
        IReadOnlyList<ISettingItemViewModel> settingItems = serviceProvider
            .GetRequiredService<ISettingsItemViewModelProvider>()
            .CreateSettings();

        textSizeService.CurrentTextSize.Should().Be(definition.DefaultValue);
        settingItems
            .OfType<NumericSettingViewModel>()
            .Should()
            .ContainSingle(setting => string.Equals(
                setting.Key,
                PromptTextSizeSettingDefinition.KeyValue,
                StringComparison.Ordinal));
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
    public void AddDesktopServices_WithDataRootMigration_ResolvesSharedServices()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IAtomicArtDataRootMigrationService migrationService =
            serviceProvider.GetRequiredService<IAtomicArtDataRootMigrationService>();
        IAtomicArtDataPathProvider pathProvider =
            serviceProvider.GetRequiredService<IAtomicArtDataPathProvider>();
        IAtomicArtDataPathSwitcher pathSwitcher =
            serviceProvider.GetRequiredService<IAtomicArtDataPathSwitcher>();

        migrationService.Should().NotBeNull();
        pathSwitcher.Should().BeSameAs(pathProvider);
    }

    [Fact]
    public void AddDesktopServices_WithImageGenerationHttpClient_UsesGenerationAttemptTimeout()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        IHttpClientFactory httpClientFactory =
            serviceProvider.GetRequiredService<IHttpClientFactory>();
        using HttpClient httpClient = httpClientFactory.CreateClient(
            nameof(IImageGenerationApiClient));

        httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(
            TestApiConfiguration
                .CreateGenerationOptions()
                .ProviderResponseTimeoutSeconds));
    }

    [Fact]
    public void AddDesktopServices_WithModelCatalogHttpClient_UsesConfiguredTimeout()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        IHttpClientFactory httpClientFactory =
            serviceProvider.GetRequiredService<IHttpClientFactory>();
        using HttpClient httpClient = httpClientFactory.CreateClient(
            nameof(IGenerationModelCatalogApiClient));

        httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(
            TestApiConfiguration
                .CreateApiClientOptions()
                .ModelCatalogTimeoutSeconds));
    }

    [Fact]
    public void AddDesktopServices_WithPicaViewer_ResolvesConstructorRegisteredServices()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        IClipboardImageWriter firstClipboardWriter =
            serviceProvider.GetRequiredService<IClipboardImageWriter>();
        IClipboardImageWriter secondClipboardWriter =
            serviceProvider.GetRequiredService<IClipboardImageWriter>();
        IImageViewerWindowFactory firstWindowFactory =
            serviceProvider.GetRequiredService<IImageViewerWindowFactory>();
        IImageViewerWindowFactory secondWindowFactory =
            serviceProvider.GetRequiredService<IImageViewerWindowFactory>();

        firstClipboardWriter.Should().BeSameAs(secondClipboardWriter);
        firstWindowFactory.Should().BeSameAs(secondWindowFactory);
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
        string path = TestRepositoryFiles.Find(
            Path.Combine("src", "AtomicArt.Desktop", DesktopConfigurationFile.Name));
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
