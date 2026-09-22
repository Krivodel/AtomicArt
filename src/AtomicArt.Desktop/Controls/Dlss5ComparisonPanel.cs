using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls;

public sealed class Dlss5ComparisonPanel : Panel
{
    public const double CaptionHeight = 28d;
    public const double ProgressHeight = 10d;

    public static GridLength CaptionRowHeight => new GridLength(CaptionHeight);
    public static GridLength ProgressRowHeight => new GridLength(ProgressHeight);

    public bool HasSource
    {
        get => GetValue(HasSourceProperty);
        set => SetValue(HasSourceProperty, value);
    }
    public double SourceWidth
    {
        get => GetValue(SourceWidthProperty);
        set => SetValue(SourceWidthProperty, value);
    }
    public double SourceHeight
    {
        get => GetValue(SourceHeightProperty);
        set => SetValue(SourceHeightProperty, value);
    }
    public double SplitProgress
    {
        get => GetValue(SplitProgressProperty);
        set => SetValue(SplitProgressProperty, value);
    }
    public bool IsRendering
    {
        get => GetValue(IsRenderingProperty);
        set => SetValue(IsRenderingProperty, value);
    }
    public IBrush? ProgressBrush
    {
        get => GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }
    public IBrush? ProgressHighlightBrush
    {
        get => GetValue(ProgressHighlightBrushProperty);
        set => SetValue(ProgressHighlightBrushProperty, value);
    }

    public static readonly StyledProperty<bool> HasSourceProperty =
        AvaloniaProperty.Register<Dlss5ComparisonPanel, bool>(nameof(HasSource));
    public static readonly StyledProperty<double> SourceWidthProperty =
        AvaloniaProperty.Register<Dlss5ComparisonPanel, double>(nameof(SourceWidth));
    public static readonly StyledProperty<double> SourceHeightProperty =
        AvaloniaProperty.Register<Dlss5ComparisonPanel, double>(nameof(SourceHeight));
    public static readonly StyledProperty<double> SplitProgressProperty =
        AvaloniaProperty.Register<Dlss5ComparisonPanel, double>(nameof(SplitProgress));
    public static readonly StyledProperty<bool> IsRenderingProperty =
        AvaloniaProperty.Register<Dlss5ComparisonPanel, bool>(nameof(IsRendering));
    public static readonly StyledProperty<IBrush?> ProgressBrushProperty =
        AvaloniaProperty.Register<Dlss5ComparisonPanel, IBrush?>(nameof(ProgressBrush));
    public static readonly StyledProperty<IBrush?> ProgressHighlightBrushProperty =
        AvaloniaProperty.Register<Dlss5ComparisonPanel, IBrush?>(nameof(ProgressHighlightBrush));

    internal const int FirstCardDurationMilliseconds = 208;
    internal const int DealDelayMilliseconds = 24;
    internal const int ResultCardDurationMilliseconds = 304;

    private const int ExpectedChildCount = 4;
    private const int ProgressChildIndex = 0;
    private const int SourceChildIndex = 1;
    private const int ResultChildIndex = 2;
    private const int PlaceholderChildIndex = 3;
    private const int OpeningDurationMilliseconds =
        FirstCardDurationMilliseconds + DealDelayMilliseconds + ResultCardDurationMilliseconds;
    private const int ClosingDurationMilliseconds = OpeningDurationMilliseconds;
    private const int ProgressFadeDurationMilliseconds = 120;
    private const double ImageGap = 12d;
    private const double DealKeyFrameOffset = 0.4d;
    private const double DealScale = 1.07d;
    private const double DealRotation = 4d;

    private readonly IUiFrameScheduler? _frameScheduler;
    private readonly Dlss5ProgressLine _progressLine = new();
    private AvaloniaUiFrameScheduler? _ownedFrameScheduler;
    private UiAnimationScheduler? _animationScheduler;
    private AnimatedTransformState? _sourceTransform;
    private AnimatedTransformState? _resultTransform;
    private AnimatedTransformState? _placeholderTransform;
    private Dlss5PanelAnimationState _currentAnimationState = GetClosingAnimationState(
        TimeSpan.FromMilliseconds(ClosingDurationMilliseconds));
    private double _frameRatio = 1d;
    private int _animationVersion;
    private bool _hasArrangedFrames;
    private bool _isAttached;
    private bool _isOpeningTransition;
    private bool _isTransitioning;
    private bool _awaitingSourceRemoval;
    private TaskCompletionSource? _closeAnimationCompletion;

    static Dlss5ComparisonPanel()
    {
        AffectsMeasure<Dlss5ComparisonPanel>(SourceWidthProperty, SourceHeightProperty);
        HasSourceProperty.Changed.AddClassHandler<Dlss5ComparisonPanel>(OnHasSourceChanged);
        SourceWidthProperty.Changed.AddClassHandler<Dlss5ComparisonPanel>(OnSourceSizeChanged);
        SourceHeightProperty.Changed.AddClassHandler<Dlss5ComparisonPanel>(OnSourceSizeChanged);
    }

    public Dlss5ComparisonPanel()
        : this(null)
    {
    }

    internal Dlss5ComparisonPanel(IUiFrameScheduler? frameScheduler)
    {
        _frameScheduler = frameScheduler;
        _progressLine.Opacity = 0d;
        _progressLine.IsVisible = true;
        _progressLine.ZIndex = 1;
        _progressLine.Transitions =
        [
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(ProgressFadeDurationMilliseconds),
                Easing = new CubicEaseOut()
            }
        ];
        Children.Add(_progressLine);
    }

    public Task CloseSourceAsync()
    {
        if (!HasSource || (!_isTransitioning && (SplitProgress <= 0d)))
        {
            ApplyTerminalState(false);
            return Task.CompletedTask;
        }

        _awaitingSourceRemoval = true;
        return StartAnimation(false, completeWhenClosed: true);
    }

    internal static Dlss5PanelAnimationState GetOpeningAnimationState(TimeSpan elapsed)
    {
        double elapsedMilliseconds = Math.Clamp(
            elapsed.TotalMilliseconds,
            0d,
            OpeningDurationMilliseconds);
        if (elapsedMilliseconds < FirstCardDurationMilliseconds)
        {
            double rawProgress = elapsedMilliseconds / FirstCardDurationMilliseconds;
            double progress = GetCardEasing(rawProgress);
            double imageOpacity = SmoothStep(rawProgress);

            return new Dlss5PanelAnimationState(
                progress,
                0d,
                imageOpacity,
                0d,
                1d - imageOpacity,
                1d,
                0d);
        }

        double resultElapsed = elapsedMilliseconds
            - FirstCardDurationMilliseconds
            - DealDelayMilliseconds;
        if (resultElapsed <= 0d)
        {
            return new Dlss5PanelAnimationState(1d, 0d, 1d, 0d, 0d, 1d, 0d);
        }

        double rawDealProgress = Math.Clamp(
            resultElapsed / ResultCardDurationMilliseconds,
            0d,
            1d);
        double dealProgress = GetCardEasing(rawDealProgress);
        (double scale, double rotation) = GetDealPose(dealProgress);

        return new Dlss5PanelAnimationState(
            1d,
            dealProgress,
            1d,
            SmoothStep(Math.Clamp(rawDealProgress / 0.28d, 0d, 1d)),
            0d,
            scale,
            rotation);
    }

    internal static Dlss5PanelAnimationState GetClosingAnimationState(TimeSpan elapsed)
    {
        double elapsedMilliseconds = Math.Clamp(
            elapsed.TotalMilliseconds,
            0d,
            ClosingDurationMilliseconds);
        if (elapsedMilliseconds < ResultCardDurationMilliseconds)
        {
            double rawProgress = elapsedMilliseconds / ResultCardDurationMilliseconds;
            double dealProgress = 1d - GetCardEasing(rawProgress);
            (double scale, double rotation) = GetDealPose(dealProgress);
            double resultOpacity = 1d - SmoothStep(Math.Clamp(
                (rawProgress - 0.65d) / 0.35d,
                0d,
                1d));

            return new Dlss5PanelAnimationState(
                1d,
                dealProgress,
                1d,
                resultOpacity,
                0d,
                scale,
                rotation);
        }

        double firstCardElapsed = elapsedMilliseconds
            - ResultCardDurationMilliseconds
            - DealDelayMilliseconds;
        if (firstCardElapsed <= 0d)
        {
            return new Dlss5PanelAnimationState(1d, 0d, 1d, 0d, 0d, 1d, 0d);
        }

        double rawFirstCardProgress = Math.Clamp(
            firstCardElapsed / FirstCardDurationMilliseconds,
            0d,
            1d);
        double firstCardProgress = 1d - GetCardEasing(rawFirstCardProgress);
        double placeholderOpacity = SmoothStep(rawFirstCardProgress);

        return new Dlss5PanelAnimationState(
            firstCardProgress,
            0d,
            1d - placeholderOpacity,
            0d,
            placeholderOpacity,
            1d,
            0d);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size panelSize = NormalizeSize(availableSize);
        (Rect source, Rect result) = GetImageFrames(panelSize);

        if (Children.Count == ExpectedChildCount)
        {
            Children[ProgressChildIndex].Measure(new Size(result.Width, ProgressHeight));
            Children[SourceChildIndex].Measure(source.Size);
            Children[ResultChildIndex].Measure(result.Size);
            Children[PlaceholderChildIndex].Measure(panelSize);
        }

        return panelSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count != ExpectedChildCount)
        {
            _hasArrangedFrames = false;
            return finalSize;
        }

        (Rect source, Rect result) = GetImageFrames(finalSize);
        _progressLine.Arrange(new Rect(
            result.Left,
            result.Bottom - ProgressHeight,
            result.Width,
            ProgressHeight));
        Children[SourceChildIndex].Arrange(source);
        Children[ResultChildIndex].Arrange(result);
        Children[PlaceholderChildIndex].Arrange(new Rect(finalSize));
        EnsureTransforms();
        _hasArrangedFrames = true;
        ApplyAnimationState(_currentAnimationState);
        UpdateProgressLine();

        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _isAttached = true;
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (_frameScheduler is not null)
        {
            _animationScheduler = new UiAnimationScheduler(_frameScheduler);
        }
        else if (topLevel is not null)
        {
            _ownedFrameScheduler = new AvaloniaUiFrameScheduler(topLevel);
            _animationScheduler = new UiAnimationScheduler(_ownedFrameScheduler);
        }

        ApplyTerminalState(HasSource);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        CancelAnimation();
        _animationScheduler = null;
        _ownedFrameScheduler?.Dispose();
        _ownedFrameScheduler = null;

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SplitProgressProperty)
        {
            ApplyAnimationState(GetOpeningAnimationState(GetOpeningElapsed(SplitProgress)));
        }

        if ((change.Property == IsRenderingProperty)
            || (change.Property == ProgressBrushProperty)
            || (change.Property == ProgressHighlightBrushProperty))
        {
            UpdateProgressLine();
        }
    }

    private static Size NormalizeSize(Size size)
    {
        return new Size(
            double.IsFinite(size.Width) ? Math.Max(0d, size.Width) : 0d,
            double.IsFinite(size.Height) ? Math.Max(0d, size.Height) : 0d);
    }

    private static double Lerp(double from, double to, double progress)
    {
        return from + ((to - from) * progress);
    }

    private static Rect Lerp(Rect from, Rect to, double progress)
    {
        return new Rect(
            Lerp(from.X, to.X, progress),
            Lerp(from.Y, to.Y, progress),
            Lerp(from.Width, to.Width, progress),
            Lerp(from.Height, to.Height, progress));
    }

    private static double SmoothStep(double progress)
    {
        double value = Math.Clamp(progress, 0d, 1d);

        return value * value * (3d - (2d * value));
    }

    private static double GetCardEasing(double progress)
    {
        return MotionEasing.CubicBezier(progress, 0.3d, 0d, 0.2d, 1d);
    }

    private static (double Scale, double Rotation) GetDealPose(double progress)
    {
        if (progress <= DealKeyFrameOffset)
        {
            double keyFrameProgress = progress / DealKeyFrameOffset;

            return (
                Lerp(1d, DealScale, keyFrameProgress),
                Lerp(0d, DealRotation, keyFrameProgress));
        }

        double settleProgress = (progress - DealKeyFrameOffset) / (1d - DealKeyFrameOffset);

        return (
            Lerp(DealScale, 1d, settleProgress),
            Lerp(DealRotation, 0d, settleProgress));
    }

    private static TimeSpan GetOpeningElapsed(double splitProgress)
    {
        double normalizedProgress = double.IsFinite(splitProgress)
            ? Math.Clamp(splitProgress, 0d, 1d)
            : 0d;

        return TimeSpan.FromMilliseconds(normalizedProgress * OpeningDurationMilliseconds);
    }

    private static void ApplyVisualRect(
        AnimatedTransformState transform,
        Rect arrangedRect,
        Rect visualRect,
        double additionalScale = 1d,
        double rotation = 0d)
    {
        double scaleX = arrangedRect.Width > 0d
            ? visualRect.Width / arrangedRect.Width
            : 1d;
        double scaleY = arrangedRect.Height > 0d
            ? visualRect.Height / arrangedRect.Height
            : 1d;
        transform.Scale.ScaleX = scaleX * additionalScale;
        transform.Scale.ScaleY = scaleY * additionalScale;
        transform.Rotate.Angle = rotation;
        transform.Translate.X = visualRect.Center.X - arrangedRect.Center.X;
        transform.Translate.Y = visualRect.Center.Y - arrangedRect.Center.Y;
    }

    private void EnsureTransforms()
    {
        if (_sourceTransform is not null
            && _resultTransform is not null
            && _placeholderTransform is not null)
        {
            return;
        }

        Control source = Children[SourceChildIndex];
        Control result = Children[ResultChildIndex];
        Control placeholder = Children[PlaceholderChildIndex];
        _sourceTransform = AnimatedTransformState.GetOrCreate(source);
        _resultTransform = AnimatedTransformState.GetOrCreate(result);
        _placeholderTransform = AnimatedTransformState.GetOrCreate(placeholder);
    }

    private void ApplyAnimationState(Dlss5PanelAnimationState state)
    {
        _currentAnimationState = state;
        if (!_hasArrangedFrames || (Children.Count != ExpectedChildCount))
        {
            return;
        }

        EnsureTransforms();
        if (_sourceTransform is null
            || _resultTransform is null
            || _placeholderTransform is null)
        {
            return;
        }

        Control source = Children[SourceChildIndex];
        Control result = Children[ResultChildIndex];
        Control placeholder = Children[PlaceholderChildIndex];
        Rect placeholderRect = new(Bounds.Size);
        Rect sourceRect = source.Bounds;
        Rect resultRect = result.Bounds;
        Rect firstCardRect = Lerp(placeholderRect, sourceRect, state.FirstCardProgress);
        Rect dealtCardRect = Lerp(sourceRect, resultRect, state.ResultCardProgress);
        ApplyVisualRect(_sourceTransform, sourceRect, firstCardRect);
        ApplyVisualRect(_placeholderTransform, placeholderRect, firstCardRect);
        ApplyVisualRect(
            _resultTransform,
            resultRect,
            dealtCardRect,
            state.ResultScale,
            state.ResultRotation);

        source.Opacity = state.SourceOpacity;
        source.IsHitTestVisible = HasSource
            && !_isTransitioning
            && state.SourceOpacity == 1d
            && state.FirstCardProgress == 1d;
        result.Opacity = state.ResultOpacity;
        result.IsHitTestVisible = source.IsHitTestVisible;
        placeholder.Opacity = state.PlaceholderOpacity;
        placeholder.IsHitTestVisible = !HasSource
            && !_isTransitioning
            && state.PlaceholderOpacity == 1d
            && state.FirstCardProgress == 0d;
    }

    private void UpdateProgressLine()
    {
        _progressLine.BaseBrush = ProgressBrush;
        _progressLine.HighlightBrush = ProgressHighlightBrush;

        bool showsProgress = IsRendering && !_isTransitioning;
        _progressLine.IsActive = showsProgress;
        _progressLine.Opacity = showsProgress ? 1d : 0d;
    }

    private void ApplyTerminalState(bool showsComparison)
    {
        SplitProgress = showsComparison ? 1d : 0d;
        Dlss5PanelAnimationState state = showsComparison
            ? GetOpeningAnimationState(TimeSpan.FromMilliseconds(OpeningDurationMilliseconds))
            : GetClosingAnimationState(TimeSpan.FromMilliseconds(ClosingDurationMilliseconds));
        ApplyAnimationState(state);
    }

    private Task StartAnimation(
        bool isOpening,
        bool completeWhenClosed = false)
    {
        CancelAnimation();
        int animationVersion = _animationVersion;
        UiAnimationScheduler? animationScheduler = _animationScheduler;
        int durationMilliseconds = isOpening
            ? OpeningDurationMilliseconds
            : ClosingDurationMilliseconds;
        if (!_isAttached || animationScheduler is null)
        {
            ApplyTerminalState(isOpening);
            return Task.CompletedTask;
        }

        _isOpeningTransition = isOpening;
        _isTransitioning = true;
        if (isOpening)
        {
            _awaitingSourceRemoval = false;
        }

        _closeAnimationCompletion = completeWhenClosed
            ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            : null;
        UpdateProgressLine();
        SplitProgress = isOpening ? 0d : 1d;
        Task animation = animationScheduler.AnimateValueAsync(
            this,
            0d,
            durationMilliseconds,
            durationMilliseconds,
            0,
            MotionEasing.Linear,
            elapsedMilliseconds => ApplyAnimationFrame(isOpening, elapsedMilliseconds),
            () => CompleteAnimation(animationVersion, isOpening));

        return _closeAnimationCompletion?.Task ?? animation;
    }

    private void ApplyAnimationFrame(bool isOpening, double elapsedMilliseconds)
    {
        ApplyAnimationState(
            isOpening
                ? GetOpeningAnimationState(TimeSpan.FromMilliseconds(elapsedMilliseconds))
                : GetClosingAnimationState(TimeSpan.FromMilliseconds(elapsedMilliseconds)));
    }

    private void CompleteAnimation(int animationVersion, bool isOpening)
    {
        if ((animationVersion != _animationVersion) || (isOpening && !HasSource))
        {
            return;
        }

        _isTransitioning = false;
        ApplyTerminalState(isOpening);
        UpdateProgressLine();
        _closeAnimationCompletion?.TrySetResult();
        _closeAnimationCompletion = null;
    }

    private void CancelAnimation()
    {
        _animationVersion++;
        _animationScheduler?.Cancel(this);
        _closeAnimationCompletion?.TrySetResult();
        _closeAnimationCompletion = null;
        _isTransitioning = false;
        UpdateProgressLine();
    }

    private (Rect Source, Rect Result) GetImageFrames(Size size)
    {
        double gap = Math.Min(ImageGap, size.Width);
        double availableImageWidth = Math.Max(0d, (size.Width - gap) / 2d);
        double availableImageHeight = Math.Max(0d, size.Height - CaptionHeight - ProgressHeight);
        double imageWidth = Math.Min(availableImageWidth, availableImageHeight * _frameRatio);
        double imageHeight = imageWidth / _frameRatio;
        double frameHeight = Math.Min(
            size.Height,
            imageHeight + CaptionHeight + ProgressHeight);
        double left = (size.Width - (2d * imageWidth) - gap) / 2d;
        double top = (size.Height - frameHeight) / 2d;
        Rect source = new(left, top, imageWidth, frameHeight);
        Rect result = new(left + imageWidth + gap, top, imageWidth, frameHeight);

        return (source, result);
    }

    private static void OnHasSourceChanged(
        Dlss5ComparisonPanel panel,
        AvaloniaPropertyChangedEventArgs args)
    {
        bool hasSource = args.NewValue is true;
        if (!hasSource && panel._awaitingSourceRemoval)
        {
            panel._awaitingSourceRemoval = false;
            if (!panel._isTransitioning)
            {
                panel.ApplyTerminalState(false);
                panel.UpdateProgressLine();
            }

            return;
        }

        if (!hasSource && panel._isTransitioning && !panel._isOpeningTransition)
        {
            return;
        }

        _ = panel.StartAnimation(hasSource);
    }

    private static void OnSourceSizeChanged(
        Dlss5ComparisonPanel panel,
        AvaloniaPropertyChangedEventArgs args)
    {
        if ((panel.SourceWidth > 0d) && (panel.SourceHeight > 0d))
        {
            panel._frameRatio = panel.SourceWidth / panel.SourceHeight;
        }
    }
}
