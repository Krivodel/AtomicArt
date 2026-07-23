using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;

using FluentAssertions;
using SukiUI.Controls.GlassMorphism;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Overlays;

public sealed class ModalOverlayControlTests : AnimatedGalleryControlTestBase
{
    private const double DefaultBlurRadius = 27d;
    private const double DefaultTintOpacity = 0.6d;
    private const double BlurMargin = -96d;
    private const double SukiBlurSizeDivisor = 42d;
    private const double SukiMinimumBaseBlurRadius = 20d;

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
                BlurBackground blurBackground = GetBlurBackground(overlay);

                overlay.Bounds.Width.Should().Be(480d);
                overlay.Bounds.Height.Should().Be(320d);
                overlay.Margin.Should().Be(margin);
                overlay.Padding.Should().Be(padding);
                backgroundChrome.CornerRadius.Should().Be(cornerRadius);
                CalculateEffectiveBlurRadius(blurBackground).Should().BeApproximately(18d, 0.001d);
                blurBackground.IsDynamic.Should().BeTrue();
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
                BlurBackground blurBackground = GetBlurBackground(overlay);
                SolidColorBrush tintBrush = overlay.Background
                    .Should()
                    .BeOfType<SolidColorBrush>()
                    .Subject;
                bool foundShadow = overlay.TryFindResource(
                    "FloatingSurfaceShadow",
                    out object? shadowResource);

                blurBackground.IsVisible.Should().BeTrue();
                overlay.IsBlurDynamic.Should().BeFalse();
                blurBackground.IsDynamic.Should().BeFalse();
                overlay.BlurRadius.Should().Be(DefaultBlurRadius);
                CalculateEffectiveBlurRadius(blurBackground)
                    .Should()
                    .BeApproximately(DefaultBlurRadius, 0.001d);
                blurBackground.Margin.Should().Be(new Thickness(BlurMargin));
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
    public void BlurRadius_WhenPanelsHaveDifferentSizes_ProducesSameEffectiveRadius()
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
                BlurBackground settingsBlur = GetBlurBackground(settingsOverlay);
                double settingsIntensity = settingsBlur.IntensityFactor;
                double settingsRadius = CalculateEffectiveBlurRadius(settingsBlur);

                ModalOverlayControl metadataOverlay = new()
                {
                    Width = 560d,
                    Height = 588d,
                    Body = new Border()
                };
                Window metadataWindow = Show(metadataOverlay, 900d, 900d);

                try
                {
                    BlurBackground metadataBlur = GetBlurBackground(metadataOverlay);
                    double metadataIntensity = metadataBlur.IntensityFactor;
                    double metadataRadius = CalculateEffectiveBlurRadius(metadataBlur);

                    settingsIntensity.Should().NotBe(metadataIntensity);
                    settingsRadius.Should().BeApproximately(DefaultBlurRadius, 0.001d);
                    metadataRadius.Should().BeApproximately(DefaultBlurRadius, 0.001d);
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
    public void Bounds_WhenChanged_RecalculatesBlurIntensityFactor()
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
                BlurBackground blurBackground = GetBlurBackground(overlay);
                double initialIntensity = blurBackground.IntensityFactor;

                overlay.Width = 560d;
                overlay.Height = 588d;
                window.CaptureRenderedFrame();

                blurBackground.IntensityFactor.Should().NotBe(initialIntensity);
                CalculateEffectiveBlurRadius(blurBackground)
                    .Should()
                    .BeApproximately(DefaultBlurRadius, 0.001d);
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
                    BlurBackground blurBackground = GetBlurBackground(overlay);

                    overlay.BlurRadius.Should().Be(CustomBlurRadius);
                    CalculateEffectiveBlurRadius(blurBackground)
                        .Should()
                        .BeApproximately(CustomBlurRadius, 0.001d);
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

    private static double CalculateEffectiveBlurRadius(BlurBackground blurBackground)
    {
        double baseBlurRadius = Math.Max(
            SukiMinimumBaseBlurRadius,
            (blurBackground.Bounds.Width + blurBackground.Bounds.Height)
            / SukiBlurSizeDivisor);

        return baseBlurRadius * blurBackground.IntensityFactor;
    }

    private static BlurBackground GetBlurBackground(ModalOverlayControl overlay)
    {
        return overlay
            .GetVisualDescendants()
            .OfType<BlurBackground>()
            .Single();
    }
}
