using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;
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
            price: new GenerationPriceDto(0.0678m, "USD", "ActualProviderUsage"),
            generationDuration: TimeSpan.FromSeconds(30));
        GenerationItemViewModel itemViewModel = CreateItemViewModel(item);

        GenerationMetadataViewModel viewModel = GenerationMetadataViewModel.FromItem(
            itemViewModel,
            new RelayCommand(() => { }),
            new GenerationPriceFormatter(),
            new GenerationDurationFormatter());

        viewModel.Price.Should().Be("$0.0678");
        viewModel.GenerationDuration.Should().Be("30с");
    }

    [Fact]
    public void FromItem_WithMissingPriceAndDuration_ShowsUnavailableText()
    {
        GenerationItemDto item = CreateItem();
        GenerationItemViewModel itemViewModel = CreateItemViewModel(item);

        GenerationMetadataViewModel viewModel = GenerationMetadataViewModel.FromItem(
            itemViewModel,
            new RelayCommand(() => { }),
            new GenerationPriceFormatter(),
            new GenerationDurationFormatter());

        viewModel.Price.Should().Be(UiStrings.MetadataUnavailable);
        viewModel.GenerationDuration.Should().Be(UiStrings.MetadataUnavailable);
    }

    private static GenerationItemViewModel CreateItemViewModel(GenerationItemDto item)
    {
        return new GenerationItemViewModel(
            item,
            0,
            item.ImagePath,
            GenerationItemStatusDescriptorRegistryTestFactory.Create());
    }

    private static GenerationItemDto CreateItem(
        GenerationPriceDto? price = null,
        TimeSpan? generationDuration = null)
    {
        return GenerationItemDtoTestFactory.Create(
            id: ItemId,
            aspectRatio: "1:1",
            resolution: ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().Resolutions[1],
            createdAtUtc: CreatedAtUtc,
            completedAtUtc: CreatedAtUtc.AddSeconds(30),
            generationDuration: generationDuration,
            price: price);
    }
}
