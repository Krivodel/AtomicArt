using FluentAssertions;
using Xunit;

using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Application.Tests.Generation;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Tests.Features.Generation.Services;

public sealed class ImageModelRegistryTests
{
    [Fact]
    public void GetModels_WithRegisteredModel_ReturnsModels()
    {
        ImageModelRegistry registry = MetadataImageModelTestFactory.CreateRegistry();

        IReadOnlyList<GenerationModelMetadataDto> models = registry.GetModels();

        models.Should().HaveCount(3);
        GenerationModelMetadataDto nanoBanana2Metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        models.Should().Contain(model =>
            model.Id == ApiModelMetadataTestCatalog.NanoBanana2ModelId
            && model.DisplayName == nanoBanana2Metadata.DisplayName);
        models.Single(model => model.Id == ApiModelMetadataTestCatalog.NanoBanana2ModelId)
            .Temperature.Should().Be(nanoBanana2Metadata.Temperature);
        models.Should().Contain(model =>
            model.Id == ApiModelMetadataTestCatalog.NanoBananaProModelId
            && model.ProviderModelId == ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata().ProviderModelId);
        models.Select(model => model.PanelId)
            .Should()
            .OnlyContain(panelId => panelId == GenerationPanelIds.NanoBanana);
    }

    [Fact]
    public void GetById_WithUnknownId_ReturnsNull()
    {
        ImageModelRegistry registry = MetadataImageModelTestFactory.CreateRegistry();

        IImageModelDefinition? definition = registry.GetById("unknown");

        definition.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithDuplicateIds_ThrowsInvalidOperationException()
    {
        GenerationModelCatalogDto catalog = CreateCatalog(
            CreateMetadata("duplicate"),
            CreateMetadata("duplicate"));

        AssertRegistryConstructionFails(catalog);
    }

    [Fact]
    public void Constructor_WithEmptyCatalog_ThrowsInvalidOperationException()
    {
        GenerationModelCatalogDto catalog = CreateCatalog();

        AssertRegistryConstructionFails(catalog);
    }

    [Fact]
    public void GetModels_WithMultipleMatchingFactories_UsesHighestPriorityFactory()
    {
        GenerationModelCatalogDto catalog = CreateCatalog(CreateMetadata());
        IImageModelDefinitionFactory[] factories =
        [
            new TestImageModelDefinitionFactory(priority: 0),
            new TestImageModelDefinitionFactory(priority: 10)
        ];

        ImageModelRegistry registry = new(catalog, factories);

        registry.GetModels().Should().ContainSingle()
            .Which.DisplayName.Should().Be("Test Factory 10");
    }

    [Fact]
    public void Constructor_WithoutMatchingFactory_ThrowsInvalidOperationException()
    {
        IImageModelDefinitionFactory[] factories =
        [
            new TestImageModelDefinitionFactory(0, _ => false)
        ];

        AssertFactorySelectionFails(
            factories,
            "No generation model factory is registered for model 'test-model'.");
    }

    [Fact]
    public void Constructor_WithMatchingFactoriesAtSameHighestPriority_ThrowsInvalidOperationException()
    {
        IImageModelDefinitionFactory[] factories =
        [
            new TestImageModelDefinitionFactory(priority: 10),
            new TestImageModelDefinitionFactory(priority: 10)
        ];

        AssertFactorySelectionFails(
            factories,
            "Multiple generation model factories with priority 10 are registered for model 'test-model'.");
    }

    private static GenerationModelCatalogDto CreateCatalog(
        params GenerationModelMetadataDto[] models)
    {
        return new GenerationModelCatalogDto(models);
    }

    private static GenerationModelMetadataDto CreateMetadata(string modelId = "test-model")
    {
        return new GenerationModelMetadataDto(
            modelId,
            "Test model",
            "google",
            $"provider-{modelId}",
            GenerationPanelIds.NanoBanana,
            4_000,
            1_000,
            4_000,
            [GenerationAspectRatios.Auto],
            ["4k"],
            [1],
            new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d),
            new GenerationModelAttachmentMetadataDto(
                10,
                1_048_576,
                4_194_304,
                [GenerationImageContentTypes.Png]),
            new GenerationModelPricingMetadataDto(
                "USD",
                0.25m,
                1.50m,
                30.00m,
                1120,
                new Dictionary<string, int>
                {
                    ["4k"] = 2520
                }));
    }

    private static void AssertRegistryConstructionFails(GenerationModelCatalogDto catalog)
    {
        Action action = () => MetadataImageModelTestFactory.CreateRegistry(catalog);

        action.Should().Throw<InvalidOperationException>();
    }

    private static void AssertFactorySelectionFails(
        IImageModelDefinitionFactory[] factories,
        string expectedMessage)
    {
        GenerationModelCatalogDto catalog = CreateCatalog(CreateMetadata());

        Action action = () => new ImageModelRegistry(catalog, factories);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage(expectedMessage);
    }

    private sealed class TestImageModelDefinitionFactory : IImageModelDefinitionFactory
    {
        public int Priority { get; }

        private readonly Func<GenerationModelMetadataDto, bool> _canCreate;

        public TestImageModelDefinitionFactory(int priority)
            : this(priority, _ => true)
        {
        }

        public TestImageModelDefinitionFactory(
            int priority,
            Func<GenerationModelMetadataDto, bool> canCreate)
        {
            Priority = priority;
            _canCreate = canCreate ?? throw new ArgumentNullException(nameof(canCreate));
        }

        public bool CanCreate(GenerationModelMetadataDto metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return _canCreate(metadata);
        }

        public IImageModelDefinition Create(GenerationModelMetadataDto metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return new TestImageModelDefinition(metadata, $"Test Factory {Priority}");
        }
    }

    private sealed class TestImageModelDefinition : IImageModelDefinition
    {
        public GenerationModelMetadataDto Metadata { get; }
        public GenerationModelConstraints Constraints { get; }

        public TestImageModelDefinition(GenerationModelMetadataDto metadata, string displayName)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

            Metadata = metadata with
            {
                DisplayName = displayName
            };
            Constraints = MetadataImageModelTestFactory.CreateDefinition(metadata).Constraints;
        }

        public Result<ImageGenerationRequestDto> Validate(ImageGenerationRequestDto request)
        {
            throw new NotSupportedException("Test definition does not validate requests.");
        }
    }
}
