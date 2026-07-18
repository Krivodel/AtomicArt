using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Tests.Services.Generation;
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

        viewModel.ElapsedText.Should().Be("50с");
    }

    [Fact]
    public void RefreshElapsedText_WhenCreatedMinutesAgo_ReturnsMinutesText()
    {
        DateTime utcNow = new(2026, 6, 30, 10, 2, 0, DateTimeKind.Utc);
        GenerationItemViewModel viewModel = CreateViewModel(utcNow.AddMinutes(-2));

        viewModel.RefreshElapsedText(utcNow);

        viewModel.ElapsedText.Should().Be("2м");
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
        GenerationPriceDto price = new(0.0678m, "USD", "ActualProviderUsage");
        DateTime completedAtUtc = CreatedAtUtc.AddSeconds(30);
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            aspectRatio: "Авто",
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
    public void DisplayThumbnailPath_WithoutThumbnailPath_ReturnsImagePath()
    {
        GenerationItemViewModel viewModel = CreateViewModel(
            CreatedAtUtc,
            imagePath: "image.png");

        string displayPath = viewModel.DisplayThumbnailPath;

        displayPath.Should().Be("image.png");
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

    private static GenerationItemViewModel CreateViewModel(
        DateTime createdAtUtc,
        GenerationItemStatus status = GenerationItemStatus.Generated,
        string? imagePath = null)
    {
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            id: ItemId,
            aspectRatio: "Авто",
            createdAtUtc: createdAtUtc,
            status: status,
            imagePath: imagePath);

        return new GenerationItemViewModel(
            item,
            0,
            imagePath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create());
    }
}
