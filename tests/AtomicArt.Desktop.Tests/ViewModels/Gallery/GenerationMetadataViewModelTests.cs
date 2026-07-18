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
    private const string NanoBanana2ModelId = "nano-banana-2";
    private const string NanoBanana2DisplayName = "Nano Banana 2";

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
        return new GenerationItemDto(
            ItemId,
            NanoBanana2ModelId,
            NanoBanana2DisplayName,
            "Prompt",
            "1:1",
            "1K",
            CreatedAtUtc,
            GenerationItemStatus.Generated,
            null,
            null,
            CreatedAtUtc.AddSeconds(30),
            generationDuration,
            price,
            null);
    }
}
