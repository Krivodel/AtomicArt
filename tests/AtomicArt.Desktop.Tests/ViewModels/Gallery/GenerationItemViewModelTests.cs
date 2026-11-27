using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

public sealed class GenerationItemViewModelTests
{
    private static readonly Guid ItemId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTime CreatedAtUtc = new(2026, 6, 30, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RefreshElapsedText_WhenCreatedSecondsAgo_ReturnsSecondsText()
    {
        DateTime utcNow = new(2026, 6, 30, 10, 0, 50, DateTimeKind.Utc);
        GenerationItemViewModel viewModel = CreateViewModel(utcNow.AddSeconds(-50));

        viewModel.RefreshElapsedText(utcNow);

        viewModel.ElapsedText.Should().Be(string.Concat(
            "50",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.SecondShort)));
    }

    [Fact]
    public void RefreshElapsedText_WhenCreatedMinutesAgo_ReturnsMinutesText()
    {
        DateTime utcNow = new(2026, 6, 30, 10, 2, 0, DateTimeKind.Utc);
        GenerationItemViewModel viewModel = CreateViewModel(utcNow.AddMinutes(-2));

        viewModel.RefreshElapsedText(utcNow);

        viewModel.ElapsedText.Should().Be(string.Concat(
            "2",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.MinuteShort)));
    }

    [Fact]
    public void RefreshElapsedText_WhenTextChanges_RaisesPropertyChanged()
    {
        DateTime createdAtUtc = new(2026, 6, 30, 10, 0, 0, DateTimeKind.Utc);
        GenerationItemViewModel viewModel = CreateViewModel(createdAtUtc);
        List<string?> propertyNames = [];
        viewModel.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);

        viewModel.RefreshElapsedText(createdAtUtc.AddSeconds(5));

        propertyNames.Should().Contain(nameof(GenerationItemViewModel.ElapsedText));
    }

    [Fact]
    public void PreviewState_WhenFailed_HasFailedPriority()
    {
        GenerationItemViewModel viewModel = CreateViewModel(
            CreatedAtUtc,
            status: GenerationItemStatus.Failed,
            imagePath: "image.png");

        viewModel.IsFailed.Should().BeTrue();
        viewModel.HasDisplayImagePath.Should().BeTrue();
        viewModel.ShowsGeneratedImage.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpdateFromResult_WithCodexReply_StoresTheCompleteIndependentHistory(bool textOnly)
    {
        GenerationReference parentImage = new("parent", "parent.png", GenerationImageContentTypes.Png);
        GenerationReference image = new("branch", "branch.png", GenerationImageContentTypes.Png);
        GenerationChatImageCall parentCall = new(parentImage.Hash, "Exact parent prompt", false, "parent-call");
        GenerationChatImageCall call = new(image.Hash, "Exact branch prompt\n  Preserve spacing.", false, "branch-call");
        GenerationChatHistory parent = new(new GenerationChatMessage[]
        {
            new(false, "Parent request", CreatedAtUtc, Array.Empty<GenerationReference>()),
            new(true, "Parent reply", CreatedAtUtc, new GenerationReference[] { parentImage }, ImageGeneration: parentCall)
            { SourceCardId = Guid.Parse("66666666-6666-6666-6666-666666666666") }
        });
        GenerationItemViewModel viewModel = CreateViewModel(CreatedAtUtc, GenerationItemStatus.Generating);
        viewModel.ChatHistory = parent;
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(id: ItemId, createdAtUtc: CreatedAtUtc,
            completedAtUtc: CreatedAtUtc.AddSeconds(30)) with { ModelId = LocalCodexImageModel.Id, Prompt = "Branch request" };
        GenerationChatResult reply = textOnly
            ? new GenerationChatResult("Branch reply", null) { IsTextReply = true }
            : new GenerationChatResult("Branch reply", image) { ImageGeneration = call };

        viewModel.UpdateFromResult(item, textOnly ? null : "branch.png", null, reply);

        GenerationChatHistory complete = viewModel.ChatHistory.Should().BeOfType<GenerationChatHistory>().Subject;
        complete.IncludesCardTurn.Should().BeTrue();
        complete.Messages.Should().HaveCount(4);
        complete.Messages[1].ImageGeneration.Should().Be(parentCall);
        complete.Messages[1].SourceCardId.Should().Be(parent.Messages[1].SourceCardId);
        complete.Messages[2].Text.Should().Be(item.Prompt);
        complete.Messages[3].Text.Should().Be(reply.Text);
        complete.Messages[3].ImageGeneration.Should().Be(reply.ImageGeneration);
        complete.Messages[3].SourceCardId.Should().Be(ItemId);
        complete.Messages[3].Images.Should().Equal(textOnly ? Array.Empty<GenerationReference>() : new GenerationReference[] { image });
        complete.Id.Should().NotBe(parent.Id);
        viewModel.ChatId.Should().Be(complete.Id);
        viewModel.ChatReferenceHashes.Should().BeEquivalentTo(textOnly
            ? new[] { parentImage.Hash }
            : new[] { parentImage.Hash, image.Hash });
        parent.Messages.Should().HaveCount(2);

        viewModel.UpdateFromResult(item, textOnly ? null : "branch.png", null, reply);

        viewModel.ChatHistory.Should().BeSameAs(complete);
    }

    [Fact]
    public void UpdateFromResult_WhenTheCardIdChanges_RecordsTheFinalIdWithoutDuplicatingTheTurn()
    {
        GenerationItemViewModel viewModel = CreateViewModel(CreatedAtUtc, GenerationItemStatus.Generating);
        viewModel.ChatHistory = new GenerationChatHistory(new GenerationChatMessage[]
        {
            new(false, "Request", CreatedAtUtc, Array.Empty<GenerationReference>()),
            new(true, "Reply", CreatedAtUtc, Array.Empty<GenerationReference>()) { SourceCardId = viewModel.Id }
        }, includesCardTurn: true);
        Guid resultId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        GenerationItemDto result = GenerationItemDtoTestFactory.Create(id: resultId, createdAtUtc: CreatedAtUtc)
            with { ModelId = LocalCodexImageModel.Id };

        viewModel.UpdateFromResult(result, null, null, new GenerationChatResult("Reply", null) { IsTextReply = true });

        viewModel.ChatHistory?.Messages.Should().HaveCount(2);
        viewModel.ChatHistory?.Messages[^1].SourceCardId.Should().Be(resultId);
        viewModel.ChatHistory?.Messages[^1].Text.Should().Be("Reply");
    }

    [Fact]
    public void UpdateFromResult_WithUsagePriceAndDuration_PreservesNewResultFields()
    {
        GenerationItemViewModel viewModel = CreateViewModel(
            CreatedAtUtc,
            status: GenerationItemStatus.Generating);
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2320);
        GenerationPriceDto price = new(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage);
        DateTime completedAtUtc = CreatedAtUtc.AddSeconds(30);
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            aspectRatio: GenerationAspectRatios.Auto,
            createdAtUtc: CreatedAtUtc,
            completedAtUtc: completedAtUtc,
            generationDuration: TimeSpan.FromSeconds(30),
            price: price,
            usage: usage);

        viewModel.UpdateFromResult(item, "result.png", null);

        viewModel.CompletedAtUtc.Should().Be(completedAtUtc);
        viewModel.GenerationDuration.Should().Be(TimeSpan.FromSeconds(30));
        viewModel.Price.Should().BeSameAs(price);
        viewModel.Usage.Should().BeSameAs(usage);
    }

    [Theory]
    [InlineData(-3600)]
    [InlineData(30)]
    [InlineData(3600)]
    public void UpdateFromResult_WithDifferentServerTime_KeepsElapsedTimeFromLocalStart(
        int serverTimeOffsetSeconds)
    {
        GenerationItemViewModel viewModel = CreatePlaceholder();
        DateTime serverCreatedAtUtc = CreatedAtUtc.AddSeconds(serverTimeOffsetSeconds);
        GenerationItemDto result = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            createdAtUtc: serverCreatedAtUtc,
            completedAtUtc: serverCreatedAtUtc.AddSeconds(30),
            generationDuration: TimeSpan.FromSeconds(30));

        viewModel.UpdateFromResult(result, "result.png", null);
        viewModel.RefreshElapsedText(CreatedAtUtc.AddSeconds(45));

        viewModel.CreatedAtUtc.Should().Be(CreatedAtUtc);
        viewModel.ElapsedText.Should().Be(string.Concat(
            "45",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.SecondShort)));
        viewModel.CompletedAtUtc.Should().Be(result.CompletedAtUtc);
        viewModel.GenerationDuration.Should().Be(result.GenerationDuration);
    }

    [Fact]
    public void Restore_AfterCompletionAndStateSerialization_KeepsRequestSettingsAndStartTime()
    {
        GenerationRequestSettings settings = GenerationRequestSettingsTestFactory.Create(
            (GenerationParameterNames.AspectRatio, GenerationAspectRatios.Auto),
            (GenerationParameterNames.Resolution, "2K"),
            (GenerationParameterNames.Temperature, 0.5d),
            (GenerationParameterNames.Quality, "high"),
            (GenerationParameterNames.Complexity, "medium"),
            (GenerationParameterNames.ThinkingLevel, "high"));
        GenerationItemViewModel viewModel = CreatePlaceholder(settings);
        GenerationItemDto result = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            aspectRatio: "16:9",
            createdAtUtc: CreatedAtUtc.AddHours(1));
        JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);
        GalleryStateSection section = new();

        viewModel.UpdateFromResult(result, "result.png", null);
        GalleryState savedState = new()
        {
            Items = new List<GalleryItemState> { viewModel.CreateState() }
        };
        JsonElement payload = JsonSerializer.SerializeToElement(savedState, serializerOptions);
        GalleryState restoredState = (GalleryState)section.DeserializePayload(
            section.SchemaVersion,
            payload,
            serializerOptions);
        GalleryItemState restoredItem = restoredState.Items.Single();
        GenerationItemViewModel restoredViewModel = GenerationItemViewModel.Restore(
            restoredItem,
            restoredItem.ImagePath,
            restoredItem.ThumbnailPath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestLocalizationTextProvider.Default);
        restoredViewModel.RefreshElapsedText(CreatedAtUtc.AddMinutes(2));

        restoredViewModel.CreatedAtUtc.Should().Be(CreatedAtUtc);
        JsonSerializer.Serialize(restoredViewModel.RequestSettings, serializerOptions)
            .Should().Be(JsonSerializer.Serialize(settings, serializerOptions));
        restoredViewModel.AspectRatio.Should().Be(result.AspectRatio);
        restoredViewModel.ElapsedText.Should().Be(string.Concat(
            "2",
            TestLocalizationTextProvider.Default.Get(CommonLocalizationKeys.TimeUnits.MinuteShort)));
        restoredViewModel.GalleryOrderTimestampUtc.Should().Be(CreatedAtUtc);
    }

    [Fact]
    public void Deserialize_WithFlatRequestSettings_KeepsParametersAndDropsImageCount()
    {
        const string Json = """
            {
              "aspectRatio": "auto",
              "resolution": "2K",
              "generationCount": 4,
              "quality": "high",
              "thinkingLevel": "low",
              "thinkingLevelLocalizationKey": "Generation.Thinking.Low"
            }
            """;
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

        GenerationRequestSettings settings = JsonSerializer
            .Deserialize<GenerationRequestSettings>(Json, options)
            ?? throw new InvalidOperationException("The legacy request settings were not loaded.");
        string saved = JsonSerializer.Serialize(settings, options);

        settings.GetString(GenerationParameterNames.Quality).Should().Be("high");
        settings.Parameters[GenerationParameterNames.ThinkingLevel].ValueLocalizationKey
            .Should().Be(GenerationLocalizationKeys.ThinkingLow);
        saved.Should().NotContain("generationCount");
    }

    [Fact]
    public void Deserialize_WithParameterMissingValue_CanResaveValidParameters()
    {
        const string Json = """
            {
              "parameters": {
                "quality": { "value": "high" },
                "complexity": { "labelLocalizationKey": "Generation.Labels.Complexity" }
              }
            }
            """;
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

        GenerationRequestSettings settings = JsonSerializer
            .Deserialize<GenerationRequestSettings>(Json, options)
            ?? throw new InvalidOperationException("The request settings were not loaded.");
        string saved = JsonSerializer.Serialize(settings, options);
        GenerationRequestSettings? restored = JsonSerializer
            .Deserialize<GenerationRequestSettings>(saved, options);

        restored.Should().NotBeNull();
        restored!.Parameters.Keys.Should().Equal(GenerationParameterNames.Quality);
        restored.GetString(GenerationParameterNames.Quality).Should().Be("high");
    }

    [Fact]
    public void DisplayThumbnailPath_WithThumbnailPath_ReturnsThumbnailPath()
    {
        GenerationItemViewModel viewModel = CreateViewModel(
            CreatedAtUtc,
            imagePath: "image.png");
        viewModel.ThumbnailPath = "thumbnail.png";

        string displayPath = viewModel.DisplayThumbnailPath;

        displayPath.Should().Be("thumbnail.png");
    }

    [Fact]
    public void DisplayThumbnailPath_WithoutThumbnailPath_ReturnsEmptyPath()
    {
        GenerationItemViewModel viewModel = CreateViewModel(
            CreatedAtUtc,
            imagePath: "image.png");

        string displayPath = viewModel.DisplayThumbnailPath;

        displayPath.Should().BeEmpty();
    }

    [Fact]
    public void ToState_WithThumbnailPath_IncludesThumbnailPath()
    {
        GenerationItemViewModel viewModel = CreateViewModel(
            CreatedAtUtc,
            imagePath: "image.png");
        viewModel.ThumbnailPath = "thumbnail.png";

        GalleryItemState state = viewModel.CreateState();

        state.ThumbnailPath.Should().Be("thumbnail.png");
    }

    [Fact]
    public void CreateState_WhenFavorite_IncludesFavoriteState()
    {
        GenerationItemViewModel viewModel = CreateViewModel(CreatedAtUtc);
        viewModel.IsFavorite = true;

        GalleryItemState state = viewModel.CreateState();

        state.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void Restore_WithFavoriteState_RestoresFavoriteState()
    {
        GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated(
            id: ItemId,
            createdAtUtc: CreatedAtUtc,
            isFavorite: true);

        GenerationItemViewModel viewModel = GenerationItemViewModel.Restore(
            state,
            state.ImagePath,
            state.ThumbnailPath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestLocalizationTextProvider.Default);

        viewModel.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void MarkFailed_WithCode_PreservesCode()
    {
        GenerationItemViewModel viewModel = CreateViewModel(
            CreatedAtUtc,
            status: GenerationItemStatus.Generating);

        viewModel.MarkFailed(GenerationProviderFailureErrorCodes.RequestRejected);

        viewModel.IsFailed.Should().BeTrue();
        viewModel.FailureCode.Should().Be(
            GenerationProviderFailureErrorCodes.RequestRejected);
    }

    private static GenerationItemViewModel CreatePlaceholder(
        GenerationRequestSettings? requestSettings = null)
    {
        GenerationLifecycleEvent startedEvent = GalleryLifecycleTestFactory.CreateStartedEvent(
            ItemId,
            CreatedAtUtc,
            generationCount: 1,
            attachedImagesCount: 0,
            requestSettings: requestSettings);
        GenerationStartSnapshot start = startedEvent.Start
            ?? throw new InvalidOperationException("Test generation start snapshot is missing.");

        return GenerationItemViewModel.CreatePlaceholder(
            start,
            ItemId,
            0,
            CreatedAtUtc,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestLocalizationTextProvider.Default);
    }

    private static GenerationItemViewModel CreateViewModel(
        DateTime createdAtUtc,
        GenerationItemStatus status = GenerationItemStatus.Generated,
        string? imagePath = null)
    {
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            aspectRatio: GenerationAspectRatios.Auto,
            createdAtUtc: createdAtUtc,
            status: status,
            imagePath: imagePath);

        return new GenerationItemViewModel(
            item,
            0,
            imagePath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestLocalizationTextProvider.Default);
    }
}
