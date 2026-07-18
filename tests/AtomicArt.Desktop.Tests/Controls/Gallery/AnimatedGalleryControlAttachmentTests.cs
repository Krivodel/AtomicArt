using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public sealed class AnimatedGalleryControlAttachmentTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void OnAttachedToVisualTree_WhenControlAttached_CreatesSceneWithOverlayCanvas()
    {
        Dispatch(() =>
        {
            AnimatedGalleryControl control = CreateControlWithItem(CreateItem());

            Show(control, _ =>
            {
                AssertAttachedScene(control);
            });
        });
    }

    [Fact]
    public void OnAttachedToVisualTree_WhenItemProvided_PassesItemToGeneratedCardDataContext()
    {
        Dispatch(() =>
        {
            ShowSingleCard((card, item) =>
            {
                card.DataContext.Should().BeSameAs(item);
            });
        });
    }

    [Fact]
    public void OnAttachedToVisualTree_WhenCardCreated_SizesCardCellAndPreservesCardContentBindings()
    {
        Dispatch(() =>
        {
            ShowSingleCard((card, item) =>
            {
                AssertCardContent(card, item);
            });
        });
    }

    [Fact]
    public void OnAttachedToVisualTree_WhenCommandsProvided_PassesCommandsToGeneratedCards()
    {
        Dispatch(() =>
        {
            RelayCommand revealCommand = new(() => { });
            RelayCommand metadataCommand = new(() => { });
            RelayCommand deleteCommand = new(() => { });
            AnimatedGalleryControl control = CreateControlWithCommands(
                revealCommand,
                metadataCommand,
                deleteCommand);

            Show(control, _ =>
            {
                GenerationCardControl card = GetSingleCard(control);

                card.RevealInFolderCommand.Should().BeSameAs(revealCommand);
                card.OpenMetadataCommand.Should().BeSameAs(metadataCommand);
                card.DeleteOrCancelCommand.Should().BeSameAs(deleteCommand);
            });
        });
    }

    [Fact]
    public void OnAttachedToVisualTree_WithOperations_RegistersAndDetachesSceneOperations()
    {
        Dispatch(() =>
        {
            RecordingGalleryOperations operations = new();
            AnimatedGalleryControl control = CreateControlWithOperations(operations);

            Show(control, window =>
            {
                operations.AttachedOperations.Should().NotBeNull();

                window.Content = null;
                window.CaptureRenderedFrame();

                operations.DetachedOperations.Should().BeSameAs(operations.AttachedOperations);
            });
        });
    }

    private static AnimatedGalleryControl CreateControlWithItem(GenerationItemViewModel item)
    {
        return new AnimatedGalleryControl(CreateSceneFactory())
        {
            Items = new List<GenerationItemViewModel>
            {
                item
            }
        };
    }

    private static AnimatedGalleryControl CreateControlWithCommands(
        RelayCommand revealCommand,
        RelayCommand metadataCommand,
        RelayCommand deleteCommand)
    {
        AnimatedGalleryControl control = CreateControlWithItem(CreateItem());
        control.RevealInFolderCommand = revealCommand;
        control.OpenMetadataCommand = metadataCommand;
        control.DeleteOrCancelCommand = deleteCommand;

        return control;
    }

    private static AnimatedGalleryControl CreateControlWithOperations(
        RecordingGalleryOperations operations)
    {
        AnimatedGalleryControl control = CreateControlWithItem(CreateItem());
        control.Operations = operations;

        return control;
    }

    private static void AssertAttachedScene(AnimatedGalleryControl control)
    {
        Canvas galleryPanel = GetGalleryPanel(control);
        Canvas overlayCanvas = GetOverlayCanvas(control);
        ScrollViewer scrollViewer = GetGalleryScrollViewer(control);

        overlayCanvas.IsHitTestVisible.Should().BeFalse();
        scrollViewer.VerticalScrollBarVisibility.Should().Be(ScrollBarVisibility.Visible);
        galleryPanel.HorizontalAlignment.Should().Be(HorizontalAlignment.Center);
        galleryPanel.VerticalAlignment.Should().Be(VerticalAlignment.Top);
        galleryPanel.Children.OfType<GenerationCardControl>().Should().ContainSingle();
    }

    private static GenerationCardControl GetSingleCard(AnimatedGalleryControl control)
    {
        return GetGalleryPanel(control)
            .Children
            .OfType<GenerationCardControl>()
            .Single();
    }

    private static void AssertCardContent(
        GenerationCardControl card,
        GenerationItemViewModel item)
    {
        List<string?> textValues = card.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(textBlock => textBlock.Text)
            .ToList();

        card.Width.Should().Be(GalleryLayoutService.CardWidth);
        card.Height.Should().Be(GalleryLayoutService.CardHeight);
        card.DataContext.Should().BeSameAs(item);
        textValues.Should().Contain(item.Prompt);
        textValues.Should().Contain(item.ModelDisplayName);
        textValues.Should().Contain(item.ElapsedText);
    }

    private static void ShowSingleCard(
        Action<GenerationCardControl, GenerationItemViewModel> action)
    {
        GenerationItemViewModel item = CreateItem();
        AnimatedGalleryControl control = CreateControlWithItem(item);

        Show(control, _ =>
        {
            GenerationCardControl card = GetSingleCard(control);

            action(card, item);
        });
    }
}
