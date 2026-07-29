using Avalonia.Controls;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services.UiAnimation;

public sealed class AvaloniaUiFrameSchedulerTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void RequestAnimationFrame_WhenRequestedWhileHidden_SubmitsAfterWindowIsShown()
    {
        Dispatch(() =>
        {
            Window window = Show(new Border());
            List<Action<TimeSpan>> submittedFrames = [];
            int completedFrameCount = 0;

            try
            {
                window.Hide();
                AvaloniaUiFrameScheduler scheduler = new(
                    window,
                    submittedFrames.Add);

                scheduler.RequestAnimationFrame(_ => completedFrameCount++);

                submittedFrames.Should().BeEmpty();

                window.Show();

                submittedFrames.Should().ContainSingle();
                submittedFrames[0](TimeSpan.FromMilliseconds(16));
                completedFrameCount.Should().Be(1);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RequestAnimationFrame_WhenWindowIsHiddenBeforeFrame_IgnoresStaleFrame()
    {
        Dispatch(() =>
        {
            Window window = Show(new Border());
            List<Action<TimeSpan>> submittedFrames = [];
            int completedFrameCount = 0;

            try
            {
                AvaloniaUiFrameScheduler scheduler = new(
                    window,
                    submittedFrames.Add);
                scheduler.RequestAnimationFrame(_ => completedFrameCount++);

                window.Hide();
                window.Show();

                submittedFrames.Should().HaveCount(2);

                submittedFrames[0](TimeSpan.FromMilliseconds(16));
                completedFrameCount.Should().Be(0);

                submittedFrames[1](TimeSpan.FromMilliseconds(32));
                completedFrameCount.Should().Be(1);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RequestAnimationFrame_WhenSeveralFramesArePending_TracksEachFrameIndependently()
    {
        Dispatch(() =>
        {
            Window window = Show(new Border());
            List<Action<TimeSpan>> submittedFrames = [];
            int completedFrameCount = 0;

            try
            {
                AvaloniaUiFrameScheduler scheduler = new(
                    window,
                    submittedFrames.Add);
                scheduler.RequestAnimationFrame(_ => completedFrameCount++);
                scheduler.RequestAnimationFrame(_ => completedFrameCount++);

                submittedFrames[0](TimeSpan.FromMilliseconds(16));
                window.Hide();
                window.Show();

                submittedFrames.Should().HaveCount(3);
                submittedFrames[1](TimeSpan.FromMilliseconds(32));
                completedFrameCount.Should().Be(1);

                submittedFrames[2](TimeSpan.FromMilliseconds(48));
                completedFrameCount.Should().Be(2);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RequestAnimationFrame_AfterLastFrameCompletes_DetachesPresentationObserver()
    {
        Dispatch(() =>
        {
            Window window = Show(new Border());
            List<Action<TimeSpan>> submittedFrames = [];

            try
            {
                AvaloniaUiFrameScheduler scheduler = new(
                    window,
                    submittedFrames.Add);
                scheduler.RequestAnimationFrame(_ => { });
                submittedFrames[0](TimeSpan.FromMilliseconds(16));

                window.Hide();
                window.Show();

                submittedFrames.Should().ContainSingle();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
