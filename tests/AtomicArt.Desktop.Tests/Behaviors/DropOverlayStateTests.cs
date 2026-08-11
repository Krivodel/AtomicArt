using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Behaviors;

public sealed class DropOverlayStateTests : AnimatedGalleryControlTestBase
{
    private const int HideWaitMilliseconds = 100;

    [Fact]
    public async Task CancelScheduledHide_AfterPendingHide_KeepsOverlayActive()
    {
        await DispatchAsync(async () =>
        {
            ImageDropOverlayControl overlay = new()
            {
                IsActive = true
            };
            Grid target = new();
            target.Children.Add(overlay);
            Window window = Show(target, 320d, 180d);

            try
            {
                DropOverlayState.ScheduleHide(target, overlay);
                DropOverlayState.CancelScheduledHide(target);

                await Task.Delay(HideWaitMilliseconds);

                overlay.IsActive.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
