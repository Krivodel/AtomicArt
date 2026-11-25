using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Controls.Generation;
using AtomicArt.Desktop.Views.Gallery;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Theory]
    [InlineData(64, 64, -6)]
    [InlineData(1024, 1024, 4)]
    [InlineData(160, 90, -6)]
    [InlineData(1920, 1080, 4)]
    [InlineData(90, 160, -6)]
    [InlineData(1080, 1920, 4)]
    [InlineData(4096, 128, -6)]
    [InlineData(128, 4096, 4)]
    public async Task BranchPreview_WithDifferentResolutionsAndZooms_KeepsItsScreenSizeAndFitsTheWholeImage(
        int width, int height, int zoomSteps)
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            const double PixelTolerance = 0.01d;
            PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
            GenerationPreviewControl preview = previews[0];
            Grid expansion = preview.FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("The branch expansion host is unavailable.");
            (Size expected, _) = GenerationPreviewExpansionCalculator.CalculateFitted(
                new Size(preview.PreviewSize, preview.PreviewSize), new Size(width, height),
                new Rect(preview.Bounds.Size), new Rect(viewport.Bounds.Size));
            window.MouseMove(GetBranchPreviewPoint(preview, window));
            window.KeyPress(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, null);
            await WaitForExpandedBranchSizeAsync(window, expansion, expected);
            Rect initial = GetExpandedBranchBounds(expansion, viewport);
            initial.Width.Should().BeApproximately(expected.Width, 0.01d);
            initial.Height.Should().BeApproximately(expected.Height, 0.01d);
            initial.Width.Should().BeLessThanOrEqualTo(viewport.Bounds.Width + PixelTolerance);
            initial.Height.Should().BeLessThanOrEqualTo(viewport.Bounds.Height + PixelTolerance);
            (initial.Width / initial.Height).Should().BeApproximately((double)width / height, 0.01d);
            window.KeyRelease(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null);
            await WaitForFramesAsync(window, () => preview.ExpansionHost?.ActiveOwner is null
                && expansion.GetVisualAncestors().Contains(preview));

            for (int step = 0; step < Math.Abs(zoomSteps); step++)
            {
                (zoomSteps > 0 ? viewport.ZoomInCommand : viewport.ZoomOutCommand).Execute(null);
            }

            await WaitForFramesAsync(window, () => !viewport.IsInteracting);
            window.MouseMove(GetBranchPreviewPoint(preview, window));
            window.KeyPress(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, null);
            await WaitForExpandedBranchSizeAsync(window, expansion, expected);

            Rect zoomed = GetExpandedBranchBounds(expansion, viewport);
            zoomed.Width.Should().BeApproximately(initial.Width, 0.01d);
            zoomed.Height.Should().BeApproximately(initial.Height, 0.01d);
        }, new Size(width, height));
    }

    [Theory]
    [InlineData(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft)]
    [InlineData(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft)]
    [InlineData(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft)]
    public async Task BranchPreview_WhileZoomingWithAModifier_StaysFixedAndKeepsTheCardHidden(
        Key key, RawInputModifiers modifiers, PhysicalKey physicalKey)
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
            GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
            GenerationPreviewControl preview = previews[0];
            Control owner = panel.Children[0];
            Button card = owner.GetVisualDescendants().OfType<Button>().Single();
            Grid expansion = preview.FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("The branch expansion host is unavailable.");
            Image image = preview.FindControl<Image>("PreviewImage")
                ?? throw new InvalidOperationException("The branch preview image is unavailable.");
            Point pointer = GetBranchPreviewPoint(preview, window);
            Size sourceSize = image.Source?.Size ?? throw new InvalidOperationException("The branch preview has no image.");
            (Size expected, _) = GenerationPreviewExpansionCalculator.CalculateFitted(
                new Size(preview.PreviewSize, preview.PreviewSize), sourceSize,
                new Rect(preview.Bounds.Size), new Rect(viewport.Bounds.Size));
            window.MouseMove(pointer);
            window.KeyPress(key, modifiers, physicalKey, null);
            await WaitForExpandedBranchSizeAsync(window, expansion, expected);
            Rect initial = GetExpandedBranchBounds(expansion, viewport);
            double[] wheelSteps = [-3d, 5d, -2d, 3d];

            foreach (double step in wheelSteps)
            {
                double previousZoom = viewport.Zoom;
                window.MouseWheel(pointer, new Vector(0d, step), modifiers);
                await WaitForFramesAsync(window, () =>
                {
                    owner.Classes.Should().Contain(GenerationPreviewExpansionVisualMetrics.ExpandedClass);
                    preview.ExpansionHost?.ActiveOwner.Should().BeSameAs(owner);
                    card.Opacity.Should().Be(0d);
                    image.Source.Should().NotBeNull();
                    Rect current = GetExpandedBranchBounds(expansion, viewport);
                    current.X.Should().BeApproximately(initial.X, 0.01d);
                    current.Y.Should().BeApproximately(initial.Y, 0.01d);
                    current.Width.Should().BeApproximately(initial.Width, 0.01d);
                    current.Height.Should().BeApproximately(initial.Height, 0.01d);

                    return !viewport.IsInteracting;
                });
                viewport.Zoom.Should().NotBe(previousZoom);
            }

            using Bitmap captured = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("The enlarged branch image was not captured.");
            SaveRenderedFrame(captured, "chat-branch-preview-zoom.png");
            window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
            await WaitForFramesAsync(window, () => !owner.Classes.Contains(GenerationPreviewExpansionVisualMetrics.ExpandedClass));
            card.Opacity.Should().Be(1d);
            expansion.GetVisualAncestors().Should().Contain(preview);
        });
    }

    [Fact]
    public async Task BranchPreview_WhenThePointerLeavesTheOriginalCard_StaysOpenUntilItLeavesTheEnlargedImage()
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
            GenerationPreviewControl preview = previews[0];
            Grid expansion = preview.FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("The branch expansion host is unavailable.");
            window.MouseMove(GetBranchPreviewPoint(preview, window));
            window.KeyPress(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, null);
            await WaitForFramesAsync(window, () => expansion.Bounds.Height >= preview.PreviewSize * 1.6d);
            Point inside = expansion.TranslatePoint(new Point(expansion.Bounds.Width / 2d, expansion.Bounds.Height - 16d), window)
                ?? throw new InvalidOperationException("The enlarged image has no position.");

            window.MouseMove(inside, RawInputModifiers.Shift);
            window.CaptureRenderedFrame()?.Dispose();

            preview.ExpansionHost?.ActiveOwner.Should().NotBeNull();
            window.MouseMove(new Point(2d, 2d), RawInputModifiers.Shift);
            await WaitForFramesAsync(window, () => expansion.GetVisualAncestors().Contains(preview));
            preview.ExpansionHost?.ActiveOwner.Should().BeNull();
        });
    }

    [Theory]
    [InlineData(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft)]
    [InlineData(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft)]
    [InlineData(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft)]
    public async Task BranchPreview_WhenAnotherThumbnailIsBehindTheEnlargedImage_SwitchesWithoutReleasingTheModifier(
        Key key, RawInputModifiers modifiers, PhysicalKey physicalKey)
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
            GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
            Grid first = previews[0].FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("The first expansion host is unavailable.");
            Grid second = previews[1].FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("The second expansion host is unavailable.");

            for (int step = 0; step < 6; step++)
            {
                viewport.ZoomOutCommand.Execute(null);
            }

            await WaitForFramesAsync(window, () => !viewport.IsInteracting);
            Point firstPointer = GetBranchPreviewPoint(previews[0], window);
            Point secondPointer = GetBranchPreviewPoint(previews[1], window);
            window.MouseMove(firstPointer);
            window.KeyPress(key, modifiers, physicalKey, null);
            await WaitForFramesAsync(window, () => first.Bounds.Height >= previews[0].PreviewSize * 1.6d);
            GetExpandedBranchBounds(first, window).Contains(secondPointer).Should().BeTrue();
            Visual target = window.InputHitTest(secondPointer) as Visual
                ?? throw new InvalidOperationException("The thumbnail behind the enlarged image cannot receive input.");
            target.GetVisualAncestors().Should().Contain(previews[1]);

            window.MouseMove(secondPointer, modifiers);
            await WaitForFramesAsync(window, () => ReferenceEquals(previews[1].ExpansionHost?.ActiveOwner, panel.Children[1])
                && !panel.Children[0].Classes.Contains(GenerationPreviewExpansionVisualMetrics.ExpandedClass));

            GetExpandedBranchBounds(second, window).Contains(firstPointer).Should().BeTrue();
            window.MouseMove(firstPointer, modifiers);
            await WaitForFramesAsync(window, () => ReferenceEquals(previews[0].ExpansionHost?.ActiveOwner, panel.Children[0])
                && !panel.Children[1].Classes.Contains(GenerationPreviewExpansionVisualMetrics.ExpandedClass));
            window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        });
    }

    [Fact]
    public async Task BranchPreview_WhenTheCameraMovesAnotherThumbnailUnderThePointer_SwitchesOnlyAfterMouseMovement()
    {
        await VerifyBranchPreviewsAsync(async (window, view, previews) =>
        {
            PanZoomControl viewport = view.GetVisualDescendants().OfType<PanZoomControl>().Single();
            GenerationChatBranchPanel panel = view.GetVisualDescendants().OfType<GenerationChatBranchPanel>().Single();
            Grid expansion = previews[0].FindControl<Grid>("PreviewExpansionHost")
                ?? throw new InvalidOperationException("The branch expansion host is unavailable.");
            Point pointer = GetBranchPreviewPoint(previews[0], window);
            window.MouseMove(pointer);
            window.KeyPress(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, null);
            await WaitForFramesAsync(window, () => expansion.Bounds.Height >= previews[0].PreviewSize * 1.6d);

            viewport.FocusOn(panel.Children[1].Bounds);
            window.MouseMove(pointer, RawInputModifiers.Shift);
            window.CaptureRenderedFrame()?.Dispose();

            previews[0].ExpansionHost?.ActiveOwner.Should().BeSameAs(panel.Children[0]);
            window.MouseMove(pointer + new Vector(2d, 0d), RawInputModifiers.Shift);
            await WaitForFramesAsync(window, () => ReferenceEquals(previews[1].ExpansionHost?.ActiveOwner, panel.Children[1]));
            window.KeyRelease(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null);
        });
    }

    private static Rect GetExpandedBranchBounds(Control expansion, Visual viewport)
    {
        Matrix transform = expansion.TransformToVisual(viewport)
            ?? throw new InvalidOperationException("The enlarged image has no transform.");

        return new Rect(expansion.Bounds.Size).TransformToAABB(transform);
    }

    private static async Task WaitForExpandedBranchSizeAsync(Window window, Control expansion, Size expected)
    {
        try
        {
            await WaitForFramesAsync(window, () => Math.Abs(expansion.Bounds.Width - expected.Width) < 0.01d
                && Math.Abs(expansion.Bounds.Height - expected.Height) < 0.01d);
        }
        catch (TimeoutException exception)
        {
            Image? image = expansion.GetVisualDescendants().OfType<Image>().FirstOrDefault();

            throw new InvalidOperationException($"Preview expected {expected}; actual {expansion.Bounds}; target {expansion.Width}x{expansion.Height}; source {image?.Source?.Size}; parent {expansion.GetVisualParent()?.GetType().Name}.", exception);
        }
    }
}
