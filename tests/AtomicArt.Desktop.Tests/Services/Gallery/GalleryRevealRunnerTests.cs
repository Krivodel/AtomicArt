using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryRevealRunnerTests : AnimatedGalleryControlTestBase
{
    private const double CardSurfaceWidth = 220d;
    private const double CardSurfaceHeight = 322d;
    private const double CardCellMargin = 16d;
    private const double CardSurfaceCornerRadius = 8d;
    private const double HighlightBorderThickness = 2d;
    private const double FirstPulseExpandedScale = 1.05d;
    private const double SecondPulseExpandedScale = 1.10d;
    private const int FirstPulsePeakMilliseconds = 135;
    private const int FirstPulseEndMilliseconds = 270;
    private const int SecondPulsePeakMilliseconds = 480;
    private const int SecondPulseFadeMilliseconds = 690;
    private const int AnimationEndMilliseconds = 900;

    private static readonly Guid RevealedItemId =
        Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task RunAsync_WithExistingItem_ScrollsToItemAndStartsHighlight()
    {
        await DispatchAsync(async () =>
        {
            using RevealTestContext context = CreateContext();

            await context.RunAsync(RevealedItemId, CancellationToken.None);

            context.Coordinator.ScrollViewer.Offset.Y.Should().BeGreaterThan(0d);
            context.Coordinator.CardControls.Should().ContainKey(RevealedItemId);
            context.Coordinator.OverlayCanvas.Children.Should().ContainSingle();
        });
    }

    [Fact]
    public async Task RunAsync_WithCardCellMargin_AlignsHighlightWithVisibleCard()
    {
        await DispatchAsync(async () =>
        {
            using RevealTestContext context = CreateContext();

            await context.RunAsync(RevealedItemId, CancellationToken.None);

            Border highlight = GetHighlight(context.Coordinator);
            Rect expectedRect = GetCardSurfaceRect(context.Coordinator);
            Canvas.GetLeft(highlight).Should().BeApproximately(expectedRect.Left, 0.01d);
            Canvas.GetTop(highlight).Should().BeApproximately(expectedRect.Top, 0.01d);
            highlight.Width.Should().BeApproximately(expectedRect.Width, 0.01d);
            highlight.Height.Should().BeApproximately(expectedRect.Height, 0.01d);
            highlight.CornerRadius.Should().Be(
                new CornerRadius(CardSurfaceCornerRadius));
        });
    }

    [Fact]
    public async Task RunAsync_WithExistingItem_UsesFastThenWideLongPulseAndRemovesHighlight()
    {
        await DispatchAsync(async () =>
        {
            using RevealTestContext context = CreateContext();
            await context.RunAsync(RevealedItemId, CancellationToken.None);
            Border highlight = GetHighlight(context.Coordinator);

            context.FrameScheduler.RunNextFrame(TimeSpan.Zero);
            context.FrameScheduler.RunNextFrame(
                TimeSpan.FromMilliseconds(FirstPulsePeakMilliseconds));
            double firstPulseOpacity = highlight.Opacity;
            context.FrameScheduler.RunNextFrame(
                TimeSpan.FromMilliseconds(FirstPulseEndMilliseconds));
            await TestUiFrameScheduler.RunQueuedContinuationsAsync();
            context.FrameScheduler.RunNextFrame(
                TimeSpan.FromMilliseconds(FirstPulseEndMilliseconds));
            context.FrameScheduler.RunNextFrame(
                TimeSpan.FromMilliseconds(SecondPulsePeakMilliseconds));
            double secondPulseOpacity = highlight.Opacity;
            context.FrameScheduler.RunNextFrame(
                TimeSpan.FromMilliseconds(SecondPulseFadeMilliseconds));
            double secondPulseFadeOpacity = highlight.Opacity;
            TransformGroup transformGroup = highlight
                .RenderTransform
                .Should()
                .BeOfType<TransformGroup>()
                .Subject;
            ScaleTransform scale = transformGroup.Children
                .OfType<ScaleTransform>()
                .Single();

            firstPulseOpacity.Should().BeGreaterThan(0.9d);
            secondPulseOpacity.Should().BeGreaterThan(0.9d);
            secondPulseFadeOpacity.Should().BeGreaterThan(0d);
            secondPulseFadeOpacity.Should().BeLessThan(secondPulseOpacity);
            scale.ScaleX.Should().Be(FirstPulseExpandedScale);
            scale.ScaleY.Should().Be(FirstPulseExpandedScale);

            context.FrameScheduler.RunNextFrame(
                TimeSpan.FromMilliseconds(AnimationEndMilliseconds));

            scale.ScaleX.Should().Be(SecondPulseExpandedScale);
            scale.ScaleY.Should().Be(SecondPulseExpandedScale);
            context.Coordinator.OverlayCanvas.Children.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task RunAsync_WithExistingItem_UsesGreenHighlightPalette()
    {
        const string ExpectedBorderColor = "#F273F2A7";
        const string ExpectedBackgroundColor = "#2434D399";
        const string ExpectedShadowColor = "#22C55E";

        await DispatchAsync(async () =>
        {
            using RevealTestContext context = CreateContext();

            await context.RunAsync(RevealedItemId, CancellationToken.None);

            Border highlight = GetHighlight(context.Coordinator);
            ISolidColorBrush borderBrush = highlight
                .BorderBrush
                .Should()
                .BeAssignableTo<ISolidColorBrush>()
                .Subject;
            ISolidColorBrush backgroundBrush = highlight
                .Background
                .Should()
                .BeAssignableTo<ISolidColorBrush>()
                .Subject;
            DropShadowEffect shadow = highlight
                .Effect
                .Should()
                .BeOfType<DropShadowEffect>()
                .Subject;
            borderBrush.Color.Should().Be(Color.Parse(ExpectedBorderColor));
            backgroundBrush.Color.Should().Be(Color.Parse(ExpectedBackgroundColor));
            shadow.Color.Should().Be(Color.Parse(ExpectedShadowColor));
        });
    }

    [Fact]
    public async Task RunAsync_WithMissingItem_CompletesWithoutScrollingOrHighlight()
    {
        await DispatchAsync(async () =>
        {
            using RevealTestContext context = CreateContext();
            Guid missingItemId =
                Guid.Parse("99999999-9999-9999-9999-999999999999");

            await context.RunAsync(missingItemId, CancellationToken.None);

            context.Coordinator.ScrollViewer.Offset.Y.Should().Be(0d);
            context.Coordinator.OverlayCanvas.Children.Should().BeEmpty();
        });
    }

    private static RevealTestContext CreateContext()
    {
        TestUiFrameScheduler frameScheduler = new();
        UiAnimationScheduler animationScheduler = new(frameScheduler);
        GalleryLayoutService layout = new();
        GalleryRevealRunner runner = new(
            layout,
            new GalleryOverlayEffects(animationScheduler),
            NullLogger<GalleryRevealRunner>.Instance);
        List<object> items =
        [
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            RevealedItemId
        ];
        GalleryOperationRunnerRegistry registry = new(
            new List<IGalleryOperationRunner>());
        GalleryOperationCoordinator coordinator =
            GalleryOperationCoordinatorTestFactory.Create(
                frameScheduler,
                registry);
        ScrollViewer scrollViewer = new();
        Canvas galleryPanel = new();
        Canvas overlayCanvas = new()
        {
            IsHitTestVisible = false
        };
        coordinator.AttachScene(
            scrollViewer,
            galleryPanel,
            overlayCanvas,
            items,
            item => (Guid)item,
            CreateCardControl,
            () => Task.CompletedTask);
        scrollViewer.Content = galleryPanel;
        Grid root = new();
        root.Children.Add(scrollViewer);
        root.Children.Add(overlayCanvas);
        Window window = Show(root, 560d, 640d);
        layout.RenderCards(coordinator);
        window.CaptureRenderedFrame();

        return new RevealTestContext(
            window,
            frameScheduler,
            runner,
            coordinator);
    }

    private static Control CreateCardControl(object item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ContentControl
        {
            Content = new Border
            {
                Width = CardSurfaceWidth,
                Height = CardSurfaceHeight,
                CornerRadius = new CornerRadius(CardSurfaceCornerRadius),
                Margin = new Thickness(0d, 0d, CardCellMargin, CardCellMargin)
            }
        };
    }

    private static Rect GetCardSurfaceRect(GalleryOperationCoordinator coordinator)
    {
        Control card = coordinator.CardControls[RevealedItemId];
        Control surface = card is ContentControl { Content: Control content }
            ? content
            : throw new InvalidOperationException("The card surface was not found.");
        Matrix transform = surface.TransformToVisual(coordinator.OverlayCanvas)
            ?? throw new InvalidOperationException("The card transform was not found.");

        return new Rect(surface.Bounds.Size).TransformToAABB(transform);
    }

    private static Border GetHighlight(GalleryOperationCoordinator coordinator)
    {
        return coordinator
            .OverlayCanvas
            .Children
            .OfType<Border>()
            .Single(border =>
                border.BorderThickness == new Thickness(HighlightBorderThickness));
    }

    private sealed class RevealTestContext : IDisposable
    {
        public TestUiFrameScheduler FrameScheduler { get; }
        public GalleryRevealRunner Runner { get; }
        public GalleryOperationCoordinator Coordinator { get; }

        private readonly Window _window;

        public RevealTestContext(
            Window window,
            TestUiFrameScheduler frameScheduler,
            GalleryRevealRunner runner,
            GalleryOperationCoordinator coordinator)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            FrameScheduler = frameScheduler
                ?? throw new ArgumentNullException(nameof(frameScheduler));
            Runner = runner;
            Coordinator = coordinator;
        }

        public Task RunAsync(Guid itemId, CancellationToken ct)
        {
            GalleryOperation operation = new RevealGalleryItemOperation(itemId);

            return Runner.RunAsync(
                new List<GalleryOperation> { operation },
                Coordinator,
                ct);
        }

        public void Dispose()
        {
            _window.Close();
        }
    }
}
