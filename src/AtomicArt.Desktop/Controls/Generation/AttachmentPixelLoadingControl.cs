using System.Diagnostics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

using SkiaSharp;

namespace AtomicArt.Desktop.Controls.Generation;

public sealed class AttachmentPixelLoadingControl : Control
{
    public int GridSize
    {
        get => GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private const int DefaultGridSize = 6;
    private const double PixelGap = 2d;
    private const double PixelCornerRadius = 2d;
    private const int FrameIntervalMilliseconds = 40;
    private const int CompletionDurationMilliseconds = 520;

    public static readonly StyledProperty<int> GridSizeProperty =
        AvaloniaProperty.Register<AttachmentPixelLoadingControl, int>(
            nameof(GridSize),
            defaultValue: DefaultGridSize,
            validate: value => value > 0);
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AttachmentPixelLoadingControl, bool>(
            nameof(IsActive),
            defaultValue: true);

    private static readonly SKColor[] PixelPalette =
    [
        new(0x5b, 0x8d, 0xff),
        new(0x8b, 0x6b, 0xff),
        new(0xff, 0x6e, 0xa8),
        new(0x6e, 0xa8, 0xff),
        new(0xa7, 0x8b, 0xff)
    ];

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private PixelLoadingState[] _pixels = [];
    private long _completionStartedAtMilliseconds;
    private bool _isCompleting;

    static AttachmentPixelLoadingControl()
    {
        GridSizeProperty.Changed.AddClassHandler<AttachmentPixelLoadingControl>(
            OnGridSizeChanged);
        IsActiveProperty.Changed.AddClassHandler<AttachmentPixelLoadingControl>(
            OnIsActiveChanged);
    }

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
        if (!IsActive || _isCompleting || !IsVisible)
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

        if (!IsActive || !IsVisible || Bounds.Width <= 0d || Bounds.Height <= 0d)
        {
            return;
        }

        EnsurePixels();
        int gridSize = GridSize;
        double availableSideLength = Math.Min(Bounds.Width, Bounds.Height);
        double totalGapLength = PixelGap * (gridSize - 1);
        double pixelSideLength = Math.Max(
            0d,
            (availableSideLength - totalGapLength) / gridSize);
        double gridSideLength = (pixelSideLength * gridSize) + totalGapLength;
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

        context.Custom(
            new PixelLoadingDrawOperation(
                new Rect(Bounds.Size),
                gridSize,
                PixelGap,
                PixelCornerRadius,
                pixelSideLength,
                originX,
                originY,
                elapsedSeconds,
                completionProgress,
                _pixels));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (IsActive && !_isCompleting)
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

    private void Restart()
    {
        _pixels = [];
        EnsurePixels();
        _completionStartedAtMilliseconds = 0L;
        _isCompleting = false;
        IsVisible = true;
        _stopwatch.Restart();
        StartTimer();
    }

    private void EnsurePixels()
    {
        int pixelCount = GridSize * GridSize;

        if (_pixels.Length == pixelCount)
        {
            return;
        }

        PixelLoadingState[] pixels = new PixelLoadingState[pixelCount];

        for (int index = 0; index < pixelCount; index++)
        {
            pixels[index] = new PixelLoadingState(
                Random.Shared.NextDouble() * Math.PI * 2d,
                PixelPalette[Random.Shared.Next(PixelPalette.Length)],
                Random.Shared.NextDouble());
        }

        _pixels = pixels;
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

    private static void OnGridSizeChanged(
        AttachmentPixelLoadingControl control,
        AvaloniaPropertyChangedEventArgs args)
    {
        _ = args;

        control._pixels = [];
        control.InvalidateVisual();
    }

    private static void OnIsActiveChanged(
        AttachmentPixelLoadingControl control,
        AvaloniaPropertyChangedEventArgs args)
    {
        _ = args;

        control.UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        if (IsActive && VisualRoot is not null)
        {
            Restart();
        }
        else if (!IsActive)
        {
            _timer.Stop();
            _stopwatch.Stop();
            _isCompleting = false;
        }

        InvalidateVisual();
    }
}
