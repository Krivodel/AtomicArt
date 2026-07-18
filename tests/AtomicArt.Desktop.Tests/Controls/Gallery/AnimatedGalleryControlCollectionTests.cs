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
            ObservableCollection<GenerationItemViewModel> items = [CreateItem()];
            AnimatedGalleryControl control = new(CreateSceneFactory())
            {
                Items = items
            };
            Window window = Show(control);

            try
            {
                items.Add(CreateItem());
                window.CaptureRenderedFrame();

                GetGalleryPanel(control)
                    .Children
                    .OfType<GenerationCardControl>()
                    .Should()
                    .HaveCount(2);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CollectionChanged_WhenOperationsProvided_DoesNotRefreshVisibleCardsDirectly()
    {
        Dispatch(() =>
        {
            ObservableCollection<GenerationItemViewModel> items = [CreateItem()];
            RecordingGalleryOperations operations = new();
            AnimatedGalleryControl control = new(CreateSceneFactory())
            {
                Items = items,
                Operations = operations
            };
            Window window = Show(control);

            try
            {
                items.Add(CreateItem());
                window.CaptureRenderedFrame();

                GetGalleryPanel(control)
                    .Children
                    .OfType<GenerationCardControl>()
                    .Should()
                    .ContainSingle();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void OnDetachedFromVisualTree_WhenSceneHasOverlay_ClearsTemporaryOverlayAndScene()
    {
        Dispatch(() =>
        {
            ObservableCollection<GenerationItemViewModel> items = [CreateItem()];
            AnimatedGalleryControl control = new(CreateSceneFactory())
            {
                Items = items
            };
            Window window = Show(control);

            try
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
            }
            finally
            {
                window.Close();
            }
        });
    }
}
