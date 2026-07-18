using System.Diagnostics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace AtomicArt.Desktop.Controls.Generation;

public sealed class AttachmentPixelLoadingControl : Control
{
    private const int GridSize = 6;
    private const int PixelCount = GridSize * GridSize;
    private const double PixelGap = 2d;
    private const double PixelCornerRadius = 2d;
    private const double MinimumOpacity = 0.1d;
    private const double OpacityRange = 0.85d;
    private const double FlickerDurationSeconds = 1.8d;
    private const double CompletionStaggerRange = 0.42d;
    private const int FrameIntervalMilliseconds = 40;
    private const int CompletionDurationMilliseconds = 520;

    private static readonly IBrush[] PixelPalette =
    [
        new SolidColorBrush(Color.Parse("#5b8dff")),
        new SolidColorBrush(Color.Parse("#8b6bff")),
        new SolidColorBrush(Color.Parse("#ff6ea8")),
        new SolidColorBrush(Color.Parse("#6ea8ff")),
        new SolidColorBrush(Color.Parse("#a78bff"))
    ];

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private readonly List<AttachmentPixelState> _pixels = [];
    private long _completionStartedAtMilliseconds;
    private bool _isCompleting;

    public AttachmentPixelLoadingControl()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FrameIntervalMilliseconds)
        };
        _timer.Tick += OnTimerTick;
        IsHitTestVisible = false;
    }

    public void Complete()
    {
        if (_isCompleting || !IsVisible)
        {
            return;
        }

        _isCompleting = true;
        _completionStartedAtMilliseconds = _stopwatch.ElapsedMilliseconds;
        StartTimer();
    }

    public void ShowCompleted()
    {
        _timer.Stop();
        _stopwatch.Reset();
        _completionStartedAtMilliseconds = 0L;
        _isCompleting = true;
        IsVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!IsVisible || Bounds.Width <= 0d || Bounds.Height <= 0d)
        {
            return;
        }

        EnsurePixels();
        double availableSideLength = Math.Min(Bounds.Width, Bounds.Height);
        double totalGapLength = PixelGap * (GridSize - 1);
        double pixelSideLength = Math.Max(
            0d,
            (availableSideLength - totalGapLength) / GridSize);
        double gridSideLength = (pixelSideLength * GridSize) + totalGapLength;
        double originX = (Bounds.Width - gridSideLength) / 2d;
        double originY = (Bounds.Height - gridSideLength) / 2d;
        long elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
        double elapsedSeconds = elapsedMilliseconds / 1000d;
        double completionProgress = _isCompleting
            ? Math.Clamp(
                (elapsedMilliseconds - _completionStartedAtMilliseconds)
                / (double)CompletionDurationMilliseconds,
                0d,
                1d)
            : 0d;

        for (int index = 0; index < _pixels.Count; index++)
        {
            AttachmentPixelState pixel = _pixels[index];
            int row = index / GridSize;
            int column = index % GridSize;
            double pixelX = originX + (column * (pixelSideLength + PixelGap));
            double pixelY = originY + (row * (pixelSideLength + PixelGap));
            double opacity = CalculateLoadingOpacity(pixel, elapsedSeconds);

            if (_isCompleting)
            {
                double staggerStart = pixel.DisappearOrder * CompletionStaggerRange;
                double localProgress = Math.Clamp(
                    (completionProgress - staggerStart) / (1d - staggerStart),
                    0d,
                    1d);
                double smoothProgress = localProgress
                    * localProgress
                    * (3d - (2d * localProgress));
                opacity *= 1d - smoothProgress;
            }

            Rect pixelBounds = new(
                pixelX,
                pixelY,
                pixelSideLength,
                pixelSideLength);

            using (context.PushOpacity(opacity))
            {
                context.DrawRectangle(
                    pixel.Brush,
                    null,
                    pixelBounds,
                    PixelCornerRadius,
                    PixelCornerRadius);
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!_isCompleting)
        {
            Restart();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        _stopwatch.Stop();

        base.OnDetachedFromVisualTree(e);
    }

    private static double CalculateLoadingOpacity(
        AttachmentPixelState pixel,
        double elapsedSeconds)
    {
        double shimmer = 0.5d
            + (0.5d * Math.Sin(
                pixel.InitialPhase
                + (elapsedSeconds * Math.Tau / FlickerDurationSeconds)));

        return MinimumOpacity + (OpacityRange * shimmer);
    }

    private void Restart()
    {
        _pixels.Clear();
        EnsurePixels();
        _completionStartedAtMilliseconds = 0L;
        _isCompleting = false;
        IsVisible = true;
        _stopwatch.Restart();
        StartTimer();
    }

    private void EnsurePixels()
    {
        if (_pixels.Count > 0)
        {
            return;
        }

        for (int index = 0; index < PixelCount; index++)
        {
            _pixels.Add(new AttachmentPixelState(
                Random.Shared.NextDouble() * Math.PI * 2d,
                PixelPalette[Random.Shared.Next(PixelPalette.Length)],
                Random.Shared.NextDouble()));
        }
    }

    private void StartTimer()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isCompleting
            && _stopwatch.ElapsedMilliseconds - _completionStartedAtMilliseconds
            >= CompletionDurationMilliseconds)
        {
            _timer.Stop();
            IsVisible = false;
            return;
        }

        InvalidateVisual();
    }

    private sealed record AttachmentPixelState(
        double InitialPhase,
        IBrush Brush,
        double DisappearOrder);
}
