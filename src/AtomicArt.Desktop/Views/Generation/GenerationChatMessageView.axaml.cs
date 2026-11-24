using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Views.Generation;

public partial class GenerationChatMessageView : UserControl
{
    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }
    public double ReferenceRevealProgress => _referenceRevealProgress;
    public double ArrivalProgress => _arrivalProgress;
    public double ToolsRevealProgress => _toolsRevealProgress;
    public double EditorProgress => _editorProgress;
    public bool IsEditorPresented => _isEditorPresented;
    public double ToolsOpacity => ToolsRevealProgress * (1d - EditorProgress);
    public bool IsLayoutAnimating => EditorProgress is > 0d and < 1d || ToolsRevealProgress is > 0d and < 1d;

    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<GenerationChatMessageView, bool>(nameof(IsEditing));
    public static readonly DirectProperty<GenerationChatMessageView, double> ReferenceRevealProgressProperty =
        AvaloniaProperty.RegisterDirect<GenerationChatMessageView, double>(
            nameof(ReferenceRevealProgress), view => view.ReferenceRevealProgress);
    public static readonly DirectProperty<GenerationChatMessageView, double> ArrivalProgressProperty =
        AvaloniaProperty.RegisterDirect<GenerationChatMessageView, double>(nameof(ArrivalProgress), view => view.ArrivalProgress);
    public static readonly DirectProperty<GenerationChatMessageView, double> ToolsRevealProgressProperty =
        AvaloniaProperty.RegisterDirect<GenerationChatMessageView, double>(nameof(ToolsRevealProgress), view => view.ToolsRevealProgress);
    public static readonly DirectProperty<GenerationChatMessageView, double> EditorProgressProperty =
        AvaloniaProperty.RegisterDirect<GenerationChatMessageView, double>(nameof(EditorProgress), view => view.EditorProgress);
    public static readonly DirectProperty<GenerationChatMessageView, bool> IsEditorPresentedProperty =
        AvaloniaProperty.RegisterDirect<GenerationChatMessageView, bool>(nameof(IsEditorPresented), view => view.IsEditorPresented);
    public static readonly DirectProperty<GenerationChatMessageView, double> ToolsOpacityProperty =
        AvaloniaProperty.RegisterDirect<GenerationChatMessageView, double>(nameof(ToolsOpacity), view => view.ToolsOpacity);
    public static readonly DirectProperty<GenerationChatMessageView, bool> IsLayoutAnimatingProperty =
        AvaloniaProperty.RegisterDirect<GenerationChatMessageView, bool>(nameof(IsLayoutAnimating), view => view.IsLayoutAnimating);

    internal const string MessageArrivalDurationResourceKey = "GenerationChatMessageArrivalDuration";

    private const string ToolsRevealDurationResourceKey = "GenerationOptionResetHoverDuration";


    private AvaloniaUiFrameScheduler? _frames;
    private UiAnimationScheduler? _animation;
    private ScrollViewer? _scrollAnchor;
    private double _referenceRevealProgress;
    private double _arrivalProgress = 1d;
    private double _toolsRevealProgress;
    private double _editorProgress;
    private bool _isEditorPresented;

    public GenerationChatMessageView()
    {
        InitializeComponent();
        AddHandler(RequestBringIntoViewEvent, OnEditorBringIntoViewRequested);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _scrollAnchor?.UnregisterAnchorCandidate(this);
        _scrollAnchor = this.FindAncestorOfType<ScrollViewer>();
        _scrollAnchor?.RegisterAnchorCandidate(this);

        if (DataContext is GenerationChatMessageViewModel { AnimateInsertion: true } message)
        {
            message.AnimateInsertion = false;
            Animate(this, ArrivalProgress, 1d, MessageArrivalDurationResourceKey, SetArrivalProgress);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _scrollAnchor?.UnregisterAnchorCandidate(this);
        _scrollAnchor = null;
        _animation?.Cancel(new Control[] { this, MessageEditor, MessageTools });
        _animation = null;
        _frames?.Dispose();
        _frames = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _animation?.Cancel(new Control[] { this, MessageEditor, MessageTools });
        SetArrivalProgress(DataContext is GenerationChatMessageViewModel { AnimateInsertion: true } ? 0d : 1d);
        SetEditorProgress(IsEditing ? 1d : 0d);
        SetReferenceRevealProgress(GetReferenceTarget());
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IsEditingProperty)
        {
            return;
        }

        double fromReferences = ReferenceRevealProgress;
        double targetReferences = GetReferenceTarget();
        double fromEditor = EditorProgress;
        double targetEditor = IsEditing ? 1d : 0d;

        if (IsEditing)
        {
            SetAndRaise(IsEditorPresentedProperty, ref _isEditorPresented, true);
        }

        Animate(MessageEditor, 0d, 1d, "GenerationChatEditorFadeDuration", progress =>
                {
                    SetReferenceRevealProgress(fromReferences + ((targetReferences - fromReferences) * progress));
                    SetEditorProgress(fromEditor + ((targetEditor - fromEditor) * progress));
                });
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Animate(MessageTools, ToolsRevealProgress, 1d, ToolsRevealDurationResourceKey, SetToolsRevealProgress);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Animate(MessageTools, ToolsRevealProgress, 0d, ToolsRevealDurationResourceKey, SetToolsRevealProgress);
    }

    private void Animate(Control target, double from, double to, string durationKey, Action<double> update)
    {
        _animation?.Cancel(target);

        if (TopLevel.GetTopLevel(this) is not { } topLevel || !IsLoaded)
        {
            update(to);

            return;
        }

        _frames ??= new AvaloniaUiFrameScheduler(topLevel);
        _animation ??= new UiAnimationScheduler(_frames);
        int duration = this.FindResource(durationKey) is TimeSpan fade ? (int)fade.TotalMilliseconds : 0;
        this.FindAncestorOfType<GenerationChatView>()?.TrackMessageResize(this, TimeSpan.FromMilliseconds(duration));
        _ = _animation.AnimateValueAsync(target, from, to, duration, 0, MotionEasing.EaseRail, update);
    }

    private double GetReferenceTarget()
    {
        return DataContext is GenerationChatMessageViewModel { ShowsReferenceStrip: true } ? 1d : 0d;
    }

    private void SetArrivalProgress(double progress)
    {
        SetAndRaise(ArrivalProgressProperty, ref _arrivalProgress, progress);
    }

    private void SetEditorProgress(double progress)
    {
        double previousOpacity = ToolsOpacity;
        bool wasAnimating = IsLayoutAnimating;
        SetAndRaise(EditorProgressProperty, ref _editorProgress, progress);
        SetAndRaise(IsEditorPresentedProperty, ref _isEditorPresented, IsEditing || progress > 0d);
        RaisePropertyChanged(ToolsOpacityProperty, previousOpacity, ToolsOpacity);
        RaisePropertyChanged(IsLayoutAnimatingProperty, wasAnimating, IsLayoutAnimating);
    }

    private void SetToolsRevealProgress(double progress)
    {
        double previousOpacity = ToolsOpacity;
        bool wasAnimating = IsLayoutAnimating;
        SetAndRaise(ToolsRevealProgressProperty, ref _toolsRevealProgress, progress);
        RaisePropertyChanged(ToolsOpacityProperty, previousOpacity, ToolsOpacity);
        RaisePropertyChanged(IsLayoutAnimatingProperty, wasAnimating, IsLayoutAnimating);

        if (progress == 0d && DataContext is GenerationChatMessageViewModel message)
        {
            message.IsCopied = false;
        }
    }

    private void SetReferenceRevealProgress(double progress)
    {
        SetAndRaise(ReferenceRevealProgressProperty, ref _referenceRevealProgress, progress);
    }

    private void OnEditorBringIntoViewRequested(object? sender, RequestBringIntoViewEventArgs e)
    {
        if (e.TargetObject is { } target && (ReferenceEquals(target, MessageEditor)
            || target.GetVisualAncestors().Contains(MessageEditor)))
        {
            e.Handled = true;
        }
    }
}
