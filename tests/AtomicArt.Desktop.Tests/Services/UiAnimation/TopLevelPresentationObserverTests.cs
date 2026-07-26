using Avalonia.Controls;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services.UiAnimation;

public sealed class TopLevelPresentationObserverTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void Attach_WhenWindowPresentationChanges_NotifiesOnlyChangedStates()
    {
        Dispatch(() =>
        {
            Border control = new();
            Window window = Show(control);
            List<bool> presentationStates = [];
            using TopLevelPresentationObserver observer = new(
                presentationStates.Add);

            try
            {
                observer.Attach(control);

                observer.IsPresented.Should().BeTrue();

                window.WindowState = WindowState.Minimized;
                window.WindowState = WindowState.Normal;
                window.Hide();

                presentationStates.Should().Equal(false, true, false);
                observer.IsPresented.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
