using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Overlays;

public sealed class ModalOverlayPresenterControlTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void Backdrop_WhenRendered_CoversPresenterWithSquareDimmedSurface()
    {
        Dispatch(() =>
        {
            ModalOverlayPresenterControl overlay = new()
            {
                Body = new Border(),
                IsOpen = true
            };
            Window window = Show(overlay);

            try
            {
                Button backdrop = overlay.FindControl<Button>("Backdrop")
                    ?? throw new InvalidOperationException("Modal backdrop was not found.");
                Border dimmingSurface = backdrop
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Single(control => control.Name == "PART_DimmingSurface");
                ISolidColorBrush dimmingBrush = dimmingSurface.Background as ISolidColorBrush
                    ?? throw new InvalidOperationException("Modal dimming brush was not resolved.");

                backdrop.CornerRadius.Should().Be(new CornerRadius());
                backdrop.Bounds.Size.Should().Be(overlay.Bounds.Size);
                dimmingBrush.Color.A.Should().BeGreaterThan((byte)0);
                dimmingSurface.CornerRadius.Should().Be(new CornerRadius());
                dimmingSurface.Opacity.Should().Be(0.72d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task IsOpen_WhenClosed_HidesAfterAnimationCompletes()
    {
        await DispatchAsync(async () =>
        {
            TestUiFrameScheduler frameScheduler = new();
            ModalOverlayPresenterControl overlay = new(frameScheduler)
            {
                Body = new Border(),
                IsOpen = true
            };
            Window window = Show(overlay);

            try
            {
                await CompleteAnimationAsync(frameScheduler, TimeSpan.Zero);

                overlay.IsOpen = false;

                overlay.IsVisible.Should().BeTrue();
                await CompleteAnimationAsync(frameScheduler, TimeSpan.FromMilliseconds(200d));
                overlay.IsVisible.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task IsOpen_WhenClosing_FadesRenderedPanelSnapshot()
    {
        await DispatchAsync(async () =>
        {
            TestUiFrameScheduler frameScheduler = new();
            ModalOverlayPresenterControl overlay = CreateBlurredOverlay(frameScheduler);
            Window window = Show(overlay);

            try
            {
                await CompleteAnimationAsync(frameScheduler, TimeSpan.Zero);
                ContentControl bodyPresenter = overlay.FindControl<ContentControl>("BodyPresenter")
                    ?? throw new InvalidOperationException("Modal body presenter was not found.");
                Border snapshotHost = overlay.FindControl<Border>("BodyTransitionSnapshotHost")
                    ?? throw new InvalidOperationException("Modal transition snapshot host was not found.");
                Image snapshot = overlay.FindControl<Image>("BodyTransitionSnapshot")
                    ?? throw new InvalidOperationException("Modal transition snapshot was not found.");
                ModalOverlayControl panel = overlay
                    .GetVisualDescendants()
                    .OfType<ModalOverlayControl>()
                    .Single();
                Point panelPosition = panel.TranslatePoint(new Point(), overlay)
                    ?? throw new InvalidOperationException("Modal panel position was not resolved.");

                overlay.IsOpen = false;

                bodyPresenter.IsVisible.Should().BeFalse();
                snapshotHost.IsVisible.Should().BeTrue();
                snapshot.Source.Should().NotBeNull();
                snapshotHost.Width.Should().BeApproximately(panel.Bounds.Width, 0.001d);
                snapshotHost.Height.Should().BeApproximately(panel.Bounds.Height, 0.001d);
                snapshotHost.Margin.Left.Should().BeApproximately(
                    panelPosition.X,
                    0.001d);
                snapshotHost.Margin.Top.Should().BeApproximately(
                    panelPosition.Y,
                    0.001d);
                snapshotHost.CornerRadius.Should().Be(panel.CornerRadius);
                RunAnimationFrame(frameScheduler, TimeSpan.FromMilliseconds(300d));
                await RunAnimationFrameAsync(
                    frameScheduler,
                    TimeSpan.FromMilliseconds(380d));

                snapshotHost.Opacity.Should().BeGreaterThan(0d).And.BeLessThan(1d);
                await RunAnimationFrameAsync(
                    frameScheduler,
                    TimeSpan.FromMilliseconds(500d));

                overlay.IsVisible.Should().BeFalse();
                bodyPresenter.IsVisible.Should().BeTrue();
                snapshotHost.IsVisible.Should().BeFalse();
                snapshot.Source.Should().BeNull();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task IsOpen_WhenReopenedDuringClosing_RestoresLivePanel()
    {
        await DispatchAsync(async () =>
        {
            TestUiFrameScheduler frameScheduler = new();
            ModalOverlayPresenterControl overlay = CreateBlurredOverlay(frameScheduler);
            Window window = Show(overlay);

            try
            {
                await CompleteAnimationAsync(frameScheduler, TimeSpan.Zero);
                ContentControl bodyPresenter = overlay.FindControl<ContentControl>("BodyPresenter")
                    ?? throw new InvalidOperationException("Modal body presenter was not found.");
                Border snapshotHost = overlay.FindControl<Border>("BodyTransitionSnapshotHost")
                    ?? throw new InvalidOperationException("Modal transition snapshot host was not found.");
                Image snapshot = overlay.FindControl<Image>("BodyTransitionSnapshot")
                    ?? throw new InvalidOperationException("Modal transition snapshot was not found.");
                overlay.IsOpen = false;
                RunAnimationFrame(frameScheduler, TimeSpan.FromMilliseconds(300d));
                await RunAnimationFrameAsync(
                    frameScheduler,
                    TimeSpan.FromMilliseconds(380d));

                overlay.IsOpen = true;

                overlay.IsVisible.Should().BeTrue();
                bodyPresenter.IsVisible.Should().BeTrue();
                snapshotHost.IsVisible.Should().BeFalse();
                snapshot.Source.Should().BeNull();
                await CompleteAnimationAsync(
                    frameScheduler,
                    TimeSpan.FromMilliseconds(400d));

                bodyPresenter.Opacity.Should().Be(1d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Order_WhenChanged_UpdatesVisualOrder()
    {
        Dispatch(() =>
        {
            const int ExpectedOrder = 250;
            ModalOverlayPresenterControl overlay = new();

            overlay.Order = ExpectedOrder;

            overlay.ZIndex.Should().Be(ExpectedOrder);
        });
    }

    private static ModalOverlayPresenterControl CreateBlurredOverlay(
        TestUiFrameScheduler frameScheduler)
    {
        return new ModalOverlayPresenterControl(frameScheduler)
        {
            Body = new ModalOverlayControl
            {
                Width = 320d,
                Height = 260d,
                Body = new Border
                {
                    Background = Brushes.CornflowerBlue
                }
            },
            IsOpen = true
        };
    }

    private static async Task CompleteAnimationAsync(
        TestUiFrameScheduler frameScheduler,
        TimeSpan startTime)
    {
        frameScheduler.RunNextFrame(startTime);
        await frameScheduler.RunNextFrameAsync(startTime + TimeSpan.FromMilliseconds(200d));
        await Task.Yield();
    }

    private static void RunAnimationFrame(
        TestUiFrameScheduler frameScheduler,
        TimeSpan time)
    {
        frameScheduler.RunNextFrame(time);
    }

    private static async Task RunAnimationFrameAsync(
        TestUiFrameScheduler frameScheduler,
        TimeSpan time)
    {
        await frameScheduler.RunNextFrameAsync(time);
        await Task.Yield();
    }
}
