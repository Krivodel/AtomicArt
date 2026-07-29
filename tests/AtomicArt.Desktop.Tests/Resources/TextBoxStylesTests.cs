using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Controls.Gallery;
using AtomicArt.Desktop.Views.Gallery;
using AtomicArt.Desktop.Views.Generation;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Resources;

public sealed class TextBoxStylesTests : AnimatedGalleryControlTestBase
{
    [Fact]
    public void TextBox_WhenScrolled_UsesFullViewportAndKeepsThemeInset()
    {
        Dispatch(() =>
        {
            TextBox textBox = new()
            {
                AcceptsReturn = true,
                Height = 80d,
                Text = string.Join(
                    Environment.NewLine,
                    Enumerable.Range(1, 20).Select(lineNumber => $"Line {lineNumber}")),
                Width = 300d
            };
            Window window = Show(textBox);

            try
            {
                Thickness themePadding = GetThemePadding(textBox);

                TextBoxScrollContentAssertions.AssertUsesScrollableInsets(
                    textBox,
                    window,
                    themePadding);
                AssertUsesScrollEdgeFade(
                    textBox,
                    window,
                    themePadding);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ProductionViews_WhenShown_AllTextBoxesUseConfiguredInsetsAndScrollEdgeFade()
    {
        Dispatch(() =>
        {
            Control[] views =
            [
                new SecretSettingView(),
                new DataRootSettingView(),
                new ApiBaseAddressSettingView(),
                new NanoBanana2PanelView(),
                new GenerationMetadataOverlayView()
            ];
            int textBoxCount = 0;

            foreach (Control view in views)
            {
                Window window = Show(view, 750d, 780d);

                try
                {
                    IReadOnlyList<TextBox> textBoxes = view
                        .GetVisualDescendants()
                        .OfType<TextBox>()
                        .ToList();
                    textBoxCount += textBoxes.Count;

                    foreach (TextBox textBox in textBoxes)
                    {
                        Thickness expectedInsets = textBox.Classes
                            .Contains("metadata-text")
                            ? GetMetadataTextScrollInsets(textBox)
                            : GetThemePadding(textBox);
                        Control textPresenter =
                            TextBoxScrollContentAssertions.GetTextPresenter(textBox);
                        ScrollContentPresenter scrollPresenter =
                            GetScrollPresenter(textBox);
                        IReadOnlyList<TextBlock> placeholders = textBox
                            .GetVisualDescendants()
                            .OfType<TextBlock>()
                            .Where(textBlock =>
                                string.Equals(
                                    textBlock.Name,
                                    "PART_Placeholder",
                                    StringComparison.Ordinal)
                                || string.Equals(
                                    textBlock.Name,
                                    "PART_FloatingPlaceholder",
                                    StringComparison.Ordinal))
                            .ToList();

                        textPresenter.Margin.Should().Be(expectedInsets);
                        placeholders.Should().NotBeEmpty();
                        placeholders.Should().OnlyContain(
                            placeholder => placeholder.Margin == expectedInsets);
                        VerticalFadeMaskBehavior.GetInsets(scrollPresenter)
                            .Should()
                            .Be(expectedInsets);
                        scrollPresenter.OpacityMask.Should()
                            .BeOfType<LinearGradientBrush>();
                    }
                }
                finally
                {
                    window.Close();
                }
            }

            textBoxCount.Should().Be(6);
        });
    }

    private static Thickness GetThemePadding(TextBox textBox)
    {
        textBox.TryFindResource(
                "TextControlThemePadding",
                out object? paddingResource)
            .Should()
            .BeTrue();

        return paddingResource.Should()
            .BeOfType<Thickness>()
            .Subject;
    }

    private static Thickness GetMetadataTextScrollInsets(TextBox textBox)
    {
        textBox.TryFindResource(
                "MetadataTextScrollInsets",
                out object? insetsResource)
            .Should()
            .BeTrue();

        return insetsResource.Should()
            .BeOfType<Thickness>()
            .Subject;
    }

    private static ScrollContentPresenter GetScrollPresenter(TextBox textBox)
    {
        return textBox
            .GetVisualDescendants()
            .OfType<ScrollContentPresenter>()
            .Single();
    }

    private static ScrollViewer GetScrollViewer(TextBox textBox)
    {
        return textBox
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Single();
    }

    private static void AssertUsesScrollEdgeFade(
        TextBox textBox,
        Window window,
        Thickness fadeInsets)
    {
        ScrollViewer scrollViewer = GetScrollViewer(textBox);
        ScrollContentPresenter scrollPresenter = GetScrollPresenter(textBox);
        LinearGradientBrush opacityMask = scrollPresenter.OpacityMask
            .Should()
            .BeOfType<LinearGradientBrush>()
            .Subject;
        (double topFadeEnd, double bottomFadeStart) =
            VerticalFadeMaskBehavior.CalculateFadeOffsets(
                scrollPresenter.Bounds.Height,
                fadeInsets);

        scrollViewer.OpacityMask.Should().BeNull();
        scrollPresenter.ClipToBounds.Should().BeTrue();
        scrollPresenter.Bounds.Size.Should().Be(scrollPresenter.Viewport);
        VerticalFadeMaskBehavior.GetInsets(scrollPresenter)
            .Should()
            .Be(fadeInsets);
        opacityMask.GradientStops.Should().HaveCount(4);
        opacityMask.GradientStops[0].Color.A.Should().Be(0);
        opacityMask.GradientStops[0].Offset.Should().Be(0d);
        opacityMask.GradientStops[1].Color.A.Should().Be(byte.MaxValue);
        opacityMask.GradientStops[1].Offset.Should().BeApproximately(
            topFadeEnd,
            0.001d);
        opacityMask.GradientStops[2].Color.A.Should().Be(byte.MaxValue);
        opacityMask.GradientStops[2].Offset.Should().BeApproximately(
            bottomFadeStart,
            0.001d);
        opacityMask.GradientStops[3].Color.A.Should().Be(0);
        opacityMask.GradientStops[3].Offset.Should().Be(1d);
        (opacityMask.GradientStops[1].Offset
         * scrollPresenter.Bounds.Height)
            .Should()
            .BeApproximately(fadeInsets.Top, 0.001d);
        ((1d - opacityMask.GradientStops[2].Offset)
         * scrollPresenter.Bounds.Height)
            .Should()
            .BeApproximately(fadeInsets.Bottom, 0.001d);

        scrollViewer.Offset = new Vector(
            0d,
            scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        window.CaptureRenderedFrame();

        scrollPresenter.Offset.Y.Should().BeGreaterThan(0d);
        scrollPresenter.OpacityMask.Should().BeSameAs(opacityMask);
        opacityMask.GradientStops[0].Color.A.Should().Be(0);
        opacityMask.GradientStops[3].Color.A.Should().Be(0);
    }

}
