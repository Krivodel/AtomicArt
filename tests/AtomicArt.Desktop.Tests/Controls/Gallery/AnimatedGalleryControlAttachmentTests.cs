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
            Window window = Show(control);

            try
            {
                AssertAttachedScene(control);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void OnAttachedToVisualTree_WhenItemProvided_PassesItemToGeneratedCardDataContext()
    {
        Dispatch(() =>
        {
            GenerationItemViewModel item = CreateItem();
            AnimatedGalleryControl control = CreateControlWithItem(item);
            Window window = Show(control);

            try
            {
                GenerationCardControl card = GetSingleCard(control);

                card.DataContext.Should().BeSameAs(item);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void OnAttachedToVisualTree_WhenCardCreated_SizesCardCellAndPreservesCardContentBindings()
    {
        Dispatch(() =>
        {
            GenerationItemViewModel item = CreateItem();
            AnimatedGalleryControl control = CreateControlWithItem(item);
            Window window = Show(control);

            try
            {
                GenerationCardControl card = GetSingleCard(control);

                AssertCardContent(card, item);
            }
            finally
            {
                window.Close();
            }
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
            Window window = Show(control);

            try
            {
                GenerationCardControl card = GetSingleCard(control);

                card.RevealInFolderCommand.Should().BeSameAs(revealCommand);
                card.OpenMetadataCommand.Should().BeSameAs(metadataCommand);
                card.DeleteOrCancelCommand.Should().BeSameAs(deleteCommand);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void OnAttachedToVisualTree_WithOperations_RegistersAndDetachesSceneOperations()
    {
        Dispatch(() =>
        {
            RecordingGalleryOperations operations = new();
            AnimatedGalleryControl control = CreateControlWithOperations(operations);
            Window window = Show(control);

            try
            {
                operations.AttachedOperations.Should().NotBeNull();

                window.Content = null;
                window.CaptureRenderedFrame();

                operations.DetachedOperations.Should().BeSameAs(operations.AttachedOperations);
            }
            finally
            {
                window.Close();
            }
        });
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

    private AnimatedGalleryControl CreateControlWithItem(GenerationItemViewModel item)
    {
        return new AnimatedGalleryControl(CreateSceneFactory())
        {
            Items = new List<GenerationItemViewModel>
            {
                item
            }
        };
    }

    private AnimatedGalleryControl CreateControlWithCommands(
        RelayCommand revealCommand,
        RelayCommand metadataCommand,
        RelayCommand deleteCommand)
    {
        return new AnimatedGalleryControl(CreateSceneFactory())
        {
            Items = new List<GenerationItemViewModel>
            {
                CreateItem()
            },
            RevealInFolderCommand = revealCommand,
            OpenMetadataCommand = metadataCommand,
            DeleteOrCancelCommand = deleteCommand
        };
    }

    private AnimatedGalleryControl CreateControlWithOperations(RecordingGalleryOperations operations)
    {
        return new AnimatedGalleryControl(CreateSceneFactory())
        {
            Items = new List<GenerationItemViewModel>
            {
                CreateItem()
            },
            Operations = operations
        };
    }
}
