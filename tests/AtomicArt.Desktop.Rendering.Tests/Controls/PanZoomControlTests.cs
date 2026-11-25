using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;
using AtomicArt.Desktop.Tests;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Rendering.Tests.Controls;

public sealed class PanZoomControlTests : DesktopControlTestBase
{
    public new static AppBuilder BuildAvaloniaApp()
    {
        return GenerationChatViewModelTests.BuildAvaloniaApp();
    }

    [Fact]
    public async Task ZoomInCommand_OnTheFirstFrame_ChangesTheVisibleScale()
    {
        await DispatchAsync(typeof(PanZoomControlTests), () =>
        {
            TestUiFrameScheduler frames = new();
            PanZoomControl viewport = new()
            {
                FrameScheduler = frames,
                Clock = frames,
                Child = new Border { Width = 1800d, Height = 1200d }
            };
            Window window = Show(viewport, 900d, 600d);

            try
            {
                double initial = viewport.Zoom;

                viewport.ZoomInCommand.Execute(null);
                frames.RunFrame(TimeSpan.FromMilliseconds(8d));

                viewport.Zoom.Should().BeGreaterThan(initial).And.BeLessThan(initial * 1.2d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ZoomInCommand_WithInputBeforeEachFrame_KeepsMovingAndQueuesOneFrame()
    {
        await DispatchAsync(typeof(PanZoomControlTests), () =>
        {
            TestUiFrameScheduler frames = new();
            PanZoomControl viewport = new()
            {
                FrameScheduler = frames,
                Clock = frames,
                Child = new Border { Width = 1800d, Height = 1200d }
            };
            Window window = Show(viewport, 900d, 600d);

            try
            {
                int requested = frames.RequestedFrameCount;
                viewport.ZoomInCommand.Execute(null);
                frames.RunFrame(TimeSpan.Zero);

                for (int frame = 1; frame <= 8; frame++)
                {
                    double previous = viewport.Zoom;
                    viewport.ZoomInCommand.Execute(null);
                    frames.RunFrame(TimeSpan.FromMilliseconds(frame * 8d));

                    viewport.Zoom.Should().BeGreaterThan(previous);
                    frames.RequestedFrameCount.Should().Be(requested + frame + 2);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ZoomOutCommand_AfterSeveralQueuedZoomIns_ReversesOnTheNextFrame()
    {
        await DispatchAsync(typeof(PanZoomControlTests), () =>
        {
            TestUiFrameScheduler frames = new();
            PanZoomControl viewport = new()
            {
                FrameScheduler = frames,
                Clock = frames,
                Child = new Border { Width = 1800d, Height = 1200d }
            };
            Window window = Show(viewport, 900d, 600d);

            try
            {
                viewport.ZoomInCommand.Execute(null);
                frames.RunFrame(TimeSpan.Zero);
                frames.RunFrame(TimeSpan.FromMilliseconds(8d));

                for (int input = 0; input < 4; input++)
                {
                    viewport.ZoomInCommand.Execute(null);
                }

                double previous = viewport.Zoom;
                viewport.ZoomOutCommand.Execute(null);
                frames.RunFrame(TimeSpan.FromMilliseconds(16d));

                viewport.Zoom.Should().BeLessThan(previous);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Pan_FromACard_CancelsItsClickAndKeepsNormalClicksWorking()
    {
        await DispatchAsync(typeof(PanZoomControlTests), () =>
        {
            TestUiFrameScheduler frames = new();
            int clicks = 0;
            Button card = new()
            {
                Width = 220d,
                Height = 160d,
                Content = "A card",
                Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => clicks++)
            };
            Canvas content = new() { Width = 1200d, Height = 800d };
            content.Children.Add(card);
            Canvas.SetLeft(card, 100d);
            Canvas.SetTop(card, 100d);
            PanZoomControl viewport = new()
            {
                FrameScheduler = frames,
                Clock = frames,
                Child = content,
                Background = Brushes.Transparent
            };
            Window window = DesktopControlTestBase.Show(viewport, 900d, 600d);

            try
            {
                Point start = card.TranslatePoint(new Point(100d, 80d), window)
                    ?? throw new InvalidOperationException("The card has no position.");
                Matrix initial = content.RenderTransform?.Value ?? throw new InvalidOperationException("The camera is unavailable.");
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(start + new Vector(100d, 40d));
                window.MouseUp(start + new Vector(100d, 40d), MouseButton.Left);

                for (int frame = 0; frame < 30; frame++)
                {
                    frames.RunFrame(TimeSpan.FromMilliseconds(frame * 16d));
                }

                clicks.Should().Be(0);
                Matrix final = content.RenderTransform?.Value ?? throw new InvalidOperationException("The camera is unavailable.");
                final.M31.Should().BeApproximately(initial.M31 + 100d, 0.001d);
                final.M32.Should().BeApproximately(initial.M32 + 40d, 0.001d);
                Point click = card.TranslatePoint(new Point(100d, 80d), window)
                    ?? throw new InvalidOperationException("The moved card has no position.");
                window.MouseDown(click, MouseButton.Left);
                window.MouseUp(click, MouseButton.Left);

                clicks.Should().Be(1);
                viewport.IsInteracting.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ZoomInCommand_DuringAnimation_PreservesTheAnchorAndMovesAcrossFrames()
    {
        await DispatchAsync(typeof(PanZoomControlTests), () =>
        {
            TestUiFrameScheduler frames = new();
            Border content = new() { Width = 1800d, Height = 1200d, Background = Brushes.CornflowerBlue };
            PanZoomControl viewport = new()
            {
                FrameScheduler = frames,
                Clock = frames,
                Child = content,
                Background = Brushes.Transparent
            };
            Window window = DesktopControlTestBase.Show(viewport, 900d, 600d);

            try
            {
                double initial = viewport.Zoom;
                Matrix before = content.RenderTransform?.Value ?? throw new InvalidOperationException("The camera transform is unavailable.");
                Point anchor = new(viewport.Bounds.Width / 2d, viewport.Bounds.Height / 2d);
                Point world = new((anchor.X - before.M31) / initial, (anchor.Y - before.M32) / initial);

                viewport.ZoomInCommand.Execute(null);
                viewport.Zoom.Should().Be(initial);
                frames.RunFrame(TimeSpan.Zero);
                frames.RunFrame(TimeSpan.FromMilliseconds(70d));

                viewport.Zoom.Should().BeGreaterThan(initial).And.BeLessThan(initial * 1.2d);
                Matrix middle = content.RenderTransform?.Value ?? throw new InvalidOperationException("The camera transform is unavailable.");
                Point mapped = world.Transform(middle);
                mapped.X.Should().BeApproximately(anchor.X, 0.001d);
                mapped.Y.Should().BeApproximately(anchor.Y, 0.001d);
                Point visibleOrigin = viewport.VisibleContentBounds.TopLeft.Transform(middle);
                Point visibleEnd = viewport.VisibleContentBounds.BottomRight.Transform(middle);
                visibleOrigin.X.Should().BeApproximately(0d, 0.001d);
                visibleOrigin.Y.Should().BeApproximately(0d, 0.001d);
                visibleEnd.X.Should().BeApproximately(viewport.Bounds.Width, 0.001d);
                visibleEnd.Y.Should().BeApproximately(viewport.Bounds.Height, 0.001d);

                frames.RunFrame(TimeSpan.FromMilliseconds(140d));
                frames.RunFrame(TimeSpan.FromMilliseconds(200d));

                viewport.Zoom.Should().BeApproximately(initial * 1.2d, 0.001d);
                viewport.IsInteracting.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Pan_WithManyPointerEvents_FollowsTheLatestPointerInOneFrame()
    {
        await DispatchAsync(typeof(PanZoomControlTests), () =>
        {
            TestUiFrameScheduler frames = new();
            Border content = new() { Width = 1800d, Height = 1200d, Background = Brushes.CornflowerBlue };
            PanZoomControl viewport = new()
            {
                FrameScheduler = frames,
                Clock = frames,
                Child = content,
                Background = Brushes.Transparent
            };
            Window window = DesktopControlTestBase.Show(viewport, 900d, 600d);

            try
            {
                Point start = new(20d, 20d);
                Matrix initial = content.RenderTransform?.Value ?? throw new InvalidOperationException("The camera transform is unavailable.");
                int requested = frames.RequestedFrameCount;

                window.MouseDown(start, MouseButton.Left);

                for (int index = 1; index <= 100; index++)
                {
                    window.MouseMove(start + new Vector(index, index * 0.4d));
                }

                frames.RequestedFrameCount.Should().Be(requested + 1);
                content.RenderTransform?.Value.Should().Be(initial);
                frames.RunFrame(TimeSpan.FromMilliseconds(8d));
                Matrix middle = content.RenderTransform?.Value ?? throw new InvalidOperationException("The camera transform is unavailable.");
                middle.M31.Should().BeApproximately(initial.M31 + 100d, 0.001d);
                middle.M32.Should().BeApproximately(initial.M32 + 40d, 0.001d);
                window.MouseUp(start + new Vector(100d, 40d), MouseButton.Left);
                content.RenderTransform?.Value.Should().Be(middle);

                for (int index = 1; index <= 30; index++)
                {
                    frames.RunFrame(TimeSpan.FromMilliseconds(index * 16d));
                }

                Matrix final = content.RenderTransform?.Value ?? throw new InvalidOperationException("The camera transform is unavailable.");
                final.M31.Should().BeApproximately(initial.M31 + 100d, 0.001d);
                final.M32.Should().BeApproximately(initial.M32 + 40d, 0.001d);
                viewport.IsInteracting.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ZoomInCommand_WhenInterrupted_ContinuesFromTheVisibleCamera()
    {
        await DispatchAsync(typeof(PanZoomControlTests), () =>
        {
            TestUiFrameScheduler frames = new();
            PanZoomControl viewport = new()
            {
                FrameScheduler = frames,
                Clock = frames,
                Child = new Border { Width = 1800d, Height = 1200d, Background = Brushes.CornflowerBlue }
            };
            Window window = DesktopControlTestBase.Show(viewport, 900d, 600d);

            try
            {
                viewport.ZoomInCommand.Execute(null);
                frames.RunFrame(TimeSpan.Zero);
                frames.RunFrame(TimeSpan.FromMilliseconds(70d));
                double visible = viewport.Zoom;

                viewport.ZoomOutCommand.Execute(null);

                viewport.Zoom.Should().Be(visible);
                frames.RunFrame(TimeSpan.FromMilliseconds(70d));
                frames.RunFrame(TimeSpan.FromMilliseconds(210d));
                frames.RunFrame(TimeSpan.FromMilliseconds(260d));
                frames.RunFrame(TimeSpan.FromMilliseconds(310d));
                viewport.Zoom.Should().BeApproximately(visible / 1.2d, 0.001d);
                viewport.IsInteracting.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ZoomInCommand_WithDifferentFrameIntervals_ReachesTheSameCameraPosition()
    {
        await DispatchAsync(typeof(PanZoomControlTests), () =>
        {
            Matrix[] positions = new int[] { 8, 16, 32 }.Select(CaptureCameraAfterZoom).ToArray();

            foreach (Matrix position in positions)
            {
                position.M11.Should().BeApproximately(positions[0].M11, 0.00001d);
                position.M31.Should().BeApproximately(positions[0].M31, 0.001d);
                position.M32.Should().BeApproximately(positions[0].M32, 0.001d);
            }
        });
    }

    [Fact]
    public async Task ZoomInCommand_AtTheMaximumScale_DoesNotQueueFrames()
    {
        await DispatchAsync(typeof(PanZoomControlTests), () =>
        {
            TestUiFrameScheduler frames = new();
            PanZoomControl viewport = new()
            {
                FrameScheduler = frames,
                Clock = frames,
                Child = new Border { Width = 1800d, Height = 1200d }
            };
            Window window = Show(viewport, 900d, 600d);

            try
            {
                for (int input = 0; input < 20; input++)
                {
                    viewport.ZoomInCommand.Execute(null);
                }

                for (int frame = 1; frame <= 40; frame++)
                {
                    frames.RunFrame(TimeSpan.FromMilliseconds(frame * 16d));
                }

                int requested = frames.RequestedFrameCount;
                double scale = viewport.Zoom;

                viewport.ZoomInCommand.Execute(null);

                frames.RequestedFrameCount.Should().Be(requested);
                viewport.Zoom.Should().Be(scale);
                viewport.IsInteracting.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Matrix CaptureCameraAfterZoom(int frameIntervalMilliseconds)
    {
        TestUiFrameScheduler frames = new();
        Border content = new() { Width = 1800d, Height = 1200d };
        PanZoomControl viewport = new() { FrameScheduler = frames, Clock = frames, Child = content };
        Window window = Show(viewport, 900d, 600d);

        try
        {
            viewport.ZoomInCommand.Execute(null);

            for (int elapsed = frameIntervalMilliseconds; elapsed <= 96; elapsed += frameIntervalMilliseconds)
            {
                frames.RunFrame(TimeSpan.FromMilliseconds(elapsed));
            }

            return content.RenderTransform?.Value ?? throw new InvalidOperationException("The camera is unavailable.");
        }
        finally
        {
            window.Close();
        }
    }
}
