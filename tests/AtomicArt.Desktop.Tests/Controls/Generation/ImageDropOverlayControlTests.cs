using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Generation;

public sealed class ImageDropOverlayControlTests : AnimatedGalleryControlTestBase
{
    private const double DefaultBlurRadius = 24d;
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

    [Fact]
    public void Background_WhenActive_UsesDynamicBlurWithoutDimmingFill()
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
                BlurBackdropControl blurBackdrop = overlay
                    .GetVisualDescendants()
                    .OfType<BlurBackdropControl>()
                    .Single();
                Border backdropChrome = blurBackdrop.GetVisualParent<Border>()
                    ?? throw new InvalidOperationException("Drop backdrop chrome was not found.");

                blurBackdrop.BlurRadius.Should().Be(DefaultBlurRadius);
                blurBackdrop.IsDynamic.Should().BeTrue();
                backdropChrome.Background.Should().BeNull();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void IsActive_WhenChanged_UpdatesDynamicBlurCapture()
    {
        Dispatch(() =>
        {
            ImageDropOverlayControl overlay = new();
            Window window = Show(overlay, WindowWidth, WindowHeight);

            try
            {
                BlurBackdropControl blurBackdrop = overlay
                    .GetVisualDescendants()
                    .OfType<BlurBackdropControl>()
                    .Single();

                blurBackdrop.IsDynamic.Should().BeFalse();

                overlay.IsActive = true;

                blurBackdrop.IsDynamic.Should().BeTrue();

                overlay.IsActive = false;

                blurBackdrop.IsDynamic.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void BlurIntensityTransition_WhenConfigured_MatchesOverlayFade()
    {
        Dispatch(() =>
        {
            ImageDropOverlayControl overlay = new();
            Window window = Show(overlay, WindowWidth, WindowHeight);

            try
            {
                Grid overlayChrome = overlay.FindControl<Grid>("OverlayChrome")
                    ?? throw new InvalidOperationException("Drop overlay chrome was not found.");
                BlurBackdropControl blurBackdrop = overlay
                    .GetVisualDescendants()
                    .OfType<BlurBackdropControl>()
                    .Single();
                DoubleTransition overlayTransition = overlayChrome.Transitions
                    ?.OfType<DoubleTransition>()
                    .Single()
                    ?? throw new InvalidOperationException("Drop overlay transition was not found.");
                DoubleTransition blurTransition = blurBackdrop.Transitions
                    ?.OfType<DoubleTransition>()
                    .Single()
                    ?? throw new InvalidOperationException("Drop blur transition was not found.");

                blurTransition.Property.Should().Be(BlurBackdropControl.IntensityProperty);
                blurTransition.Duration.Should().Be(overlayTransition.Duration);
                blurTransition.Easing.Should().BeSameAs(overlayTransition.Easing);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void IsActive_WhenTransitionsAreDisabled_UpdatesBlurIntensityTarget()
    {
        Dispatch(() =>
        {
            ImageDropOverlayControl overlay = new();
            Window window = Show(overlay, WindowWidth, WindowHeight);

            try
            {
                BlurBackdropControl blurBackdrop = overlay
                    .GetVisualDescendants()
                    .OfType<BlurBackdropControl>()
                    .Single();
                blurBackdrop.Transitions = null;

                overlay.IsActive = true;

                blurBackdrop.Intensity.Should().Be(1d);

                overlay.IsActive = false;

                blurBackdrop.Intensity.Should().Be(0d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FontFamily_WhenResourceIsOverridden_InheritsConfiguredApplicationFamily()
    {
        Dispatch(() =>
        {
            Avalonia.Application application = Avalonia.Application.Current
                ?? throw new InvalidOperationException("Test application was not initialized.");
            const string ConfiguredFontFamilyName = "AtomicArt Test Font";
            FontFamily configuredFontFamily = new(ConfiguredFontFamilyName);
            object? originalFontFamily = application.Resources["DefaultFontFamily"];
            application.Resources["DefaultFontFamily"] = configuredFontFamily;

            try
            {
                ImageDropOverlayControl overlay = new();
                Window window = Show(overlay, WindowWidth, WindowHeight);

                try
                {
                    TextBlock label = overlay
                        .GetVisualDescendants()
                        .OfType<TextBlock>()
                        .Single();

                    configuredFontFamily.Name.Should().NotBe(FontFamily.Default.Name);
                    window.FontFamily.Should().Be(configuredFontFamily);
                    label.FontFamily.Should().Be(configuredFontFamily);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                application.Resources["DefaultFontFamily"] = originalFontFamily;
            }
        });
    }
}
