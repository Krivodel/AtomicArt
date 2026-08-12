using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class SettingsOverlayViewTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void Background_WhenShown_UsesPopupGradientWithoutBlur()
    {
        Dispatch(() =>
        {
            SettingsOverlayView view = new();
            Window window = Show(view);

            try
            {
                window.CaptureRenderedFrame();
                ModalOverlayControl panel = view
                    .GetVisualDescendants()
                    .OfType<ModalOverlayControl>()
                    .Single();
                Border backgroundBase = panel
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Single(control => control.Classes.Contains("opaque-background-base"));
                bool gradientFound = panel.TryFindResource(
                    "PopupGradientBrush",
                    out object? popupGradient);
                ISolidColorBrush backgroundBrush = backgroundBase.Background
                    .Should()
                    .BeAssignableTo<ISolidColorBrush>()
                    .Subject;

                gradientFound.Should().BeTrue();
                panel.Background.Should().BeSameAs(popupGradient);
                panel.BlurRadius.Should().Be(0d);
                backgroundBrush.Color.A.Should().Be(byte.MaxValue);
                backgroundBrush.Opacity.Should().Be(1d);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
