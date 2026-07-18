using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation.State;

public sealed class GenerationPanelStateServiceTests
{
    private const string FirstPanelId = "first-panel";
    private const string SecondPanelId = "second-panel";
    private const string FirstPrompt = "first prompt";
    private const string SecondPrompt = "second prompt";

    [Fact]
    public async Task SaveAsync_WithDifferentPanelIds_SchedulesIndependentPanelStates()
    {
        RecordingStateWriteScheduler scheduler = new();
        GenerationPanelStateService service = CreateService(
            new GenerationPanelsState(),
            scheduler,
            CreateCatalogWithTwoPanels());
        GenerationPanelState firstState = new()
        {
            PanelId = FirstPanelId,
            SelectedModelId = "first-model",
            Temperature = 1.7d,
            ThinkingLevel = "high",
            Prompt = FirstPrompt
        };
        GenerationPanelState secondState = new()
        {
            PanelId = SecondPanelId,
            SelectedModelId = "second-model",
            Prompt = SecondPrompt,
            Attachments =
            [
                new()
                {
                    Id = "invalid-attachment-id",
                    FileName = string.Empty,
                    ContentType = GenerationImageContentTypes.Png,
                    SizeBytes = 8,
                    InternalFileName = "invalid.png"
                },
                new()
                {
                    Id = "attachment-id",
                    FileName = "attachment.png",
                    ContentType = GenerationImageContentTypes.Png,
                    SizeBytes = 8,
                    InternalFileName = "attachment.png"
                }
            ]
        };

        await service.SaveAsync(FirstPanelId, firstState, CancellationToken.None);
        await service.SaveAsync(SecondPanelId, secondState, CancellationToken.None);

        GenerationPanelsState savedState = scheduler.SavedState.Should()
            .BeOfType<GenerationPanelsState>()
            .Subject;
        savedState.Panels.Should().ContainKey(FirstPanelId)
            .WhoseValue.Prompt.Should().Be(FirstPrompt);
        savedState.Panels[FirstPanelId].Temperature.Should().Be(1.7d);
        savedState.Panels[FirstPanelId].ThinkingLevel.Should().Be("high");
        savedState.Panels.Should().ContainKey(SecondPanelId)
            .WhoseValue.Prompt.Should().Be(SecondPrompt);
        savedState.Panels[SecondPanelId].Attachments.Should().ContainSingle();
    }

    [Fact]
    public async Task LoadAsync_WithUnavailableSavedValues_NormalizesToCurrentCatalog()
    {
        GenerationPanelsState existingState = new()
        {
            Panels = new Dictionary<string, GenerationPanelState>(StringComparer.Ordinal)
            {
                [FirstPanelId] = new()
                {
                    PanelId = FirstPanelId,
                    SelectedModelId = "missing-model",
                    AspectRatio = "missing-aspect",
                    Resolution = "missing-resolution",
                    GenerationCount = 999,
                    ThinkingLevel = "missing-thinking",
                    Prompt = FirstPrompt
                }
            }
        };
        GenerationPanelStateService service = CreateService(
            existingState,
            new RecordingStateWriteScheduler(),
            CreateCatalogWithTwoPanels());

        GenerationPanelState state = await service.LoadAsync(FirstPanelId, CancellationToken.None);

        state.PanelId.Should().Be(FirstPanelId);
        state.SelectedModelId.Should().Be("first-model");
        state.AspectRatio.Should().Be(GenerationAspectRatios.Auto);
        state.Resolution.Should().Be("1K");
        state.Temperature.Should().Be(
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().Temperature.Default);
        state.ThinkingLevel.Should().Be("low");
        state.GenerationCount.Should().Be(1);
        state.Prompt.Should().Be(FirstPrompt);
    }

    [Fact]
    public async Task LoadAsync_WithLegacyMinimalThinkingLevel_NormalizesToLow()
    {
        GenerationPanelsState existingState = new()
        {
            Panels = new Dictionary<string, GenerationPanelState>(StringComparer.Ordinal)
            {
                [FirstPanelId] = new()
                {
                    PanelId = FirstPanelId,
                    SelectedModelId = "first-model",
                    ThinkingLevel = "minimal"
                }
            }
        };
        GenerationPanelStateService service = CreateService(
            existingState,
            new RecordingStateWriteScheduler(),
            CreateCatalogWithTwoPanels());

        GenerationPanelState state = await service.LoadAsync(FirstPanelId, CancellationToken.None);

        state.ThinkingLevel.Should().Be("low");
    }

    [Fact]
    public async Task LoadAsync_WithThinkingUnsupportedBySelectedModel_PreservesPanelThinkingLevel()
    {
        GenerationModelMetadataDto supportedModel = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata() with
        {
            Id = "supported-model",
            PanelId = FirstPanelId
        };
        GenerationModelMetadataDto unsupportedModel = ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata() with
        {
            Id = "unsupported-model",
            PanelId = FirstPanelId
        };
        GenerationPanelsState existingState = new()
        {
            Panels = new Dictionary<string, GenerationPanelState>(StringComparer.Ordinal)
            {
                [FirstPanelId] = new()
                {
                    PanelId = FirstPanelId,
                    SelectedModelId = unsupportedModel.Id,
                    ThinkingLevel = "high"
                }
            }
        };
        GenerationPanelStateService service = CreateService(
            existingState,
            new RecordingStateWriteScheduler(),
            new GenerationModelCatalogDto(
                [supportedModel, unsupportedModel]));

        GenerationPanelState state = await service.LoadAsync(FirstPanelId, CancellationToken.None);

        state.SelectedModelId.Should().Be(unsupportedModel.Id);
        state.ThinkingLevel.Should().Be("high");
    }

    private static GenerationPanelStateService CreateService(
        GenerationPanelsState state,
        IStateWriteScheduler scheduler,
        GenerationModelCatalogDto catalog)
    {
        ImageModelOptionCatalog optionCatalog = new();
        optionCatalog.Initialize(catalog);

        return new GenerationPanelStateService(
            new StubAppStateStore(state),
            scheduler,
            optionCatalog,
            new GenerationPanelStateSection());
    }

    private static GenerationModelCatalogDto CreateCatalogWithTwoPanels()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();

        return new GenerationModelCatalogDto(
        [
            metadata with
                {
                    Id = "first-model",
                    DisplayName = "First Model",
                    PanelId = FirstPanelId,
                    Resolutions = ["1K"],
                    GenerationCounts = [1]
                },
                metadata with
                {
                    Id = "second-model",
                    DisplayName = "Second Model",
                    PanelId = SecondPanelId,
                    Resolutions = ["2K"],
                    GenerationCounts = [2]
                }
        ]);
    }
}
