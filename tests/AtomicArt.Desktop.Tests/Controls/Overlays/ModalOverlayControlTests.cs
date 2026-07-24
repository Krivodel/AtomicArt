using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Overlays;

public sealed class ModalOverlayControlTests : AnimatedGalleryControlTestBase
{
    private const double DefaultBlurRadius = 120d;
    private const double DefaultTintOpacity = 0.6d;

    [Fact]
    public void AppearanceProperties_WhenCustomized_PropagateToSharedBackground()
    {
        Dispatch(() =>
        {
            CornerRadius cornerRadius = new(14d);
            Thickness margin = new(12d);
            Thickness padding = new(18d);
            ModalOverlayControl overlay = new()
            {
                Width = 480d,
                Height = 320d,
                Margin = margin,
                Padding = padding,
                BlurIntensity = 0.4d,
                BlurRadius = 18d,
                Body = new Border(),
                CornerRadius = cornerRadius,
                IsBlurDynamic = true
            };
            Window window = Show(overlay);

            try
            {
                Border backgroundChrome = overlay
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Single(control => control.Name == "PART_BackgroundChrome");
                BlurBackdropControl blurBackdrop = GetBlurBackdrop(overlay);

                overlay.Bounds.Width.Should().Be(480d);
                overlay.Bounds.Height.Should().Be(320d);
                overlay.Margin.Should().Be(margin);
                overlay.Padding.Should().Be(padding);
                backgroundChrome.CornerRadius.Should().Be(cornerRadius);
                blurBackdrop.Intensity.Should().Be(0.4d);
                blurBackdrop.BlurRadius.Should().Be(18d);
                blurBackdrop.IsDynamic.Should().BeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Background_WhenRendered_UsesSharedBlurAndGeometryDefaults()
    {
        Dispatch(() =>
        {
            ModalOverlayControl overlay = new()
            {
                Body = new Border()
            };
            Window window = Show(overlay);

            try
            {
                Border backgroundChrome = overlay
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Single(control => control.Name == "PART_BackgroundChrome");
                Border shadowChrome = overlay
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Single(control => control.Name == "PART_ShadowChrome");
                BlurBackdropControl blurBackdrop = GetBlurBackdrop(overlay);
                SolidColorBrush tintBrush = overlay.Background
                    .Should()
                    .BeOfType<SolidColorBrush>()
                    .Subject;
                bool foundShadow = overlay.TryFindResource(
                    "FloatingSurfaceShadow",
                    out object? shadowResource);

                blurBackdrop.IsVisible.Should().BeTrue();
                blurBackdrop.IsHitTestVisible.Should().BeFalse();
                overlay.IsBlurDynamic.Should().BeFalse();
                blurBackdrop.IsDynamic.Should().BeFalse();
                overlay.BlurRadius.Should().Be(DefaultBlurRadius);
                blurBackdrop.BlurRadius.Should().Be(DefaultBlurRadius);
                tintBrush.Color.Should().Be(Color.Parse("#172133"));
                tintBrush.Opacity.Should().Be(DefaultTintOpacity);
                foundShadow.Should().BeTrue();
                shadowChrome.BoxShadow.Should().Be(
                    shadowResource.Should().BeOfType<BoxShadows>().Subject);
                shadowChrome.ClipToBounds.Should().BeFalse();
                shadowChrome.Bounds.Size.Should().Be(overlay.Bounds.Size);
                backgroundChrome.CornerRadius.Should().Be(overlay.CornerRadius);
                backgroundChrome.ClipToBounds.Should().BeTrue();
                backgroundChrome.Bounds.Size.Should().Be(overlay.Bounds.Size);
                overlay.ClipToBounds.Should().BeFalse();
                overlay.Margin.Should().Be(new Thickness(20d));
                overlay.Padding.Should().Be(new Thickness(24d));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void BlurRadius_WhenPanelsHaveDifferentSizes_UsesSameConfiguredStrength()
    {
        Dispatch(() =>
        {
            ModalOverlayControl settingsOverlay = new()
            {
                Width = 384d,
                Height = 413d,
                Body = new Border()
            };
            Window settingsWindow = Show(settingsOverlay, 900d, 900d);

            try
            {
                BlurBackdropControl settingsBlur = GetBlurBackdrop(settingsOverlay);

                ModalOverlayControl metadataOverlay = new()
                {
                    Width = 560d,
                    Height = 588d,
                    Body = new Border()
                };
                Window metadataWindow = Show(metadataOverlay, 900d, 900d);

                try
                {
                    BlurBackdropControl metadataBlur = GetBlurBackdrop(metadataOverlay);

                    settingsBlur.BlurRadius.Should().Be(DefaultBlurRadius);
                    metadataBlur.BlurRadius.Should().Be(DefaultBlurRadius);
                }
                finally
                {
                    metadataWindow.Close();
                }
            }
            finally
            {
                settingsWindow.Close();
            }
        });
    }

    [Fact]
    public void Bounds_WhenChanged_ResizesBlurBackdrop()
    {
        Dispatch(() =>
        {
            ModalOverlayControl overlay = new()
            {
                Width = 384d,
                Height = 413d,
                Body = new Border()
            };
            Window window = Show(overlay, 900d, 900d);

            try
            {
                BlurBackdropControl blurBackdrop = GetBlurBackdrop(overlay);

                overlay.Width = 560d;
                overlay.Height = 588d;
                window.CaptureRenderedFrame();

                blurBackdrop.Bounds.Width.Should().Be(560d);
                blurBackdrop.Bounds.Height.Should().Be(588d);
                blurBackdrop.BlurRadius.Should().Be(DefaultBlurRadius);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void BlurRadiusResource_WhenOverridden_ChangesAllModalBlurStrength()
    {
        Dispatch(() =>
        {
            const double CustomBlurRadius = 34d;
            Avalonia.Application application = Avalonia.Application.Current
                ?? throw new InvalidOperationException("Test application was not initialized.");
            application.Resources["ModalOverlayBlurRadius"] = CustomBlurRadius;

            try
            {
                ModalOverlayControl overlay = new()
                {
                    Width = 560d,
                    Height = 588d,
                    Body = new Border()
                };
                Window window = Show(overlay, 900d, 900d);

                try
                {
                    BlurBackdropControl blurBackdrop = GetBlurBackdrop(overlay);

                    overlay.BlurRadius.Should().Be(CustomBlurRadius);
                    blurBackdrop.BlurRadius.Should().Be(CustomBlurRadius);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                application.Resources.Remove("ModalOverlayBlurRadius");
            }
        });
    }

    private static BlurBackdropControl GetBlurBackdrop(ModalOverlayControl overlay)
    {
        return overlay
            .GetVisualDescendants()
            .OfType<BlurBackdropControl>()
            .Single();
    }
}
