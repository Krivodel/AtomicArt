using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Views.Notifications;

public partial class GenerationNotificationWindow : Window
{
    internal Guid NotificationId { get; } = Guid.NewGuid();
    internal Task DismissalRequested => _dismissal.Task;
    internal Task OpeningAnimation => _openingAnimation;
    internal bool IsHovered => NotificationCard.IsPointerOver;
    internal bool IsDismissalInProgress => _closingAnimation is not null;
    internal bool IsNotificationClosed => _closed;
    internal Size CardSize => NotificationCard.DesiredSize.Deflate(NotificationCard.Margin);
    internal Thickness CardMargin => NotificationCard.Margin;

    internal event EventHandler? NotificationClicked;
    internal event EventHandler? HoverChanged;

    private const int EnterDurationMilliseconds = 340;
    private const int SettleDurationMilliseconds = 120;
    private const int CloseDurationMilliseconds = 270;
    private const int PlacementDurationMilliseconds = 240;

    private static readonly MotionFrame EnterFrame = new(0, 28, 0.94, -1.8, 0);
    private static readonly MotionFrame OvershootFrame = new(0, -2, 1.003, 0.35, 1);
    private static readonly MotionFrame ExitFrame = new(0, 16.8, 0.96, 1.2, 0);

    private readonly TaskCompletionSource _dismissal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IUiFrameScheduler? _providedFrameScheduler;
    private AvaloniaUiFrameScheduler? _ownedFrameScheduler;
    private UiAnimationScheduler? _animations;
    private Task _openingAnimation = Task.CompletedTask;
    private Task? _closingAnimation;
    private bool _closed;

    public GenerationNotificationWindow()
        : this(null)
    {
    }

    internal GenerationNotificationWindow(IUiFrameScheduler? frameScheduler)
    {
        _providedFrameScheduler = frameScheduler;
        InitializeComponent();
        Win32Properties.AddWindowStylesCallback(this, AddNotificationStyles);
        Win32Properties.AddWndProcHookCallback(this, OnNativeMessage);
        NotificationCard.PointerEntered += OnHoverChanged;
        NotificationCard.PointerExited += OnHoverChanged;
        NotificationCard.PointerReleased += OnCardPointerReleased;
        Closed += OnClosed;
        MotionFrameApplier.Apply(NotificationCard, EnterFrame);
        NotificationCard.RenderTransformOrigin = RelativePoint.BottomRight;
    }

    internal static (uint Style, uint ExtendedStyle) AddNotificationStyles(uint style, uint extendedStyle)
    {
        const uint NoActivate = 0x08000000;
        const uint ToolWindow = 0x00000080;
        return (style, extendedStyle | NoActivate | ToolWindow);
    }

    internal static nint PreventMouseActivation(nint window, uint message,
        nint parameter, nint data, ref bool handled)
    {
        const uint MouseActivate = 0x0021;
        const int DoNotActivate = 3;

        if (message == MouseActivate)
        {
            handled = true;
            return DoNotActivate;
        }

        return 0;
    }

    internal void PrepareLayout(double maximumWidth, double maximumHeight)
    {
        Width = Math.Min(Width, maximumWidth);
        NotificationCard.Measure(new Size(Width, maximumHeight + NotificationCard.Margin.Top + NotificationCard.Margin.Bottom));
        Height = NotificationCard.DesiredSize.Height;
    }

    internal void RequestClose()
    {
        _dismissal.TrySetResult();
    }

    internal void ShowAnimated()
    {
        EnsureAnimations();
        Show();
        _openingAnimation = OpenAsync();
    }

    internal Task CloseAnimatedAsync()
    {
        if (_closingAnimation is not null)
        {
            return _closingAnimation;
        }

        NotificationCard.IsHitTestVisible = false;
        _animations?.Cancel(NotificationCard);
        _animations?.Cancel(this);
        _closingAnimation = CloseCoreAsync();
        return _closingAnimation;
    }

    internal void MoveTo(PixelPoint target)
    {
        if (_closingAnimation is not null)
        {
            return;
        }

        if (!IsVisible || _animations is null)
        {
            Position = target;
            return;
        }

        _animations.Cancel(this);
        PixelPoint start = Position;

        if (start == target)
        {
            return;
        }

        _ = _animations.AnimateValueAsync(this, 0, 1, PlacementDurationMilliseconds, 0,
            MotionEasing.EaseMaterial, progress => Position = new PixelPoint(
                (int)Math.Round(start.X + (target.X - start.X) * progress),
                (int)Math.Round(start.Y + (target.Y - start.Y) * progress)));
    }

    private static double EntryEasing(double progress)
    {
        return MotionEasing.CubicBezier(progress, 0.22, 0.72, 0.28, 1);
    }

    private static double ExitEasing(double progress)
    {
        return MotionEasing.CubicBezier(progress, 0.4, 0, 0.8, 0.5);
    }

    private void EnsureAnimations()
    {
        if (_animations is not null)
        {
            return;
        }

        _ownedFrameScheduler = _providedFrameScheduler is null ? new AvaloniaUiFrameScheduler(this) : null;
        _animations = new UiAnimationScheduler(_providedFrameScheduler ?? _ownedFrameScheduler
            ?? throw new InvalidOperationException("The notification frame scheduler is unavailable."));
    }

    private async Task OpenAsync()
    {
        UiAnimationScheduler animations = _animations
            ?? throw new InvalidOperationException("The notification animation scheduler is unavailable.");
        await animations.AnimateAsync(NotificationCard, new[] { EnterFrame, OvershootFrame },
            EnterDurationMilliseconds, 0, EntryEasing);

        if (!_closed && _closingAnimation is null)
        {
            await animations.AnimateAsync(NotificationCard, new[] { OvershootFrame, MotionFrame.Identity },
                SettleDurationMilliseconds, 0, MotionEasing.EaseOut);
        }
    }

    private async Task CloseCoreAsync()
    {
        if (IsVisible && !_closed && _animations is not null)
        {
            AnimatedTransformState current = AnimatedTransformState.GetOrCreate(NotificationCard);
            MotionFrame frame = new(current.Translate.X, current.Translate.Y, current.Scale.ScaleX,
                current.Rotate.Angle, NotificationCard.Opacity);
            await _animations.AnimateAsync(NotificationCard, new[] { frame, ExitFrame },
                CloseDurationMilliseconds, 0, ExitEasing);
        }

        Close();
    }

    private nint OnNativeMessage(nint window, uint message, nint parameter, nint data, ref bool handled)
    {
        const uint HitTest = 0x0084;
        const int Transparent = -1;
        const int CoordinateBits = 16;

        if (message == HitTest)
        {
            PixelPoint screenPoint = new((short)(long)data, (short)((long)data >> CoordinateBits));
            IInputElement? hit = this.InputHitTest(this.PointToClient(screenPoint));

            if (hit is not Visual visual
                || (!ReferenceEquals(visual, NotificationCard) && !NotificationCard.IsVisualAncestorOf(visual)))
            {
                handled = true;
                return Transparent;
            }
        }

        return PreventMouseActivation(window, message, parameter, data, ref handled);
    }

    private void OnHoverChanged(object? sender, PointerEventArgs e)
    {
        HoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if ((e.InitialPressMouseButton != MouseButton.Left)
            || (e.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<Button>().Any()))
        {
            return;
        }

        e.Handled = true;
        NotificationClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _animations?.Cancel(NotificationCard);
        _animations?.Cancel(this);
        _ownedFrameScheduler?.Dispose();
        _dismissal.TrySetResult();
    }
}
