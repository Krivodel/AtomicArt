using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
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

        viewModel.Price.Should().Be("$0.0678");
        viewModel.GenerationDuration.Should().Be("30с");
    }

    [Fact]
    public void FromItem_WithMissingPriceAndDuration_ShowsUnavailableText()
    {
        GenerationItemDto item = CreateItem();

        GenerationMetadataViewModel viewModel = CreateMetadataViewModel(item);

        viewModel.Price.Should().Be(UiStrings.MetadataUnavailable);
        viewModel.GenerationDuration.Should().Be(UiStrings.MetadataUnavailable);
    }

    [Fact]
    public async Task CopyCommands_WithPromptAndPath_WriteExactText()
    {
        GenerationItemDto item = CreateItem();
        RecordingTextClipboardService clipboardService = new();
        GenerationMetadataViewModel viewModel = CreateMetadataViewModel(
            item,
            clipboardService);

        await viewModel.CopyPromptCommand.ExecuteAsync(null);

        clipboardService.Text.Should().Be(viewModel.Prompt);

        await viewModel.CopyImagePathCommand.ExecuteAsync(null);

        clipboardService.Text.Should().Be(viewModel.ImagePath);
    }

    [Fact]
    public void RequestCommands_RaiseDeferredCloseAndRepeatActions()
    {
        GenerationItemDto item = CreateItem();
        IRelayCommand closeCommand = new RelayCommand(() => { });
        IRelayCommand repeatCommand = new RelayCommand<GenerationItemViewModel>(_ => { });
        GenerationMetadataViewModel viewModel = CreateMetadataViewModel(
            item,
            closeCommand: closeCommand,
            repeatCommand: repeatCommand);
        List<GenerationMetadataActionRequestedEventArgs> requests = [];
        viewModel.ActionRequested += (_, args) => requests.Add(args);

        viewModel.RequestCloseCommand.Execute(null);
        viewModel.RequestRepeatCommand.Execute(null);

        requests.Should().HaveCount(2);
        requests[0].Command.Should().BeSameAs(closeCommand);
        requests[0].Parameter.Should().BeNull();
        requests[1].Command.Should().BeSameAs(repeatCommand);
        requests[1].Parameter.Should().BeSameAs(viewModel.Item);
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
            GenerationItemStatusDescriptorRegistryTestFactory.Create());

        return GenerationMetadataViewModel.FromItem(
            itemViewModel,
            closeCommand ?? new RelayCommand(() => { }),
            new RelayCommand(() => { }),
            repeatCommand ?? new RelayCommand(() => { }),
            textClipboardService ?? new RecordingTextClipboardService(),
            new TestViewModelErrorHandler(),
            new GenerationPriceFormatter(),
            new GenerationDurationFormatter());
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
