using System.Collections.ObjectModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using FluentAssertions;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Gallery;

public abstract class AnimatedGalleryControlResizeTestBase : AnimatedGalleryControlTestBase
{
    private const int DefaultResizeItemCount = 6;

    private protected static void BeginWideResizeAnimation(ResizeScenario scenario)
    {
        scenario.Window.Width = 980d;
        scenario.Window.CaptureRenderedFrame();
    }

    private protected static void CompleteAnimation(TestUiFrameScheduler frameScheduler)
    {
        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(2400d));
    }

    private protected static void RunResizeScenario(
        Action<TestUiFrameScheduler, ResizeScenario> action)
    {
        RunScenario(ShowResizeScenario, scenario => scenario.Window, action);
    }

    private protected static void RunDetachScenario(
        Action<TestUiFrameScheduler, DetachScenario> action)
    {
        RunScenario(ShowDetachScenario, scenario => scenario.Window, action);
    }

    private static void RunScenario<TScenario>(
        Func<TestUiFrameScheduler, TScenario> showScenario,
        Func<TScenario, Window> getWindow,
        Action<TestUiFrameScheduler, TScenario> action)
    {
        ArgumentNullException.ThrowIfNull(showScenario);
        ArgumentNullException.ThrowIfNull(getWindow);
        ArgumentNullException.ThrowIfNull(action);

        TestUiFrameScheduler frameScheduler = new();
        TScenario scenario = showScenario(frameScheduler);

        try
        {
            action(frameScheduler, scenario);
        }
        finally
        {
            getWindow(scenario).Close();
        }
    }

    private protected static ResizeScenario ShowResizeScenario(TestUiFrameScheduler frameScheduler)
    {
        List<GenerationItemViewModel> items = CreateResizeItems();
        (AnimatedGalleryControl Control, Window Window) scene = ShowScene(items, frameScheduler);

        return new ResizeScenario(scene.Control, scene.Window, items);
    }

    private protected static DetachScenario ShowDetachScenario(TestUiFrameScheduler frameScheduler)
    {
        ObservableCollection<GenerationItemViewModel> items =
        [
            CreateItem(),
            CreateItem(),
            CreateItem()
        ];
        (AnimatedGalleryControl Control, Window Window) scene = ShowScene(items, frameScheduler);

        return new DetachScenario(scene.Control, scene.Window, items);
    }

    private static (AnimatedGalleryControl Control, Window Window) ShowScene(
        IEnumerable<GenerationItemViewModel> items,
        TestUiFrameScheduler frameScheduler)
    {
        AnimatedGalleryControl control = new(CreateSceneFactory(frameScheduler))
        {
            Items = items
        };
        Window window = Show(control, 560d, 640d);

        return (control, window);
    }

    private protected static void ChangeDetachedScene(DetachScenario scenario)
    {
        ScrollViewer scrollViewer = GetGalleryScrollViewer(scenario.Control);

        scrollViewer.SetCurrentValue(ScrollViewer.ViewportProperty, new Size(980d, 640d));
        scrollViewer.Arrange(new Rect(0d, 0d, 980d, 640d));
        scenario.Items.Add(CreateItem());
    }

    private protected static void AssertDetachedSceneIgnoredChanges(
        TestUiFrameScheduler frameScheduler,
        int requestedFrameCount,
        Canvas galleryPanel,
        Canvas overlayCanvas)
    {
        frameScheduler.RequestedFrameCount.Should().Be(requestedFrameCount);
        galleryPanel.Children.Should().BeEmpty();
        overlayCanvas.Children.Should().BeEmpty();
    }

    private protected static void AssertAllCardsRendered(ResizeScenario scenario)
    {
        GetGalleryPanel(scenario.Control)
            .Children
            .OfType<GenerationCardControl>()
            .Should()
            .HaveCount(scenario.Items.Count);
    }

    private protected static void AssertThirdCardMovedToWideLayout(GenerationCardControl thirdCard)
    {
        AssertThirdCardAtWideLayout(thirdCard);
        GetTranslateTransform(thirdCard).X.Should().Be(-GalleryLayoutService.CardWidth * 2d);
        GetTranslateTransform(thirdCard).Y.Should().Be(GalleryLayoutService.CardHeight);
    }

    private protected static void AssertThirdCardAtWideLayout(GenerationCardControl thirdCard)
    {
        Canvas.GetLeft(thirdCard).Should().Be(GalleryLayoutService.CardWidth * 2d);
        Canvas.GetTop(thirdCard).Should().Be(GalleryLayoutService.CardTopPadding);
    }

    private protected static void AssertThirdCardRetargetedToNarrowLayout(GenerationCardControl thirdCard)
    {
        AssertThirdCardAtNarrowLayout(thirdCard);
        GetTranslateTransform(thirdCard).X.Should().Be(GalleryLayoutService.CardWidth * 2d);
        GetTranslateTransform(thirdCard).Y.Should().Be(-GalleryLayoutService.CardHeight);
    }

    private protected static void AssertThirdCardAtNarrowLayout(GenerationCardControl thirdCard)
    {
        Canvas.GetLeft(thirdCard).Should().Be(0d);
        Canvas.GetTop(thirdCard).Should().Be(GalleryLayoutService.CardTopPadding + GalleryLayoutService.CardHeight);
    }

    private protected static void AssertIdentityTransform(GenerationCardControl thirdCard)
    {
        GetTranslateTransform(thirdCard).X.Should().Be(0d);
        GetTranslateTransform(thirdCard).Y.Should().Be(0d);
    }

    private static List<GenerationItemViewModel> CreateResizeItems()
    {
        return CreateResizeItems(DefaultResizeItemCount);
    }

    private static List<GenerationItemViewModel> CreateResizeItems(int count)
    {
        List<GenerationItemViewModel> items = [];

        for (int i = 0; i < count; i++)
        {
            items.Add(CreateItem());
        }

        return items;
    }

    private protected static GenerationCardControl GetThirdCard(
        AnimatedGalleryControl control,
        IReadOnlyList<GenerationItemViewModel> items)
    {
        return GetGalleryPanel(control)
            .Children
            .OfType<GenerationCardControl>()
            .Single(card => ReferenceEquals(card.DataContext, items[2]));
    }

    private protected sealed record ResizeScenario(
        AnimatedGalleryControl Control,
        Window Window,
        IReadOnlyList<GenerationItemViewModel> Items);

    private protected sealed record DetachScenario(
        AnimatedGalleryControl Control,
        Window Window,
        ObservableCollection<GenerationItemViewModel> Items);
}
