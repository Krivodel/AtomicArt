using System.Collections.ObjectModel;

using Avalonia.Controls;
using Avalonia.Headless;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public sealed class AnimatedGalleryControlCollectionTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void CollectionChanged_WhenAttached_RefreshesVisibleCards()
    {
        Dispatch(() =>
        {
            ShowCollection((items, control, window) =>
            {
                items.Add(CreateItem());
                window.CaptureRenderedFrame();

                GetGalleryPanel(control)
                    .Children
                    .OfType<GenerationCardControl>()
                    .Should()
                    .HaveCount(2);
            });
        });
    }

    [Fact]
    public void CollectionChanged_WhenOperationsProvided_DoesNotRefreshVisibleCardsDirectly()
    {
        Dispatch(() =>
        {
            RecordingGalleryOperations operations = new();

            ShowCollection(operations, (items, control, window) =>
            {
                items.Add(CreateItem());
                window.CaptureRenderedFrame();

                GetGalleryPanel(control)
                    .Children
                    .OfType<GenerationCardControl>()
                    .Should()
                    .ContainSingle();
            });
        });
    }

    [Fact]
    public void OnDetachedFromVisualTree_WhenSceneHasOverlay_ClearsTemporaryOverlayAndScene()
    {
        Dispatch(() =>
        {
            ShowCollection((items, control, window) =>
            {
                Canvas galleryPanel = GetGalleryPanel(control);
                Canvas overlayCanvas = GetOverlayCanvas(control);
                overlayCanvas.Children.Add(new Border());

                window.Content = null;
                window.CaptureRenderedFrame();
                items.Add(CreateItem());

                overlayCanvas.Children.Should().BeEmpty();
                galleryPanel.Children.Should().BeEmpty();

                window.Content = control;
                window.CaptureRenderedFrame();
                items.Add(CreateItem());
                window.CaptureRenderedFrame();

                galleryPanel.Children
                    .OfType<GenerationCardControl>()
                    .Should()
                    .HaveCount(3);
            });
        });
    }

    private static ObservableCollection<GenerationItemViewModel> CreateItems()
    {
        ObservableCollection<GenerationItemViewModel> items = [CreateItem()];

        return items;
    }

    private static AnimatedGalleryControl CreateControl(
        ObservableCollection<GenerationItemViewModel> items)
    {
        return new AnimatedGalleryControl(CreateSceneFactory())
        {
            Items = items
        };
    }

    private static void ShowCollection(
        Action<
            ObservableCollection<GenerationItemViewModel>,
            AnimatedGalleryControl,
            Window> action)
    {
        ShowCollection(null, action);
    }

    private static void ShowCollection(
        RecordingGalleryOperations? operations,
        Action<
            ObservableCollection<GenerationItemViewModel>,
            AnimatedGalleryControl,
            Window> action)
    {
        ObservableCollection<GenerationItemViewModel> items = CreateItems();
        AnimatedGalleryControl control = CreateControl(items);
        control.Operations = operations;

        Show(control, window => action(items, control, window));
    }
}
