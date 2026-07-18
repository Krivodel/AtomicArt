using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Generation;

public sealed class ImageDropOverlayControlTests : AnimatedGalleryControlTestBase
{
    private const double WindowHeight = 1080d;
    private const double WindowWidth = 1920d;

    [Fact]
    public void Layout_WhenWindowIsLarge_TransformsOnlyCompactContent()
    {
        Dispatch(() =>
        {
            ImageDropOverlayControl overlay = new()
            {
                IsActive = true
            };
            Window window = Show(overlay, WindowWidth, WindowHeight);

            try
            {
                Grid overlayChrome = overlay.FindControl<Grid>("OverlayChrome")
                    ?? throw new InvalidOperationException("Drop overlay chrome was not found.");
                StackPanel animatedContent = overlay.FindControl<StackPanel>("AnimatedContent")
                    ?? throw new InvalidOperationException("Drop overlay content was not found.");
                ImageDropDashedBorderControl dashedBorder = overlay
                    .GetVisualDescendants()
                    .OfType<ImageDropDashedBorderControl>()
                    .Single();

                overlayChrome.Bounds.Width.Should().BeApproximately(WindowWidth, 0.1d);
                overlayChrome.Bounds.Height.Should().BeApproximately(WindowHeight, 0.1d);
                overlayChrome.RenderTransform.Should().BeNull();
                animatedContent.Bounds.Width.Should().BeLessThan(overlayChrome.Bounds.Width);
                animatedContent.Bounds.Height.Should().BeLessThan(overlayChrome.Bounds.Height);
                animatedContent.RenderTransform.Should().NotBeNull();
                overlay.TryFindResource("ImageDropBorderColor", out object? borderColor).Should().BeTrue();
                dashedBorder.StrokeColor.Should().Be(borderColor.Should().BeOfType<Color>().Subject);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
