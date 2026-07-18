using System.ComponentModel;
using System.Diagnostics;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Views.Generation;

public partial class NanoBanana2PanelView : UserControl
{
    private const double LoadingIndicatorSegmentWidthRatio = 0.28d;
    private const double LoadingIndicatorMinimumSegmentWidth = 32d;
    private const double LoadingIndicatorDurationMilliseconds = 1100d;
    private const double AspectRatioHintMaxWidth = 360d;
    private const double AspectRatioHintMaxHeight = 220d;
    private const double AspectRatioHintHiddenOpacity = 0d;
    private const double AspectRatioHintVisibleOpacity = 1d;
    private const double AspectRatioHintHiddenScale = 0.7d;
    private const double AspectRatioHintVisibleScale = 1d;
    private const double AspectRatioHintHiddenOffsetY = -14d;
    private const double AspectRatioHintVisibleOffsetY = 0d;
    private const double TemperatureFlyoutHiddenOpacity = 0d;
    private const double TemperatureFlyoutVisibleOpacity = 1d;
    private const double TemperatureFlyoutHiddenScale = 0.94d;
    private const double TemperatureFlyoutVisibleScale = 1d;
    private const double TemperatureFlyoutHiddenOffsetY = -10d;
    private const double TemperatureFlyoutVisibleOffsetY = 0d;
    private const double OptionResetFlashHiddenOpacity = 0d;
    private const double OptionResetFlashVisibleOpacity = 0.45d;
    private const double OptionResetFlashCandidateBoundsTolerance = 1d;
    private const double OptionResetFlashMinimumCoverage = 0.65d;

    private static readonly TimeSpan LoadingIndicatorFrameInterval = TimeSpan.FromMilliseconds(16d);
    private static readonly TimeSpan AspectRatioHintAnimationDuration = TimeSpan.FromMilliseconds(180d);
    private static readonly TimeSpan AspectRatioHintSizeAnimationDuration = TimeSpan.FromMilliseconds(220d);
    private static readonly TimeSpan AspectRatioHintAutoCycleInterval = TimeSpan.FromMilliseconds(500d);
    private static readonly TimeSpan TemperatureFlyoutAnimationDuration = TimeSpan.FromMilliseconds(150d);
    private static readonly TimeSpan OptionResetFlashQuickFadeDuration = TimeSpan.FromMilliseconds(80d);
    private static readonly TimeSpan OptionResetFlashFinalFadeDuration = TimeSpan.FromMilliseconds(900d);
    private static readonly Color OptionResetFlashColor = Color.FromRgb(0xD9, 0x2D, 0x20);
    private static readonly IReadOnlyList<OptionResetFlashDescriptor> OptionResetFlashDescriptors =
    [
        new()
            {
                SelectionPropertyName = nameof(UniversalNanoBananaPanelViewModel.SelectedAspectRatio),
                GetComboBox = view => view.AspectRatioComboBox,
                GetState = view => view._aspectRatioResetFlashState,
                SetState = (view, state) => view._aspectRatioResetFlashState = state
            },
            new()
            {
                SelectionPropertyName = nameof(UniversalNanoBananaPanelViewModel.SelectedResolution),
                GetComboBox = view => view.ResolutionComboBox,
                GetState = view => view._resolutionResetFlashState,
                SetState = (view, state) => view._resolutionResetFlashState = state
            },
            new()
            {
                SelectionPropertyName = nameof(UniversalNanoBananaPanelViewModel.GenerationCount),
                GetComboBox = view => view.GenerationCountComboBox,
                GetState = view => view._generationCountResetFlashState,
                SetState = (view, state) => view._generationCountResetFlashState = state
            }
    ];

    private readonly Stopwatch _catalogLoadingStopwatch = new();
    private readonly DispatcherTimer _catalogLoadingTimer;
    private readonly DispatcherTimer _aspectRatioHintAutoCycleTimer;
    private readonly DispatcherTimer _temperaturePopupCloseTimer;
    private readonly ScaleTransform _aspectRatioHintScale = new()
    {
        ScaleX = AspectRatioHintHiddenScale,
        ScaleY = AspectRatioHintHiddenScale
    };
    private readonly TranslateTransform _aspectRatioHintTranslate = new()
    {
        Y = AspectRatioHintHiddenOffsetY
    };
    private readonly ScaleTransform _temperatureFlyoutMotionScale = new()
    {
        ScaleX = TemperatureFlyoutHiddenScale,
        ScaleY = TemperatureFlyoutHiddenScale
    };
    private readonly TranslateTransform _temperatureFlyoutTranslate = new()
    {
        Y = TemperatureFlyoutHiddenOffsetY
    };
    private readonly HashSet<ComboBoxItem> _aspectRatioItems = [];
    private readonly HashSet<ScrollViewer> _aspectRatioScrollViewers = [];

    private bool _isAspectRatioDropDownOpen;
    private bool _targetAspectRatioHintVisible;
    private string _hintAspectRatio = string.Empty;
    private string _hintConcreteAspectRatio = string.Empty;
    private int _autoCycleIndex = -1;
    private CancellationTokenSource? _aspectRatioHintCloseCancellation;
    private TopLevel? _temperaturePopupTopLevel;
    private OptionResetFlashState? _aspectRatioResetFlashState;
    private OptionResetFlashState? _resolutionResetFlashState;
    private OptionResetFlashState? _generationCountResetFlashState;
    private UniversalNanoBananaPanelViewModel? _selectionResetViewModel;
    private ScrollViewer? _lastAspectRatioPointerScrollViewer;
    private Point? _lastAspectRatioPointerScrollViewerPosition;

    public NanoBanana2PanelView()
    {
        InitializeComponent();
        InitializeAspectRatioHintTransform();
        InitializeTemperatureFlyout();
        _catalogLoadingTimer = new DispatcherTimer
        {
            Interval = LoadingIndicatorFrameInterval
        };
        _catalogLoadingTimer.Tick += OnCatalogLoadingTimerTick;
        _aspectRatioHintAutoCycleTimer = new DispatcherTimer
        {
            Interval = AspectRatioHintAutoCycleInterval
        };
        _aspectRatioHintAutoCycleTimer.Tick += OnAspectRatioHintAutoCycleTimerTick;
        _temperaturePopupCloseTimer = new DispatcherTimer
        {
            Interval = TemperatureFlyoutAnimationDuration
        };
        _temperaturePopupCloseTimer.Tick += OnTemperaturePopupCloseTimerTick;
        CatalogLoadingIndicatorHost.PropertyChanged += OnCatalogLoadingIndicatorHostPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeSelectionResetEvents();
        StopCatalogLoadingAnimation();
        StopAspectRatioHintAnimation();
        StopTemperatureFlyoutAnimation();
        StopOptionResetFlashAnimations();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is UniversalNanoBananaPanelViewModel viewModel)
        {
            SubscribeSelectionResetEvents(viewModel);
            return;
        }

        UnsubscribeSelectionResetEvents();
    }

    private async void OnPromptLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is UniversalNanoBananaPanelViewModel viewModel
            && viewModel.CommitPromptCommand.CanExecute(null))
        {
            await viewModel.CommitPromptCommand.ExecuteAsync(null);
        }
    }

    private void OnTemperatureButtonClick(object? sender, RoutedEventArgs e)
    {
        if (TemperaturePopup.IsOpen)
        {
            BeginTemperaturePopupClose();
            return;
        }

        CancelTemperatureFlyoutClose();
        SetTemperatureFlyoutHiddenState();
        TemperaturePopup.IsOpen = true;
    }

    private void OnTemperaturePopupOpened(object? sender, EventArgs e)
    {
        AttachTemperaturePopupDismissHandlers();
        SetTemperatureFlyoutVisibleState();
    }

    private void OnTemperaturePopupClosed(object? sender, EventArgs e)
    {
        CancelTemperatureFlyoutClose();
        DetachTemperaturePopupDismissHandlers();
        SetTemperatureFlyoutHiddenState();
    }

    private void BeginTemperaturePopupClose()
    {
        if (_temperaturePopupCloseTimer.IsEnabled)
        {
            return;
        }

        SetTemperatureFlyoutHiddenState();
        _temperaturePopupCloseTimer.Start();
    }

    private void OnTemperaturePopupCloseTimerTick(object? sender, EventArgs e)
    {
        _temperaturePopupCloseTimer.Stop();

        if (TemperaturePopup.IsOpen)
        {
            TemperaturePopup.IsOpen = false;
        }
    }

    private void AttachTemperaturePopupDismissHandlers()
    {
        DetachTemperaturePopupDismissHandlers();
        _temperaturePopupTopLevel = TopLevel.GetTopLevel(this);

        if (_temperaturePopupTopLevel is null)
        {
            return;
        }

        _temperaturePopupTopLevel.AddHandler(
            PointerPressedEvent,
            OnTemperaturePopupOutsidePointerPressed,
            RoutingStrategies.Tunnel);
        _temperaturePopupTopLevel.KeyDown += OnTemperaturePopupTopLevelKeyDown;

        if (_temperaturePopupTopLevel is Window window)
        {
            window.Deactivated += OnTemperaturePopupWindowDeactivated;
        }
    }

    private void DetachTemperaturePopupDismissHandlers()
    {
        if (_temperaturePopupTopLevel is null)
        {
            return;
        }

        _temperaturePopupTopLevel.RemoveHandler(
            PointerPressedEvent,
            OnTemperaturePopupOutsidePointerPressed);
        _temperaturePopupTopLevel.KeyDown -= OnTemperaturePopupTopLevelKeyDown;

        if (_temperaturePopupTopLevel is Window window)
        {
            window.Deactivated -= OnTemperaturePopupWindowDeactivated;
        }

        _temperaturePopupTopLevel = null;
    }

    private void OnTemperaturePopupOutsidePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (HasOpenTemperaturePopupChild())
        {
            return;
        }

        if (e.Source is Visual source
            && (IsTemperaturePopupInteractionSource(source)
                || ReferenceEquals(source, TemperatureButton)
                || TemperatureButton.IsVisualAncestorOf(source)))
        {
            return;
        }

        BeginTemperaturePopupClose();
    }

    private bool HasOpenTemperaturePopupChild()
    {
        return TemperatureFlyoutPanel
            .GetLogicalDescendants()
            .OfType<ComboBox>()
            .Any(comboBox => comboBox.IsDropDownOpen);
    }

    private bool IsTemperaturePopupInteractionSource(Visual source)
    {
        if (TemperaturePopup.IsInsidePopup(source))
        {
            return true;
        }

        if (source is Control sourceControl
            && sourceControl
                .GetLogicalAncestors()
                .Contains(TemperatureFlyoutPanel))
        {
            return true;
        }

        TopLevel? temperaturePopupRoot = TopLevel.GetTopLevel(TemperatureFlyoutPanel);
        TopLevel? sourceRoot = TopLevel.GetTopLevel(source);

        while (sourceRoot is PopupRoot popupRoot)
        {
            if (ReferenceEquals(sourceRoot, temperaturePopupRoot))
            {
                return true;
            }

            if (popupRoot.Parent is not Popup { PlacementTarget: { } placementTarget })
            {
                return false;
            }

            sourceRoot = TopLevel.GetTopLevel(placementTarget);
        }

        return false;
    }

    private void OnTemperaturePopupTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            BeginTemperaturePopupClose();
        }
    }

    private void OnTemperaturePopupWindowDeactivated(object? sender, EventArgs e)
    {
        _temperaturePopupCloseTimer.Stop();
        SetTemperatureFlyoutHiddenState();
        TemperaturePopup.IsOpen = false;
    }

    private void OnCatalogLoadingIndicatorHostPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty)
        {
            return;
        }

        if (CatalogLoadingIndicatorHost.IsVisible)
        {
            StartCatalogLoadingAnimation();
            return;
        }

        StopCatalogLoadingAnimation();
    }

    private void OnCatalogLoadingTimerTick(object? sender, EventArgs e)
    {
        UpdateCatalogLoadingIndicator();
    }

    private void StartCatalogLoadingAnimation()
    {
        _catalogLoadingStopwatch.Restart();
        _catalogLoadingTimer.Start();
        UpdateCatalogLoadingIndicator();
    }

    private void StopCatalogLoadingAnimation()
    {
        _catalogLoadingTimer.Stop();
        _catalogLoadingStopwatch.Reset();
        CatalogLoadingIndicatorBar.Width = 0d;
        Canvas.SetLeft(CatalogLoadingIndicatorBar, 0d);
    }

    private void UpdateCatalogLoadingIndicator()
    {
        double hostWidth = CatalogLoadingIndicatorHost.Bounds.Width;
        if (hostWidth <= 0d)
        {
            return;
        }

        double segmentWidth = Math.Max(
            LoadingIndicatorMinimumSegmentWidth,
            hostWidth * LoadingIndicatorSegmentWidthRatio);
        double progress = (_catalogLoadingStopwatch.Elapsed.TotalMilliseconds % LoadingIndicatorDurationMilliseconds)
                          / LoadingIndicatorDurationMilliseconds;
        double left = -segmentWidth + ((hostWidth + segmentWidth) * progress);

        CatalogLoadingIndicatorBar.Width = segmentWidth;
        Canvas.SetLeft(CatalogLoadingIndicatorBar, left);
    }

    private void InitializeAspectRatioHintTransform()
    {
        TransformGroup transformGroup = new();
        transformGroup.Children.Add(_aspectRatioHintScale);
        transformGroup.Children.Add(_aspectRatioHintTranslate);

        ConfigureAspectRatioHintTransitions();
        AspectRatioHintShape.Width = AspectRatioHintMaxWidth;
        AspectRatioHintShape.Height = AspectRatioHintMaxHeight;
        AspectRatioHintHost.Width = AspectRatioHintMaxWidth;
        AspectRatioHintHost.Height = AspectRatioHintMaxHeight;
        AspectRatioHintHost.RenderTransformOrigin = RelativePoint.Center;
        AspectRatioHintHost.RenderTransform = transformGroup;
    }

    private void InitializeTemperatureFlyout()
    {
        TransformGroup motionTransform = new();
        motionTransform.Children.Add(_temperatureFlyoutMotionScale);
        motionTransform.Children.Add(_temperatureFlyoutTranslate);
        TemperatureFlyoutPanel.RenderTransformOrigin = new RelativePoint(
            0.5d,
            0d,
            RelativeUnit.Relative);
        TemperatureFlyoutPanel.RenderTransform = motionTransform;

        CubicEaseOut easing = new();
        TemperatureFlyoutPanel.Transitions =
        [
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TemperatureFlyoutAnimationDuration,
                Easing = easing
            }
        ];
        _temperatureFlyoutMotionScale.Transitions =
        [
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleXProperty,
                Duration = TemperatureFlyoutAnimationDuration,
                Easing = easing
            },
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleYProperty,
                Duration = TemperatureFlyoutAnimationDuration,
                Easing = easing
            }
        ];
        _temperatureFlyoutTranslate.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TemperatureFlyoutAnimationDuration,
                Easing = easing
            }
        ];
    }

    private void SetTemperatureFlyoutHiddenState()
    {
        TemperatureFlyoutPanel.Opacity = TemperatureFlyoutHiddenOpacity;
        _temperatureFlyoutMotionScale.ScaleX = TemperatureFlyoutHiddenScale;
        _temperatureFlyoutMotionScale.ScaleY = TemperatureFlyoutHiddenScale;
        _temperatureFlyoutTranslate.Y = TemperatureFlyoutHiddenOffsetY;
    }

    private void SetTemperatureFlyoutVisibleState()
    {
        TemperatureFlyoutPanel.Opacity = TemperatureFlyoutVisibleOpacity;
        _temperatureFlyoutMotionScale.ScaleX = TemperatureFlyoutVisibleScale;
        _temperatureFlyoutMotionScale.ScaleY = TemperatureFlyoutVisibleScale;
        _temperatureFlyoutTranslate.Y = TemperatureFlyoutVisibleOffsetY;
    }

    private void CancelTemperatureFlyoutClose()
    {
        if (!_temperaturePopupCloseTimer.IsEnabled)
        {
            return;
        }

        _temperaturePopupCloseTimer.Stop();

        if (TemperaturePopup.IsOpen)
        {
            SetTemperatureFlyoutVisibleState();
        }
    }

    private void StopTemperatureFlyoutAnimation()
    {
        CancelTemperatureFlyoutClose();
        DetachTemperaturePopupDismissHandlers();
        SetTemperatureFlyoutHiddenState();
        TemperaturePopup.IsOpen = false;
    }

    private void ConfigureAspectRatioHintTransitions()
    {
        CubicEaseOut easing = new();

        AspectRatioHintHost.Transitions =
        [
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = AspectRatioHintAnimationDuration,
                Easing = easing
            }
        ];
        AspectRatioHintShape.Transitions =
        [
            new DoubleTransition
            {
                Property = WidthProperty,
                Duration = AspectRatioHintSizeAnimationDuration,
                Easing = easing
            },
            new DoubleTransition
            {
                Property = HeightProperty,
                Duration = AspectRatioHintSizeAnimationDuration,
                Easing = easing
            }
        ];
        _aspectRatioHintScale.Transitions =
        [
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleXProperty,
                Duration = AspectRatioHintAnimationDuration,
                Easing = easing
            },
            new DoubleTransition
            {
                Property = ScaleTransform.ScaleYProperty,
                Duration = AspectRatioHintAnimationDuration,
                Easing = easing
            }
        ];
        _aspectRatioHintTranslate.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = AspectRatioHintAnimationDuration,
                Easing = easing
            }
        ];
    }

    internal static Border? FindOptionResetFlashTarget(ComboBox comboBox)
    {
        ArgumentNullException.ThrowIfNull(comboBox);
        comboBox.ApplyTemplate();

        Border? bestTarget = null;
        double bestScore = double.NegativeInfinity;
        int visualIndex = 0;

        foreach (Border border in comboBox.GetVisualDescendants().OfType<Border>())
        {
            if (!IsOptionResetFlashCandidate(comboBox, border))
            {
                visualIndex++;
                continue;
            }

            double score = GetOptionResetFlashCandidateScore(comboBox, border, visualIndex);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = border;
            }

            visualIndex++;
        }

        return bestTarget;
    }

    internal static bool CanShowAspectRatioHint(string? aspectRatio)
    {
        return !string.IsNullOrWhiteSpace(aspectRatio)
            && !GenerationAspectRatios.IsAuto(aspectRatio);
    }

    private void SubscribeSelectionResetEvents(UniversalNanoBananaPanelViewModel viewModel)
    {
        if (ReferenceEquals(_selectionResetViewModel, viewModel))
        {
            return;
        }

        UnsubscribeSelectionResetEvents();
        _selectionResetViewModel = viewModel;
        viewModel.SelectionValueReset += OnSelectionValueReset;
    }

    private void UnsubscribeSelectionResetEvents()
    {
        if (_selectionResetViewModel is null)
        {
            return;
        }

        _selectionResetViewModel.SelectionValueReset -= OnSelectionValueReset;
        _selectionResetViewModel = null;
    }

    private void OnSelectionValueReset(object? sender, PropertyChangedEventArgs e)
    {
        OptionResetFlashDescriptor? descriptor = OptionResetFlashDescriptors.FirstOrDefault(candidate =>
            string.Equals(candidate.SelectionPropertyName, e.PropertyName, StringComparison.Ordinal));

        if (descriptor is null)
        {
            return;
        }

        OptionResetFlashState? currentState = descriptor.GetState(this);
        OptionResetFlashState? nextState = RestartOptionResetFlash(
            descriptor.GetComboBox(this),
            currentState);
        descriptor.SetState(this, nextState);
    }

    private static OptionResetFlashState? RestartOptionResetFlash(
        ComboBox comboBox,
        OptionResetFlashState? currentState)
    {
        currentState?.CancelAndRestore();
        Border? flashTarget = FindOptionResetFlashTarget(comboBox);
        if (flashTarget is null)
        {
            return null;
        }

        OptionResetFlashState flashState = OptionResetFlashState.Create(flashTarget);
        _ = RunOptionResetFlashAsync(flashState);

        return flashState;
    }

    private static async Task RunOptionResetFlashAsync(OptionResetFlashState flashState)
    {
        SolidColorBrush flashBrush = new(OptionResetFlashColor)
        {
            Opacity = OptionResetFlashHiddenOpacity
        };
        flashState.ApplyFlashStyle(flashBrush);

        try
        {
            await SetOptionResetFlashOpacityAsync(
                flashBrush,
                OptionResetFlashVisibleOpacity,
                OptionResetFlashQuickFadeDuration,
                flashState.Cancellation.Token);
            await SetOptionResetFlashOpacityAsync(
                flashBrush,
                OptionResetFlashHiddenOpacity,
                OptionResetFlashQuickFadeDuration,
                flashState.Cancellation.Token);
            await SetOptionResetFlashOpacityAsync(
                flashBrush,
                OptionResetFlashVisibleOpacity,
                OptionResetFlashQuickFadeDuration,
                flashState.Cancellation.Token);
            await SetOptionResetFlashOpacityAsync(
                flashBrush,
                OptionResetFlashHiddenOpacity,
                OptionResetFlashFinalFadeDuration,
                flashState.Cancellation.Token);
        }
        catch (OperationCanceledException) when (flashState.Cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            flashState.Restore();
        }
    }

    private static async Task SetOptionResetFlashOpacityAsync(
        SolidColorBrush flashBrush,
        double opacity,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        flashBrush.Transitions =
        [
            new DoubleTransition
            {
                Property = Brush.OpacityProperty,
                Duration = duration,
                Easing = new CubicEaseOut()
            }
        ];
        flashBrush.Opacity = opacity;
        await Task.Delay(duration, cancellationToken);
    }

    private void StopOptionResetFlashAnimations()
    {
        foreach (OptionResetFlashDescriptor descriptor in OptionResetFlashDescriptors)
        {
            descriptor.GetState(this)?.CancelAndRestore();
            descriptor.SetState(this, null);
        }
    }

    private static bool IsOptionResetFlashCandidate(ComboBox comboBox, Border border)
    {
        double width = border.Bounds.Width;
        double height = border.Bounds.Height;
        if ((width <= 0d) || (height <= 0d))
        {
            return false;
        }

        double comboWidth = comboBox.Bounds.Width;
        double comboHeight = comboBox.Bounds.Height;
        if ((comboWidth <= 0d) || (comboHeight <= 0d))
        {
            return true;
        }

        if (width > comboWidth + OptionResetFlashCandidateBoundsTolerance
            || height > comboHeight + OptionResetFlashCandidateBoundsTolerance)
        {
            return false;
        }

        double comboArea = comboWidth * comboHeight;
        double borderArea = GetOptionResetFlashCandidateArea(border);
        return borderArea / comboArea >= OptionResetFlashMinimumCoverage;
    }

    private static double GetOptionResetFlashCandidateScore(
        ComboBox comboBox,
        Border border,
        int visualIndex)
    {
        double comboArea = comboBox.Bounds.Width * comboBox.Bounds.Height;
        double borderArea = GetOptionResetFlashCandidateArea(border);
        double areaScore = comboArea > 0d
            ? 1d - Math.Abs(comboArea - borderArea) / comboArea
            : borderArea;
        double backgroundScore = border.Background is not null ? 1000d : 0d;
        double borderBrushScore = border.BorderBrush is not null ? 100d : 0d;
        double cornerScore = HasCornerRadius(border.CornerRadius) ? 10d : 0d;
        double visualOrderScore = visualIndex / 1000d;

        return backgroundScore + borderBrushScore + cornerScore + areaScore + visualOrderScore;
    }

    private static double GetOptionResetFlashCandidateArea(Border border)
    {
        return border.Bounds.Width * border.Bounds.Height;
    }

    private static bool HasCornerRadius(CornerRadius cornerRadius)
    {
        return cornerRadius.TopLeft > 0d
               || cornerRadius.TopRight > 0d
               || cornerRadius.BottomRight > 0d
               || cornerRadius.BottomLeft > 0d;
    }

    private void OnAspectRatioDropDownOpened(object? sender, EventArgs e)
    {
        _isAspectRatioDropDownOpen = true;
        ShowAspectRatioHint(AspectRatioComboBox.SelectedItem as string);
        Dispatcher.UIThread.Post(AttachAspectRatioScrollHandlers);
    }

    private void OnAspectRatioDropDownClosed(object? sender, EventArgs e)
    {
        _isAspectRatioDropDownOpen = false;
        DetachAspectRatioScrollHandlers();
        HideAspectRatioHint();
    }

    private void OnAspectRatioContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ComboBoxItem item)
        {
            return;
        }

        _aspectRatioItems.Add(item);
        AttachAspectRatioScrollHandler(item);
        item.AddHandler(
            PointerEnteredEvent,
            OnAspectRatioItemPointer,
            RoutingStrategies.Bubble | RoutingStrategies.Tunnel,
            true);
        item.AddHandler(
            PointerMovedEvent,
            OnAspectRatioItemPointer,
            RoutingStrategies.Bubble | RoutingStrategies.Tunnel,
            true);
    }

    private void OnAspectRatioContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container is not ComboBoxItem item)
        {
            return;
        }

        _aspectRatioItems.Remove(item);
        item.RemoveHandler(PointerEnteredEvent, OnAspectRatioItemPointer);
        item.RemoveHandler(PointerMovedEvent, OnAspectRatioItemPointer);
    }

    private void OnAspectRatioItemPointer(object? sender, PointerEventArgs e)
    {
        if (!_isAspectRatioDropDownOpen)
        {
            return;
        }

        if (sender is ComboBoxItem { DataContext: string aspectRatio })
        {
            RememberAspectRatioPointerPosition(e);
            ShowAspectRatioHint(aspectRatio);
        }
    }

    private void OnAspectRatioHintPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        AspectRatioComboBox.IsDropDownOpen = false;
    }

    private void OnAspectRatioScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(UpdateAspectRatioHintFromPointerPosition);
    }

    private void OnAspectRatioHintAutoCycleTimerTick(object? sender, EventArgs e)
    {
        if (!_targetAspectRatioHintVisible || !GenerationAspectRatios.IsAuto(_hintAspectRatio))
        {
            _aspectRatioHintAutoCycleTimer.Stop();
            return;
        }

        AdvanceAspectRatioHintAutoCycle();
    }

    private void ShowAspectRatioHint(string? aspectRatio)
    {
        if (!CanShowAspectRatioHint(aspectRatio))
        {
            HideAspectRatioHint();
            return;
        }

        string concreteAspectRatio = aspectRatio
            ?? throw new InvalidOperationException("Aspect ratio hint cannot be shown without an aspect ratio.");
        UpdateAspectRatioHintAspectRatio(concreteAspectRatio);
        StartAspectRatioHintVisibilityAnimation(true);
    }

    private void UpdateAspectRatioHintAspectRatio(string aspectRatio)
    {
        if (string.Equals(_hintAspectRatio, aspectRatio, StringComparison.Ordinal))
        {
            return;
        }

        bool wasAuto = GenerationAspectRatios.IsAuto(_hintAspectRatio);
        bool isAuto = GenerationAspectRatios.IsAuto(aspectRatio);
        _hintAspectRatio = aspectRatio;

        if (isAuto)
        {
            if (!wasAuto)
            {
                _autoCycleIndex = -1;
            }

            AdvanceAspectRatioHintAutoCycle();
            _aspectRatioHintAutoCycleTimer.Start();
        }
        else
        {
            _aspectRatioHintAutoCycleTimer.Stop();
            _autoCycleIndex = -1;
            SetAspectRatioHintSizeTarget(aspectRatio);
        }
    }

    private void HideAspectRatioHint()
    {
        _hintAspectRatio = string.Empty;
        _hintConcreteAspectRatio = string.Empty;
        _autoCycleIndex = -1;
        _aspectRatioHintAutoCycleTimer.Stop();
        StartAspectRatioHintVisibilityAnimation(false);
    }

    private void StartAspectRatioHintVisibilityAnimation(bool visible)
    {
        if (visible && _targetAspectRatioHintVisible && AspectRatioHintPopup.IsOpen)
        {
            return;
        }

        if (!visible && !_targetAspectRatioHintVisible && !AspectRatioHintPopup.IsOpen)
        {
            return;
        }

        _targetAspectRatioHintVisible = visible;
        _aspectRatioHintCloseCancellation?.Cancel();

        if (!visible)
        {
            StartAspectRatioHintCloseDelay();
        }

        AspectRatioHintPopup.IsOpen = true;
        AspectRatioHintHost.Opacity = visible ? AspectRatioHintVisibleOpacity : AspectRatioHintHiddenOpacity;
        double scale = visible ? AspectRatioHintVisibleScale : AspectRatioHintHiddenScale;
        _aspectRatioHintScale.ScaleX = scale;
        _aspectRatioHintScale.ScaleY = scale;
        _aspectRatioHintTranslate.Y = visible ? AspectRatioHintVisibleOffsetY : AspectRatioHintHiddenOffsetY;
    }

    private async void StartAspectRatioHintCloseDelay()
    {
        CancellationTokenSource closeCancellation = new();
        _aspectRatioHintCloseCancellation = closeCancellation;

        try
        {
            await Task.Delay(AspectRatioHintAnimationDuration, closeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!_targetAspectRatioHintVisible)
        {
            AspectRatioHintPopup.IsOpen = false;
        }

        if (ReferenceEquals(_aspectRatioHintCloseCancellation, closeCancellation))
        {
            _aspectRatioHintCloseCancellation = null;
        }

        closeCancellation.Dispose();
    }

    private void SetAspectRatioHintSizeTarget(string aspectRatio)
    {
        if (string.Equals(_hintConcreteAspectRatio, aspectRatio, StringComparison.Ordinal))
        {
            return;
        }

        AspectRatioHintPreviewSize target = AspectRatioHintPreviewSizer.Calculate(
            aspectRatio,
            AspectRatioHintMaxWidth,
            AspectRatioHintMaxHeight);

        _hintConcreteAspectRatio = aspectRatio;
        AspectRatioHintShape.Width = target.Width;
        AspectRatioHintShape.Height = target.Height;
    }

    private void AdvanceAspectRatioHintAutoCycle()
    {
        IReadOnlyList<string> concreteAspectRatios = GetConcreteAspectRatios();
        if (concreteAspectRatios.Count == 0)
        {
            return;
        }

        _autoCycleIndex = (_autoCycleIndex + 1) % concreteAspectRatios.Count;
        SetAspectRatioHintSizeTarget(concreteAspectRatios[_autoCycleIndex]);
    }

    private IReadOnlyList<string> GetConcreteAspectRatios()
    {
        if (DataContext is UniversalNanoBananaPanelViewModel viewModel)
        {
            return viewModel.AspectRatios
                .Where(aspectRatio => !GenerationAspectRatios.IsAuto(aspectRatio))
                .ToList();
        }

        return [];
    }

    private void StopAspectRatioHintAnimation()
    {
        _aspectRatioHintAutoCycleTimer.Stop();
        DetachAspectRatioScrollHandlers();
        _aspectRatioHintCloseCancellation?.Cancel();
        _aspectRatioHintCloseCancellation = null;
        AspectRatioHintPopup.IsOpen = false;
    }

    private void AttachAspectRatioScrollHandlers()
    {
        foreach (ComboBoxItem item in _aspectRatioItems)
        {
            AttachAspectRatioScrollHandler(item);
        }
    }

    private void AttachAspectRatioScrollHandler(ComboBoxItem item)
    {
        ScrollViewer? scrollViewer = item
            .GetVisualAncestors()
            .OfType<ScrollViewer>()
            .FirstOrDefault();

        if (scrollViewer is null || !_aspectRatioScrollViewers.Add(scrollViewer))
        {
            return;
        }

        scrollViewer.ScrollChanged += OnAspectRatioScrollChanged;
    }

    private void DetachAspectRatioScrollHandlers()
    {
        foreach (ScrollViewer scrollViewer in _aspectRatioScrollViewers)
        {
            scrollViewer.ScrollChanged -= OnAspectRatioScrollChanged;
        }

        _aspectRatioScrollViewers.Clear();
        _lastAspectRatioPointerScrollViewer = null;
        _lastAspectRatioPointerScrollViewerPosition = null;
    }

    private void RememberAspectRatioPointerPosition(PointerEventArgs e)
    {
        if (e.Source is not Visual visual)
        {
            return;
        }

        ScrollViewer? scrollViewer = visual
            .GetVisualAncestors()
            .OfType<ScrollViewer>()
            .FirstOrDefault();

        if (scrollViewer is null)
        {
            return;
        }

        if (_aspectRatioScrollViewers.Add(scrollViewer))
        {
            scrollViewer.ScrollChanged += OnAspectRatioScrollChanged;
        }

        _lastAspectRatioPointerScrollViewer = scrollViewer;
        _lastAspectRatioPointerScrollViewerPosition = e.GetPosition(scrollViewer);
    }

    private void UpdateAspectRatioHintFromPointerPosition()
    {
        if (!_isAspectRatioDropDownOpen
            || _lastAspectRatioPointerScrollViewer is null
            || _lastAspectRatioPointerScrollViewerPosition is null)
        {
            return;
        }

        ScrollViewer scrollViewer = _lastAspectRatioPointerScrollViewer;
        Point pointerPosition = _lastAspectRatioPointerScrollViewerPosition.Value;

        foreach (ComboBoxItem item in _aspectRatioItems)
        {
            if (!item.IsVisible || item.DataContext is not string aspectRatio)
            {
                continue;
            }

            Point? itemPosition = item.TranslatePoint(new Point(0d, 0d), scrollViewer);
            if (itemPosition is null)
            {
                continue;
            }

            Rect itemBounds = new(itemPosition.Value, item.Bounds.Size);
            if (itemBounds.Contains(pointerPosition))
            {
                ShowAspectRatioHint(aspectRatio);
                return;
            }
        }
    }

    private sealed class OptionResetFlashState
    {
        public Border Target { get; }
        public CancellationTokenSource Cancellation { get; } = new();
        private readonly IBrush? _originalBackground;
        private readonly bool _hadLocalBackground;
        private bool _isRestored;

        private OptionResetFlashState(Border target)
        {
            Target = target;
            _originalBackground = target.Background;
            _hadLocalBackground = target.IsSet(Border.BackgroundProperty);
        }

        public static OptionResetFlashState Create(Border target)
        {
            ArgumentNullException.ThrowIfNull(target);

            return new OptionResetFlashState(target);
        }

        public void ApplyFlashStyle(SolidColorBrush flashBrush)
        {
            ArgumentNullException.ThrowIfNull(flashBrush);

            Target.Background = flashBrush;
        }

        public void CancelAndRestore()
        {
            Cancellation.Cancel();
            Restore();
        }

        public void Restore()
        {
            if (_isRestored)
            {
                return;
            }

            _isRestored = true;

            if (_hadLocalBackground)
            {
                Target.Background = _originalBackground;
            }
            else
            {
                Target.ClearValue(Border.BackgroundProperty);
            }
        }
    }

    private sealed class OptionResetFlashDescriptor
    {
        public required string SelectionPropertyName { get; init; }
        public required Func<NanoBanana2PanelView, ComboBox> GetComboBox { get; init; }
        public required Func<NanoBanana2PanelView, OptionResetFlashState?> GetState { get; init; }
        public required Action<NanoBanana2PanelView, OptionResetFlashState?> SetState { get; init; }
    }
}
