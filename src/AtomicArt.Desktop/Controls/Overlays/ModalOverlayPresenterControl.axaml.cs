using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Overlays;

public partial class ModalOverlayPresenterControl : UserControl
{
    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }
    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }
    public int Order
    {
        get => GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<ModalOverlayPresenterControl, object?>(nameof(Body));
    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<ModalOverlayPresenterControl, ICommand?>(nameof(CloseCommand));
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModalOverlayPresenterControl, bool>(nameof(IsOpen));
    public static readonly StyledProperty<int> OrderProperty =
        AvaloniaProperty.Register<ModalOverlayPresenterControl, int>(nameof(Order));

    private const int BackdropAnimationDurationMilliseconds = 100;
    private const int PanelAnimationDurationMilliseconds = 160;
    private const double PanelHiddenOffsetY = 16d;
    private const double PanelHiddenScale = 0.98d;

    private static readonly MotionFrame BackdropHiddenFrame = new(0d, 0d, 1d, 0d, 0d);
    private static readonly MotionFrame PanelHiddenFrame = new(
        0d,
        PanelHiddenOffsetY,
        PanelHiddenScale,
        0d,
        0d);

    private readonly IUiFrameScheduler? _frameScheduler;
    private UiAnimationScheduler? _animationScheduler;
    private ModalOverlayTransitionSnapshot? _transitionSnapshot;
    private int _animationVersion;
    private bool _isAttached;

    public ModalOverlayPresenterControl()
        : this(null)
    {
    }

    internal ModalOverlayPresenterControl(IUiFrameScheduler? frameScheduler)
    {
        _frameScheduler = frameScheduler;
        InitializeComponent();
        IsVisible = false;
        ApplyOrder();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _isAttached = true;
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        _animationScheduler = topLevel is null
            ? null
            : new UiAnimationScheduler(
                _frameScheduler ?? new AvaloniaUiFrameScheduler(topLevel));
        ApplyOrder();
        ApplyOpenState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        CancelAnimations();
        ReleaseTransitionSnapshot();
        _animationScheduler = null;

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OrderProperty)
        {
            ApplyOrder();
        }

        if ((change.Property == IsOpenProperty) && _isAttached)
        {
            ApplyOpenState();
        }
    }

    private void ApplyOpenState()
    {
        if (IsOpen)
        {
            StartOpeningAnimation();
            return;
        }

        StartClosingAnimation();
    }

    private void ApplyOrder()
    {
        ZIndex = Order;
    }

    private int BeginAnimation()
    {
        CancelAnimations();

        return _animationVersion;
    }

    private void CancelAnimations()
    {
        _animationVersion++;
        _animationScheduler?.Cancel(
            new Control[] { Backdrop, BodyPresenter, BodyTransitionSnapshotHost });
    }

    private void CompleteClosingAnimation(int animationVersion)
    {
        if ((animationVersion != _animationVersion) || IsOpen)
        {
            return;
        }

        ReleaseTransitionSnapshot();
        IsVisible = false;
    }

    private void StartClosingAnimation()
    {
        if (!IsVisible)
        {
            return;
        }

        int animationVersion = BeginAnimation();
        BodyPresenter.IsHitTestVisible = false;
        Backdrop.IsEnabled = false;

        UiAnimationScheduler? animationScheduler = _animationScheduler;
        if (animationScheduler is null)
        {
            CompleteClosingAnimation(animationVersion);
            return;
        }

        MotionFrameApplier.Apply(BodyPresenter, MotionFrame.Identity);
        Control panelAnimationTarget = PrepareClosingAnimationTarget();
        _ = animationScheduler.AnimateAsync(
            Backdrop,
            new MotionFrame[] { MotionFrame.Identity, BackdropHiddenFrame },
            BackdropAnimationDurationMilliseconds,
            0,
            MotionEasing.EaseOut);
        _ = animationScheduler.AnimateAsync(
            panelAnimationTarget,
            new MotionFrame[] { MotionFrame.Identity, PanelHiddenFrame },
            PanelAnimationDurationMilliseconds,
            0,
            MotionEasing.EaseOut,
            () => CompleteClosingAnimation(animationVersion));
    }

    private void StartOpeningAnimation()
    {
        CancelAnimations();
        ReleaseTransitionSnapshot();
        IsVisible = true;
        BodyPresenter.IsHitTestVisible = true;
        Backdrop.IsEnabled = true;

        UiAnimationScheduler? animationScheduler = _animationScheduler;
        if (animationScheduler is null)
        {
            MotionFrameApplier.Apply(Backdrop, MotionFrame.Identity);
            MotionFrameApplier.Apply(BodyPresenter, MotionFrame.Identity);
            return;
        }

        _ = animationScheduler.AnimateAsync(
            Backdrop,
            new MotionFrame[] { BackdropHiddenFrame, MotionFrame.Identity },
            BackdropAnimationDurationMilliseconds,
            0,
            MotionEasing.EaseOut);
        _ = animationScheduler.AnimateAsync(
            BodyPresenter,
            new MotionFrame[] { PanelHiddenFrame, MotionFrame.Identity },
            PanelAnimationDurationMilliseconds,
            0,
            MotionEasing.EaseMaterial);
    }

    private Control PrepareClosingAnimationTarget()
    {
        ReleaseTransitionSnapshot();

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        ModalOverlayControl? panel = BodyPresenter
            .GetVisualDescendants()
            .OfType<ModalOverlayControl>()
            .SingleOrDefault();
        if (topLevel is null || panel is null)
        {
            return BodyPresenter;
        }

        try
        {
            _transitionSnapshot = ModalOverlayTransitionSnapshot.Create(
                topLevel,
                panel,
                this,
                BodyTransitionSnapshotHost,
                BodyTransitionSnapshotClip,
                BodyTransitionSnapshot);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(
                this,
                "Failed to create modal overlay transition snapshot: {Exception}",
                ex);
        }

        if (_transitionSnapshot is null)
        {
            return BodyPresenter;
        }

        BodyPresenter.IsVisible = false;

        return BodyTransitionSnapshotHost;
    }

    private void ReleaseTransitionSnapshot()
    {
        _transitionSnapshot?.Dispose();
        _transitionSnapshot = null;
        BodyPresenter.IsVisible = true;
    }
}
