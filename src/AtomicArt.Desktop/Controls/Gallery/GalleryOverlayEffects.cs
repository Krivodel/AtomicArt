using Avalonia.Controls.Shapes;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryOverlayEffects
{
    private readonly GalleryAnimationScheduler _animationScheduler;

    public GalleryOverlayEffects(GalleryAnimationScheduler animationScheduler)
    {
        _animationScheduler = animationScheduler ?? throw new ArgumentNullException(nameof(animationScheduler));
    }

    internal Control CreateOverlayCard(GalleryOperationCoordinator context, object item, Rect rect)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(item);

        Control clone = context.CreateControl(item);
        clone.Width = rect.Width;
        clone.Height = rect.Height;
        clone.IsHitTestVisible = false;
        clone.Opacity = 0d;
        Canvas.SetLeft(clone, rect.Left);
        Canvas.SetTop(clone, rect.Top);
        context.OverlayCanvas.Children.Add(clone);

        return clone;
    }

    internal Task AnimateWakeAsync(
        Canvas overlayCanvas,
        Rect last,
        int order,
        List<MotionFrame> frames,
        int duration,
        int delay)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);
        ArgumentNullException.ThrowIfNull(frames);

        if (order > 20)
        {
            return Task.CompletedTask;
        }

        Border wake = CreateWake(last);
        overlayCanvas.Children.Add(wake);

        return _animationScheduler.AnimateAsync(
            wake,
            CreateWakeFrames(frames),
            duration,
            delay,
            MotionEasing.EaseRail,
            () => overlayCanvas.Children.Remove(wake));
    }

    internal List<Task> CreateBurst(
        Canvas overlayCanvas,
        Point center,
        double speed,
        int delay,
        GalleryAnimationTracker overlayControls,
        GalleryAnimationTracker animatedControls)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);
        ArgumentNullException.ThrowIfNull(overlayControls);
        ArgumentNullException.ThrowIfNull(animatedControls);

        (Ellipse Glow, Ellipse Burst) overlays = CreateBurstOverlays(
            overlayCanvas,
            center,
            overlayControls,
            animatedControls);

        List<Task> animations =
        [
            CreateGlowAnimation(overlayCanvas, overlayControls, overlays.Glow, speed, delay),
            CreateBurstRingAnimation(overlayCanvas, overlayControls, overlays.Burst, speed, delay)
        ];

        return animations;
    }

    internal Task CreateTargetFlash(
        Canvas overlayCanvas,
        Rect rect,
        int index,
        double speed,
        int delay,
        GalleryAnimationTracker overlayControls,
        GalleryAnimationTracker animatedControls)
    {
        ArgumentNullException.ThrowIfNull(overlayCanvas);
        ArgumentNullException.ThrowIfNull(overlayControls);
        ArgumentNullException.ThrowIfNull(animatedControls);

        if (index > 7)
        {
            return Task.CompletedTask;
        }

        Border flash = CreateTargetFlashOverlay(rect);
        RegisterOverlay(overlayCanvas, overlayControls, animatedControls, flash);

        return _animationScheduler.AnimateAsync(
            flash,
            CreateTargetFlashFrames(),
            AnimationTiming.ScaleTime(520, speed),
            delay + AnimationTiming.ScaleTime(430 + (index * 28), speed),
            MotionEasing.EaseOut,
            () => RemoveOverlay(overlayCanvas, overlayControls, flash));
    }

    private static Border CreateWake(Rect last)
    {
        Border wake = new()
        {
            Width = last.Width,
            Height = last.Height,
            CornerRadius = new CornerRadius(15d),
            BorderBrush = Brush.Parse("#33AACDFF"),
            BorderThickness = new Thickness(1d),
            Background = Brush.Parse("#1F253656"),
            Opacity = 0.52d
        };
        Canvas.SetLeft(wake, last.Left);
        Canvas.SetTop(wake, last.Top);

        return wake;
    }

    private static List<MotionFrame> CreateWakeFrames(IReadOnlyList<MotionFrame> frames)
    {
        return frames
            .Select((frame, index) => frame with { Opacity = index == frames.Count - 1 ? 0d : 0.34d })
            .ToList();
    }

    private static Border CreateTargetFlashOverlay(Rect rect)
    {
        Border flash = new()
        {
            Width = rect.Width,
            Height = rect.Height,
            CornerRadius = new CornerRadius(16d),
            BorderBrush = Brush.Parse("#57AACDFF"),
            BorderThickness = new Thickness(1d),
            Background = Brush.Parse("#1A9AC6FF"),
            Opacity = 0d
        };
        Canvas.SetLeft(flash, rect.Left);
        Canvas.SetTop(flash, rect.Top);

        return flash;
    }

    private static List<MotionFrame> CreateTargetFlashFrames()
    {
        return
        [
            new MotionFrame(0d, 0d, 0.96d, 0d, 0d),
            new MotionFrame(0d, 0d, 1d, 0d, 0.75d),
            new MotionFrame(0d, 0d, 1.025d, 0d, 0d)
        ];
    }

    private static Ellipse CreateGlow(Point center)
    {
        Ellipse glow = new()
        {
            Width = 190d,
            Height = 190d,
            Fill = Brush.Parse("#338CB9FF"),
            Opacity = 0d
        };
        Canvas.SetLeft(glow, center.X - (glow.Width / 2d));
        Canvas.SetTop(glow, center.Y - (glow.Height / 2d));

        return glow;
    }

    private static void RegisterOverlay(
        Canvas overlayCanvas,
        GalleryAnimationTracker overlayControls,
        GalleryAnimationTracker animatedControls,
        Control overlay)
    {
        overlayCanvas.Children.Add(overlay);
        overlayControls.Add(overlay);
        animatedControls.Add(overlay);
    }

    private static Ellipse CreateBurstRing(Point center)
    {
        Ellipse burst = new()
        {
            Width = 42d,
            Height = 42d,
            Stroke = Brush.Parse("#ADAACEFF"),
            StrokeThickness = 1d,
            Fill = Brush.Parse("#66B9DAFF"),
            Opacity = 0d
        };
        Canvas.SetLeft(burst, center.X - (burst.Width / 2d));
        Canvas.SetTop(burst, center.Y - (burst.Height / 2d));

        return burst;
    }

    private static void RemoveOverlay(
        Canvas overlayCanvas,
        GalleryAnimationTracker overlayControls,
        Control overlay)
    {
        overlayCanvas.Children.Remove(overlay);
        overlayControls.Remove(overlay);
    }

    private (Ellipse Glow, Ellipse Burst) CreateBurstOverlays(
        Canvas overlayCanvas,
        Point center,
        GalleryAnimationTracker overlayControls,
        GalleryAnimationTracker animatedControls)
    {
        Ellipse glow = CreateGlow(center);
        Ellipse burst = CreateBurstRing(center);
        RegisterOverlay(overlayCanvas, overlayControls, animatedControls, glow);
        RegisterOverlay(overlayCanvas, overlayControls, animatedControls, burst);

        return (glow, burst);
    }

    private Task CreateGlowAnimation(
        Canvas overlayCanvas,
        GalleryAnimationTracker overlayControls,
        Control glow,
        double speed,
        int delay)
    {
        List<MotionFrame> frames =
        [
            new(0d, 0d, 0.40d, 0d, 0d),
            new(0d, 0d, 0.82d, 0d, 0.95d),
            new(0d, 0d, 1.42d, 0d, 0d)
        ];

        return _animationScheduler.AnimateAsync(
            glow,
            frames,
            AnimationTiming.ScaleTime(680, speed),
            delay,
            MotionEasing.EaseMaterial,
            () => RemoveOverlay(overlayCanvas, overlayControls, glow));
    }

    private Task CreateBurstRingAnimation(
        Canvas overlayCanvas,
        GalleryAnimationTracker overlayControls,
        Control burst,
        double speed,
        int delay)
    {
        List<MotionFrame> frames =
        [
            new(0d, 0d, 0.25d, 0d, 0d),
            new(0d, 0d, 0.95d, 0d, 0.82d),
            new(0d, 0d, 4.4d, 0d, 0d)
        ];

        return _animationScheduler.AnimateAsync(
            burst,
            frames,
            AnimationTiming.ScaleTime(560, speed),
            delay,
            MotionEasing.EaseMaterial,
            () => RemoveOverlay(overlayCanvas, overlayControls, burst));
    }
}
