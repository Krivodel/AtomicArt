using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls;

public sealed class PanZoomControl : Border
{
    public IRelayCommand ZoomInCommand { get; }
    public IRelayCommand ZoomOutCommand { get; }
    public IRelayCommand FitCommand { get; }
    public double Zoom => _zoom;
    public Rect VisibleContentBounds => new(-_offset.X / _zoom, -_offset.Y / _zoom, Bounds.Width / _zoom, Bounds.Height / _zoom);
    public bool IsInteracting
    {
        get => _isInteracting;
        private set => SetAndRaise(IsInteractingProperty, ref _isInteracting, value);
    }

    public static readonly DirectProperty<PanZoomControl, bool> IsInteractingProperty =
        AvaloniaProperty.RegisterDirect<PanZoomControl, bool>(nameof(IsInteracting), control => control.IsInteracting);

    public event EventHandler? CameraChanged;

    internal IUiFrameScheduler? FrameScheduler { get; init; }
    internal TimeProvider Clock { get; init; } = TimeProvider.System;

    private const double MinimumZoom = 0.08d;
    private const double MaximumZoom = 3d;
    private const double ZoomStep = 1.2d;
    private const double FitPadding = 32d;
    private const double MotionResponsiveness = 54d;
    private const double MaximumMotionFrameSeconds = 0.05d;
    private const double MotionStopDistance = 0.1d;
    private const double ZoomStopRatio = 0.0001d;

    private readonly MatrixTransform _transform = new();
    private Size _contentSize;
    private Size _viewportSize;
    private Vector _offset;
    private Point? _dragStart;
    private Vector _dragOffset;
    private double _zoom = 1d;
    private double _targetZoom = 1d;
    private Vector _targetOffset;
    private IUiFrameScheduler? _frames;
    private AvaloniaUiFrameScheduler? _ownedFrames;
    private long? _lastMotionTimestamp;
    private long _motionVersion;
    private bool _motionFramePending;
    private bool _isInteracting;
    private bool _isDragging;

    public PanZoomControl()
    {
        ClipToBounds = true;
        Focusable = true;
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
        FitCommand = new RelayCommand(Fit);
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel, true);
        AddHandler(PointerMovedEvent, OnPointerMovedTunnel, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, OnPointerReleasedTunnel, RoutingStrategies.Tunnel, true);
    }

    public void Fit()
    {
        if (Child is null || _contentSize.Width <= 0d || _contentSize.Height <= 0d)
        {
            return;
        }

        AnimateCamera(GetFittedCamera());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (FrameScheduler is { } frames)
        {
            _frames = frames;
        }
        else if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            _ownedFrames = new AvaloniaUiFrameScheduler(topLevel);
            _frames = _ownedFrames;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopMotion();
        _dragStart = null;
        _isDragging = false;
        _frames = null;
        _ownedFrames?.Dispose();
        _ownedFrames = null;
        IsInteracting = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Child?.Measure(Size.Infinity);

        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : Child?.DesiredSize.Width ?? 0d,
            double.IsFinite(availableSize.Height) ? availableSize.Height : Child?.DesiredSize.Height ?? 0d);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size contentSize = Child?.DesiredSize ?? default;
        Child?.Arrange(new Rect(contentSize));

        if (contentSize != _contentSize || finalSize != _viewportSize)
        {
            bool fit = _contentSize == default || finalSize != _viewportSize;
            _contentSize = contentSize;
            _viewportSize = finalSize;

            if (fit)
            {
                StopMotion();
                MotionFrame fitted = GetFittedCamera();
                _targetZoom = fitted.Scale;
                _targetOffset = new Vector(fitted.X, fitted.Y);
                ApplyCamera(fitted);
                IsInteracting = _dragStart is not null;
            }
        }

        return finalSize;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        if (ReferenceEquals(e.Source, this))
        {
            EndDrag();
        }

        base.OnPointerCaptureLost(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ChangeZoom(Math.Pow(ZoomStep, e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    private void ZoomIn()
    {
        ChangeZoom(ZoomStep, new Point(Bounds.Width / 2d, Bounds.Height / 2d));
    }

    private void ZoomOut()
    {
        ChangeZoom(1d / ZoomStep, new Point(Bounds.Width / 2d, Bounds.Height / 2d));
    }

    private void ChangeZoom(double factor, Point anchor)
    {
        if (factor == 1d)
        {
            return;
        }

        double minimumZoom = Math.Min(MinimumZoom, GetFittedCamera().Scale);
        double basis = ((_targetZoom - _zoom) * (factor - 1d)) < 0d ? _zoom : _targetZoom;
        double zoom = Math.Clamp(basis * factor, minimumZoom, MaximumZoom);

        if (zoom == _targetZoom)
        {
            return;
        }

        Vector offset = new(anchor.X - (anchor.X - _offset.X) * zoom / _zoom,
            anchor.Y - (anchor.Y - _offset.Y) * zoom / _zoom);
        MotionFrame target = new(offset.X, offset.Y, zoom, 0d, 1d);

        if (_dragStart is not null)
        {
            StopMotion();
            ApplyCamera(target);
            _dragStart = anchor;
            _dragOffset = _offset;
            _targetOffset = _offset;
            _targetZoom = _zoom;
        }
        else
        {
            AnimateCamera(target);
        }
    }

    private MotionFrame GetFittedCamera()
    {
        double zoom = Math.Min(1d, Math.Min(
            Math.Max(1d, _viewportSize.Width - FitPadding) / Math.Max(1d, _contentSize.Width),
            Math.Max(1d, _viewportSize.Height - FitPadding) / Math.Max(1d, _contentSize.Height)));
        return new MotionFrame((_viewportSize.Width - _contentSize.Width * zoom) / 2d,
            (_viewportSize.Height - _contentSize.Height * zoom) / 2d, zoom, 0d, 1d);
    }

    private void AnimateCamera(MotionFrame target)
    {
        _targetZoom = target.Scale;
        _targetOffset = new Vector(target.X, target.Y);

        if (_frames is null)
        {
            ApplyCamera(target);
            IsInteracting = false;
            return;
        }

        RequestMotionFrame();
    }

    private void ApplyCamera(MotionFrame frame)
    {
        _zoom = frame.Scale;
        _offset = new Vector(frame.X, frame.Y);
        ApplyTransform();
    }

    private void StopMotion()
    {
        _motionVersion++;
        _motionFramePending = false;
        _lastMotionTimestamp = null;
        _targetZoom = _zoom;
        _targetOffset = _offset;
    }

    private void EndDrag()
    {
        _dragStart = null;
        _isDragging = false;

        if ((_targetOffset - _offset).Length > MotionStopDistance)
        {
            RequestMotionFrame();
        }
        else
        {
            IsInteracting = false;
        }
    }

    private void RequestMotionFrame()
    {
        if (_motionFramePending || _frames is null)
        {
            return;
        }

        IsInteracting = true;
        _lastMotionTimestamp ??= Clock.GetTimestamp();
        _motionFramePending = true;
        long version = _motionVersion;
        _frames.RequestAnimationFrame(_ => OnMotionFrame(version));
    }

    private void ApplyTransform()
    {
        if (Child is not null)
        {
            Matrix matrix = new(_zoom, 0d, 0d, _zoom, _offset.X, _offset.Y);
            bool changed = _transform.Matrix != matrix;

            if (changed)
            {
                _transform.Matrix = matrix;
            }

            if (!ReferenceEquals(Child.RenderTransform, _transform))
            {
                Child.RenderTransformOrigin = RelativePoint.TopLeft;
                Child.RenderTransform = _transform;
                changed = true;
            }

            if (changed)
            {
                CameraChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsLeftButtonPressed || properties.IsMiddleButtonPressed)
        {
            StopMotion();
            _dragStart = e.GetPosition(this);
            _dragOffset = _offset;
            _targetOffset = _offset;
            _targetZoom = _zoom;
            bool onButton = e.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<Button>().Any();
            _isDragging = properties.IsMiddleButtonPressed || !onButton;

            if (_isDragging)
            {
                IsInteracting = true;
                e.Pointer.Capture(this);
                Focus();
                e.Handled = true;
            }
        }
    }

    private void OnPointerMovedTunnel(object? sender, PointerEventArgs e)
    {
        if (_dragStart is Point start)
        {
            if (!_isDragging)
            {
                if (!PointerDragThreshold.IsReached(start, e.GetPosition(this)))
                {
                    return;
                }

                _isDragging = true;
                e.Pointer.Capture(this);
                Focus();
            }

            _targetOffset = _dragOffset + (e.GetPosition(this) - start);
            RequestMotionFrame();
            e.Handled = true;
        }
    }

    private void OnPointerReleasedTunnel(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragStart is not null)
        {
            bool wasDragging = _isDragging;
            EndDrag();

            if (wasDragging)
            {
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }
    }

    private void OnMotionFrame(long version)
    {
        if (version != _motionVersion || !_motionFramePending || _lastMotionTimestamp is not { } previous)
        {
            return;
        }

        _motionFramePending = false;
        long now = Clock.GetTimestamp();
        double elapsed = Clock.GetElapsedTime(previous, now).TotalSeconds;
        _lastMotionTimestamp = now;
        double interpolation = _isDragging
            ? 1d
            : 1d - Math.Exp(-MotionResponsiveness * Math.Clamp(elapsed, 0d, MaximumMotionFrameSeconds));
        _zoom += (_targetZoom - _zoom) * interpolation;
        _offset += (_targetOffset - _offset) * interpolation;
        bool hasRemainingMotion = (_targetOffset - _offset).Length > MotionStopDistance
            || Math.Abs(_targetZoom - _zoom) > _targetZoom * ZoomStopRatio;

        if (!hasRemainingMotion)
        {
            _zoom = _targetZoom;
            _offset = _targetOffset;
            _lastMotionTimestamp = null;
        }

        ApplyTransform();

        if (hasRemainingMotion)
        {
            RequestMotionFrame();
        }
        else
        {
            IsInteracting = _dragStart is not null;
        }
    }
}
