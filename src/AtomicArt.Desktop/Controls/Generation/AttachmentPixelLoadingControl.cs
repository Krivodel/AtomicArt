using System.Diagnostics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

using SkiaSharp;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Generation;

public sealed class AttachmentPixelLoadingControl : Control
{
    public Guid AnimationSeed
    {
        get => GetValue(AnimationSeedProperty);
        set => SetValue(AnimationSeedProperty, value);
    }
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

    public static readonly StyledProperty<Guid> AnimationSeedProperty =
        AvaloniaProperty.Register<AttachmentPixelLoadingControl, Guid>(
            nameof(AnimationSeed));
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
    private static readonly Stopwatch SharedAnimationStopwatch = Stopwatch.StartNew();

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private readonly TopLevelPresentationObserver _presentationObserver;
    private PixelLoadingState[] _pixels = [];
    private long _completionStartedAtMilliseconds;
    private int _completionDurationMilliseconds = CompletionDurationMilliseconds;
    private bool _isCompleting;
    private bool _usesUniformCompletionFade;

    static AttachmentPixelLoadingControl()
    {
        AnimationSeedProperty.Changed.AddClassHandler<AttachmentPixelLoadingControl>(
            OnAnimationSeedChanged);
        GridSizeProperty.Changed.AddClassHandler<AttachmentPixelLoadingControl>(
            OnGridSizeChanged);
        IsActiveProperty.Changed.AddClassHandler<AttachmentPixelLoadingControl>(
            OnIsActiveChanged);
    }

    public AttachmentPixelLoadingControl()
    {
        _presentationObserver = new TopLevelPresentationObserver(
            OnWindowPresentationChanged);
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

        _completionDurationMilliseconds = CompletionDurationMilliseconds;
        _isCompleting = true;
        _usesUniformCompletionFade = false;
        _completionStartedAtMilliseconds = _stopwatch.ElapsedMilliseconds;
        ResumeCompletion();
    }

    public void ShowCompleted()
    {
        _timer.Stop();
        _stopwatch.Reset();
        _completionStartedAtMilliseconds = 0L;
        _completionDurationMilliseconds = CompletionDurationMilliseconds;
        _isCompleting = true;
        _usesUniformCompletionFade = false;
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
        double elapsedSeconds = AnimationSeed == Guid.Empty
            ? elapsedMilliseconds / 1000d
            : SharedAnimationStopwatch.Elapsed.TotalSeconds;
        double completionProgress = _isCompleting
            ? Math.Clamp(
                (elapsedMilliseconds - _completionStartedAtMilliseconds)
                / (double)_completionDurationMilliseconds,
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
                _usesUniformCompletionFade,
                _pixels));
    }

    internal void FadeOut(int durationMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(durationMilliseconds, 1);

        if (!IsVisible)
        {
            return;
        }

        if (!_stopwatch.IsRunning)
        {
            _pixels = [];
            EnsurePixels();
            _stopwatch.Restart();
        }

        _completionDurationMilliseconds = durationMilliseconds;
        _isCompleting = true;
        _usesUniformCompletionFade = true;
        _completionStartedAtMilliseconds = _stopwatch.ElapsedMilliseconds;
        StartTimer();
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _presentationObserver.Attach(this);

        if (!IsActive || !CanAnimate())
        {
            return;
        }

        if (_isCompleting)
        {
            ResumeCompletion();
        }
        else
        {
            Restart();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _presentationObserver.Detach();
        _timer.Stop();
        _stopwatch.Stop();

        base.OnDetachedFromVisualTree(e);
    }

    private void Restart()
    {
        _pixels = [];
        EnsurePixels();
        _completionStartedAtMilliseconds = 0L;
        _completionDurationMilliseconds = CompletionDurationMilliseconds;
        _isCompleting = false;
        _usesUniformCompletionFade = false;
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

        _pixels = CreatePixelStates(GridSize, AnimationSeed);
    }

    internal static PixelLoadingState[] CreatePixelStates(
        int gridSize,
        Guid animationSeed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(gridSize, 1);

        int pixelCount = gridSize * gridSize;
        PixelLoadingState[] pixels = new PixelLoadingState[pixelCount];
        Random random = animationSeed == Guid.Empty
            ? Random.Shared
            : new Random(CalculateRandomSeed(animationSeed));

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = new PixelLoadingState(
                random.NextDouble() * Math.PI * 2d,
                PixelPalette[random.Next(PixelPalette.Length)],
                random.NextDouble());
        }

        return pixels;
    }

    private void StartTimer()
    {
        if (CanAnimate() && !_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void ResumeCompletion()
    {
        if (!IsVisible || !CanAnimate())
        {
            return;
        }

        _stopwatch.Start();
        StartTimer();
        InvalidateVisual();
    }

    private bool CanAnimate()
    {
        return !_presentationObserver.IsAttached
            || _presentationObserver.IsPresented;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!CanAnimate())
        {
            _timer.Stop();
            _stopwatch.Stop();
            return;
        }

        if (_isCompleting
            && _stopwatch.ElapsedMilliseconds - _completionStartedAtMilliseconds
            >= _completionDurationMilliseconds)
        {
            _timer.Stop();
            IsVisible = false;
            return;
        }

        InvalidateVisual();
    }

    private void OnWindowPresentationChanged(bool isPresented)
    {
        if (!isPresented)
        {
            _timer.Stop();
            _stopwatch.Stop();
            return;
        }

        if (!IsActive)
        {
            return;
        }

        if (_isCompleting)
        {
            ResumeCompletion();
            return;
        }

        Restart();
    }

    private static int CalculateRandomSeed(Guid animationSeed)
    {
        Span<byte> bytes = stackalloc byte[16];
        animationSeed.TryWriteBytes(bytes);
        int seed = 17;

        foreach (byte value in bytes)
        {
            seed = unchecked((seed * 31) + value);
        }

        return seed;
    }

    private static void OnAnimationSeedChanged(
        AttachmentPixelLoadingControl control,
        AvaloniaPropertyChangedEventArgs args)
    {
        _ = args;

        control._pixels = [];
        control.InvalidateVisual();
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
        if (IsActive && VisualRoot is not null && CanAnimate())
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
