using Avalonia;
using Avalonia.Controls;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Services.Windowing;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Windowing;

public sealed class WindowPlacementTrackerTests :
    AnimatedGalleryControlTestBase
{
    [Fact]
    public void Attach_WithSavedNormalPlacement_RestoresBeforeWindowIsShown()
    {
        Dispatch(() =>
        {
            WindowPlacementState savedState = new()
            {
                X = 100,
                Y = 120,
                Width = 800d,
                Height = 600d
            };
            RecordingStateWriteScheduler scheduler = new();
            using WindowPlacementTracker tracker = CreateTracker(
                savedState,
                scheduler);
            Window window = CreateWindow();

            try
            {
                tracker.Attach(window);

                window.WindowStartupLocation.Should()
                    .Be(WindowStartupLocation.Manual);
                window.Position.Should().Be(new PixelPoint(100, 120));
                window.Width.Should().Be(800d);
                window.Height.Should().Be(600d);
                scheduler.CallCount.Should().Be(0);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Attach_WithUnavailableSavedScreen_CentersWindow()
    {
        Dispatch(() =>
        {
            WindowPlacementState savedState = new()
            {
                X = 100_000,
                Y = 100_000,
                Width = 800d,
                Height = 600d
            };
            using WindowPlacementTracker tracker = CreateTracker(
                savedState,
                new RecordingStateWriteScheduler());
            Window window = CreateWindow();

            try
            {
                tracker.Attach(window);

                window.WindowStartupLocation.Should()
                    .Be(WindowStartupLocation.CenterScreen);
                window.Position.Should().NotBe(
                    new PixelPoint(100_000, 100_000));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Attach_WithSavedMaximizedState_RestoresMaximizedState()
    {
        Dispatch(() =>
        {
            WindowPlacementState savedState = new()
            {
                X = 100,
                Y = 120,
                Width = 800d,
                Height = 600d,
                IsMaximized = true
            };
            using WindowPlacementTracker tracker = CreateTracker(
                savedState,
                new RecordingStateWriteScheduler());
            Window window = CreateWindow();

            try
            {
                tracker.Attach(window);

                window.WindowState.Should().Be(WindowState.Maximized);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Attach_WhenNormalWindowMoves_SchedulesNormalPlacement()
    {
        Dispatch(() =>
        {
            RecordingStateWriteScheduler scheduler = new();
            using WindowPlacementTracker tracker = CreateTracker(
                new WindowPlacementState(),
                scheduler);
            Window window = Show(new Border(), 800d, 600d);

            try
            {
                tracker.Attach(window);
                window.Position = new PixelPoint(140, 160);

                WindowPlacementState scheduledState = scheduler.SavedState
                    .Should()
                    .BeOfType<WindowPlacementState>()
                    .Subject;
                scheduledState.X.Should().Be(140);
                scheduledState.Y.Should().Be(160);
                scheduledState.Width.Should().BeGreaterThan(0d);
                scheduledState.Height.Should().BeGreaterThan(0d);
                scheduledState.IsMaximized.Should().BeFalse();
                scheduler.SavedSection.Should()
                    .BeOfType<WindowPlacementStateSection>();
                scheduler.SavedMode.Should().Be(StateWriteMode.Deferred);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Attach_WhenWindowIsMaximized_PreservesNormalPlacement()
    {
        Dispatch(() =>
        {
            RecordingStateWriteScheduler scheduler = new();
            using WindowPlacementTracker tracker = CreateTracker(
                new WindowPlacementState(),
                scheduler);
            Window window = Show(new Border(), 800d, 600d);

            try
            {
                tracker.Attach(window);
                window.Position = new PixelPoint(140, 160);
                window.WindowState = WindowState.Maximized;

                WindowPlacementState scheduledState = scheduler.SavedState
                    .Should()
                    .BeOfType<WindowPlacementState>()
                    .Subject;
                scheduledState.X.Should().Be(140);
                scheduledState.Y.Should().Be(160);
                scheduledState.Width.Should().BeGreaterThan(0d);
                scheduledState.Height.Should().BeGreaterThan(0d);
                scheduledState.IsMaximized.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Attach_WhenWindowIsMinimized_DoesNotPersistMinimizedState()
    {
        Dispatch(() =>
        {
            RecordingStateWriteScheduler scheduler = new();
            using WindowPlacementTracker tracker = CreateTracker(
                new WindowPlacementState(),
                scheduler);
            Window window = Show(new Border(), 800d, 600d);

            try
            {
                tracker.Attach(window);
                window.Position = new PixelPoint(140, 160);
                int callCountBeforeMinimize = scheduler.CallCount;

                window.WindowState = WindowState.Minimized;

                scheduler.CallCount.Should().Be(callCountBeforeMinimize);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static WindowPlacementTracker CreateTracker(
        WindowPlacementState savedState,
        RecordingStateWriteScheduler scheduler)
    {
        return new WindowPlacementTracker(
            new StubAppStateStore(savedState),
            scheduler,
            new WindowPlacementStateSection(),
            NullLogger<WindowPlacementTracker>.Instance);
    }

    private static Window CreateWindow()
    {
        return new Window
        {
            Width = 640d,
            Height = 480d,
            MinWidth = 320d,
            MinHeight = 240d,
            Content = new Border()
        };
    }
}
