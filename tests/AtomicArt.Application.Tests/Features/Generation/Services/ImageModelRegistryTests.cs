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

        IReadOnlyList<ImageModelOption> models = registry.GetModels();

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
        GenerationModelCatalogDto catalog = new(
        [
            CreateMetadata("duplicate"),
                CreateMetadata("duplicate")
        ]);

        Action action = () => MetadataImageModelTestFactory.CreateRegistry(catalog);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_WithEmptyCatalog_ThrowsInvalidOperationException()
    {
        GenerationModelCatalogDto catalog = new([]);

        Action action = () => MetadataImageModelTestFactory.CreateRegistry(catalog);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetModels_WithMultipleMatchingFactories_UsesHighestPriorityFactory()
    {
        GenerationModelCatalogDto catalog = new(
        [
            CreateMetadata()
        ]);
        IImageModelDefinitionFactory[] factories =
        [
            new TestImageModelDefinitionFactory(priority: 0),
            new TestImageModelDefinitionFactory(priority: 10)
        ];

        ImageModelRegistry registry = new(catalog, factories);

        registry.GetModels().Should().ContainSingle()
            .Which.DisplayName.Should().Be("Test Factory 10");
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

    private sealed class TestImageModelDefinitionFactory : IImageModelDefinitionFactory
    {
        public int Priority { get; }

        public TestImageModelDefinitionFactory(int priority)
        {
            Priority = priority;
        }

        public bool CanCreate(GenerationModelMetadataDto metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return true;
        }

        public IImageModelDefinition Create(GenerationModelMetadataDto metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            return new TestImageModelDefinition(metadata, $"Test Factory {Priority}");
        }
    }

    private sealed class TestImageModelDefinition : IImageModelDefinition
    {
        public string Id => _metadata.Id;
        public string DisplayName { get; }
        public string Provider => _metadata.Provider;
        public string ProviderModelId => _metadata.ProviderModelId;
        public string PanelId => _metadata.PanelId;
        public int ContextWindowTokens => _metadata.ContextWindowTokens;
        public int MaxOutputTokens => _metadata.MaxOutputTokens;
        public int MaxAttachedImages => _metadata.Attachments.MaxCount;
        public int? MaxPromptLength => _metadata.MaxPromptLength;
        public long MaxAttachedImageBytes => _metadata.Attachments.MaxSingleFileBytes;
        public long MaxTotalAttachedImageBytes => _metadata.Attachments.MaxTotalBytes;
        public GenerationModelTemperatureMetadataDto Temperature => _metadata.Temperature;
        public GenerationModelThinkingMetadataDto? Thinking => _metadata.Thinking;
        public GenerationModelPricingMetadataDto Pricing => _metadata.Pricing;
        public GenerationModelConstraints Constraints => new(
            _metadata.Id,
            _metadata.MaxPromptLength,
            _metadata.AspectRatios,
            _metadata.Resolutions,
            _metadata.GenerationCounts,
            new GenerationModelTemperatureConstraints(
                _metadata.Temperature.Minimum,
                _metadata.Temperature.Maximum,
                _metadata.Temperature.Default,
                _metadata.Temperature.Step),
            _metadata.Attachments.MaxCount,
            _metadata.Attachments.MaxSingleFileBytes,
            _metadata.Attachments.MaxTotalBytes,
            _metadata.Attachments.SupportedContentTypes);

        private readonly GenerationModelMetadataDto _metadata;

        public TestImageModelDefinition(GenerationModelMetadataDto metadata, string displayName)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

            _metadata = metadata;
            DisplayName = displayName;
        }

        public IReadOnlyList<string> GetAspectRatios()
        {
            return _metadata.AspectRatios;
        }

        public IReadOnlyList<string> GetResolutions()
        {
            return _metadata.Resolutions;
        }

        public IReadOnlyList<int> GetGenerationCounts()
        {
            return _metadata.GenerationCounts;
        }

        public IReadOnlyList<string> GetSupportedContentTypes()
        {
            return _metadata.Attachments.SupportedContentTypes;
        }

        public Result<ImageGenerationRequestDto> Validate(ImageGenerationRequestDto request)
        {
            throw new NotSupportedException("Test definition does not validate requests.");
        }
    }
}
