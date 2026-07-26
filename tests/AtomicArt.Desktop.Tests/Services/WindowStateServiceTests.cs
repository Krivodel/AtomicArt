using Avalonia.Controls;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Windowing;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class WindowStateServiceTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void Attach_WhenWindowIsHidden_TracksPresentationState()
    {
        Dispatch(() =>
        {
            using WindowStateService service = CreateService();
            Window window = Show(new Border());

            try
            {
                service.Attach(window);

                service.IsPresented.Should().BeTrue();

                window.Hide();

                service.IsPresented.Should().BeFalse();

                window.Show();

                service.IsPresented.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void WaitUntilPresentedAsync_WhenWindowIsMinimized_CompletesAfterRestore()
    {
        Dispatch(() =>
        {
            using WindowStateService service = CreateService();
            Window window = Show(new Border());

            try
            {
                service.Attach(window);
                window.WindowState = WindowState.Minimized;

                Task presentationTask = service.WaitUntilPresentedAsync(
                    CancellationToken.None);

                service.IsPresented.Should().BeFalse();
                presentationTask.IsCompleted.Should().BeFalse();

                window.WindowState = WindowState.Normal;

                service.IsPresented.Should().BeTrue();
                presentationTask.IsCompletedSuccessfully.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static WindowStateService CreateService()
    {
        WindowPlacementTracker placementTracker = new(
            new StubAppStateStore(new WindowPlacementState()),
            new RecordingStateWriteScheduler(),
            new WindowPlacementStateSection(),
            NullLogger<WindowPlacementTracker>.Instance);

        return new WindowStateService(placementTracker);
    }
}
