using System.Globalization;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

public sealed class GenerationMetadataViewModelTests
{
    private static readonly Guid ItemId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTime CreatedAtUtc = new(2026, 6, 30, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FromItem_WithPriceAndDuration_ShowsNewProperties()
    {
        GenerationItemDto item = CreateItem(
            price: new GenerationPriceDto(
                0.0678m,
                "USD",
                GenerationPriceSources.ActualProviderUsage),
            generationDuration: TimeSpan.FromSeconds(30));

        GenerationMetadataViewModel viewModel = CreateMetadataViewModel(item);

        GenerationMetadataRow row = viewModel.Rows[0];
        (row.Third?.Value).Should().Be(
            string.Concat("$", 0.0678m.ToString("0.####", CultureInfo.CurrentCulture)));
        (row.Second?.Value).Should().Be(
            new GenerationDurationFormatter(
                TestLocalizationTextProvider.Default).Format(
                TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void FromItem_WithMissingPriceAndDuration_ShowsUnavailableText()
    {
        GenerationItemDto item = CreateItem();

        GenerationMetadataViewModel viewModel = CreateMetadataViewModel(item);

        GenerationMetadataRow row = viewModel.Rows[0];
        string unavailable = TestLocalizationTextProvider.Default.Get(GalleryLocalizationKeys.Metadata.Unavailable);
        (row.Second?.Value).Should().Be(unavailable);
        (row.Third?.Value).Should().Be(unavailable);
    }

    [Fact]
    public void Rows_WithSavedOptions_ShowsLocalizedValuesAndChangedResolution()
    {
        GenerationRequestSettings settings = GenerationRequestSettingsTestFactory.Create(
            (GenerationParameterNames.AspectRatio, GenerationAspectRatios.Auto),
            (GenerationParameterNames.Resolution, "4K"),
            (GenerationParameterNames.Quality, "high"),
            (GenerationParameterNames.Complexity, "medium"),
            (GenerationParameterNames.Temperature, 0.30000000000000004d));
        GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated(requestSettings: settings);
        GenerationItemViewModel item = GenerationItemViewModel.Restore(
            state,
            state.ImagePath,
            null,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestLocalizationTextProvider.Default);
        using GenerationMetadataViewModel viewModel = CreateMetadataViewModel(item);

        IReadOnlyList<GenerationMetadataRow> rows = viewModel.Rows;

        rows.Should().HaveCount(3);
        rows[1].First.Value.Should().Be(state.Resolution);
        rows[1].Third.Should().Be(new GenerationMetadataField("Requested resolution", "4K"));
        rows[2].Should().Be(new GenerationMetadataRow(
            new GenerationMetadataField("Quality", "High"),
            new GenerationMetadataField("Complexity", "Medium"),
            new GenerationMetadataField(
                "Temperature",
                string.Concat("0", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, "3"))));
    }

    [Fact]
    public void Rows_WithLegacyItem_LeavesLastCellEmpty()
    {
        using GenerationMetadataViewModel viewModel = CreateMetadataViewModel(CreateItem());

        IReadOnlyList<GenerationMetadataRow> rows = viewModel.Rows;

        rows.Should().HaveCount(2);
        rows[1].Third.Should().BeNull();
    }

    [Fact]
    public async Task CopyPromptCommand_WithPrompt_WritesExactText()
    {
        GenerationItemDto item = CreateItem();
        RecordingTextClipboardService clipboardService = new();
        GenerationMetadataViewModel viewModel = CreateMetadataViewModel(
            item,
            clipboardService);

        await viewModel.CopyPromptCommand.ExecuteAsync(null);

        clipboardService.Text.Should().Be(viewModel.Prompt);

    }

    [Fact]
    public void FromItem_WhenSourceItemCompletes_RefreshesMutableProperties()
    {
        GenerationItemDto generatingItem = CreateItem(status: GenerationItemStatus.Generating);
        GenerationMetadataViewModel viewModel = CreateMetadataViewModel(generatingItem);
        GenerationPriceDto price = new(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage);
        GenerationItemDto completedItem = CreateItem(
            status: GenerationItemStatus.Generated,
            imagePath: "result.png",
            price: price,
            generationDuration: TimeSpan.FromSeconds(30));
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.Item.UpdateFromResult(completedItem, completedItem.ImagePath, null);

        changedProperties.Should().Contain(nameof(GenerationMetadataViewModel.Rows));
        viewModel.IsGenerated.Should().BeTrue();
        viewModel.IsGenerating.Should().BeFalse();
        GenerationMetadataRow row = viewModel.Rows[0];
        (row.Third?.Value).Should().Be(
            string.Concat("$", 0.0678m.ToString("0.####", CultureInfo.CurrentCulture)));
        (row.Second?.Value).Should().Be(
            new GenerationDurationFormatter(
                TestLocalizationTextProvider.Default).Format(
                TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Dispose_WhenSourceItemChanges_DoesNotPublishPropertyChanges()
    {
        GenerationItemDto generatingItem = CreateItem(status: GenerationItemStatus.Generating);
        GenerationMetadataViewModel viewModel = CreateMetadataViewModel(generatingItem);
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        GenerationItemDto completedItem = CreateItem(
            status: GenerationItemStatus.Generated,
            imagePath: "result.png");
        viewModel.Dispose();

        viewModel.Item.UpdateFromResult(completedItem, completedItem.ImagePath, null);

        changedProperties.Should().BeEmpty();
    }

    private static GenerationMetadataViewModel CreateMetadataViewModel(
        GenerationItemDto item,
        ITextClipboardService? textClipboardService = null,
        IRelayCommand? closeCommand = null,
        IRelayCommand? repeatCommand = null)
    {
        GenerationItemViewModel itemViewModel = new(
            item,
            0,
            item.ImagePath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(),
            TestLocalizationTextProvider.Default);

        return CreateMetadataViewModel(itemViewModel, textClipboardService, closeCommand, repeatCommand);
    }

    private static GenerationMetadataViewModel CreateMetadataViewModel(
        GenerationItemViewModel item,
        ITextClipboardService? textClipboardService = null,
        IRelayCommand? closeCommand = null,
        IRelayCommand? repeatCommand = null)
    {
        return GenerationMetadataViewModel.FromItem(
            item,
            closeCommand ?? new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            repeatCommand ?? new RelayCommand(() => { }),
            textClipboardService ?? new RecordingTextClipboardService(),
            new TestViewModelErrorHandler(),
            new GenerationPriceFormatter(),
            new GenerationDurationFormatter(TestLocalizationTextProvider.Default),
            TestLocalizationTextProvider.Default);
    }

    private static GenerationItemDto CreateItem(
        GenerationPriceDto? price = null,
        TimeSpan? generationDuration = null,
        GenerationItemStatus status = GenerationItemStatus.Generated,
        string? imagePath = "image.png")
    {
        return GenerationItemDtoTestFactory.Create(
            id: ItemId,
            aspectRatio: "1:1",
            resolution: ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().Resolutions[1],
            createdAtUtc: CreatedAtUtc,
            completedAtUtc: CreatedAtUtc.AddSeconds(30),
            generationDuration: generationDuration,
            status: status,
            imagePath: imagePath,
            price: price);
    }
}
