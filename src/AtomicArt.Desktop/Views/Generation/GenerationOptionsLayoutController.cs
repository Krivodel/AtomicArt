using System.ComponentModel;

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Views.Generation;

internal sealed class GenerationOptionsLayoutController
{
    private const string InlineClass = "inline";
    private const int TransitionDurationMilliseconds = 180;

    private static readonly CubicEaseInOut TransitionEasing = new();

    private readonly Grid _parameterRow;
    private readonly Grid _optionsSlot;
    private readonly ContentControl _inlineHost;
    private readonly Border _settingsButtonHost;
    private readonly Button _settingsButton;
    private readonly Popup _popup;
    private readonly Border _flyoutPanel;
    private readonly StackPanel _optionsPanel;
    private readonly Action _closePopup;
    private readonly IUiFrameScheduler? _frameSchedulerOverride;
    private AvaloniaUiFrameScheduler? _ownedFrameScheduler;
    private UiAnimationScheduler? _animationScheduler;
    private INotifyPropertyChanged? _viewModel;
    private bool _isAttached;
    private bool _isUpdateScheduled;
    private bool _hasLayout;
    private bool _showInline;
    private bool _isPresented = true;
    private double _progress;
    private double _inlineWidth;
    private double _availableWidth;

    internal GenerationOptionsLayoutController(
        Grid parameterRow,
        Grid optionsSlot,
        ContentControl inlineHost,
        Border settingsButtonHost,
        Button settingsButton,
        Popup popup,
        Border flyoutPanel,
        StackPanel optionsPanel,
        Action closePopup,
        IUiFrameScheduler? frameScheduler = null)
    {
        _parameterRow = parameterRow ?? throw new ArgumentNullException(nameof(parameterRow));
        _optionsSlot = optionsSlot ?? throw new ArgumentNullException(nameof(optionsSlot));
        _inlineHost = inlineHost ?? throw new ArgumentNullException(nameof(inlineHost));
        _settingsButtonHost = settingsButtonHost ?? throw new ArgumentNullException(nameof(settingsButtonHost));
        _settingsButton = settingsButton ?? throw new ArgumentNullException(nameof(settingsButton));
        _popup = popup ?? throw new ArgumentNullException(nameof(popup));
        _flyoutPanel = flyoutPanel ?? throw new ArgumentNullException(nameof(flyoutPanel));
        _optionsPanel = optionsPanel ?? throw new ArgumentNullException(nameof(optionsPanel));
        _closePopup = closePopup ?? throw new ArgumentNullException(nameof(closePopup));
        _frameSchedulerOverride = frameScheduler;
    }

    internal void Attach(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        if (_isAttached)
        {
            return;
        }

        _isAttached = true;

        if (_frameSchedulerOverride is not null)
        {
            _animationScheduler = new UiAnimationScheduler(_frameSchedulerOverride);
        }
        else
        {
            _ownedFrameScheduler = new AvaloniaUiFrameScheduler(topLevel);
            _animationScheduler = new UiAnimationScheduler(_ownedFrameScheduler);
        }

        _parameterRow.SizeChanged += OnParameterRowSizeChanged;
        _parameterRow.DataContextChanged += OnDataContextChanged;
        UpdateDataContext();
    }

    internal void Detach()
    {
        _isAttached = false;
        _parameterRow.SizeChanged -= OnParameterRowSizeChanged;
        _parameterRow.DataContextChanged -= OnDataContextChanged;
        UnsubscribeViewModel();
        CompleteTransition();
        _ownedFrameScheduler?.Dispose();
        _ownedFrameScheduler = null;
        _animationScheduler = null;
        _hasLayout = false;
    }

    internal void SetPresented(bool isPresented)
    {
        _isPresented = isPresented;

        if (!isPresented)
        {
            CompleteTransition();
        }
    }

    internal void PreparePopup()
    {
        if (!_showInline)
        {
            CompleteTransition();
        }
    }

    private void UpdateDataContext()
    {
        UnsubscribeViewModel();
        _optionsPanel.DataContext = _parameterRow.DataContext;
        _viewModel = _parameterRow.DataContext as INotifyPropertyChanged;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ScheduleUpdate();
    }

    private void UnsubscribeViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void ScheduleUpdate()
    {
        if (!_isAttached || _isUpdateScheduled)
        {
            return;
        }

        _isUpdateScheduled = true;
        _parameterRow.Dispatcher.Post(UpdateLayout, DispatcherPriority.Loaded);
    }

    private void UpdateLayout()
    {
        _isUpdateScheduled = false;

        if (!_isAttached || _parameterRow.Bounds.Width <= 0d)
        {
            return;
        }

        bool hasOptions = _popup.IsVisible;
        bool showInline = false;

        if (hasOptions)
        {
            _optionsPanel.Classes.Set(InlineClass, true);
            _optionsPanel.Width = double.NaN;
            Size availableSize = new(double.PositiveInfinity, double.PositiveInfinity);
            _optionsPanel.Measure(availableSize);
            _inlineWidth = _optionsPanel.DesiredSize.Width;
            double requiredWidth = _optionsSlot.Margin.Left + _optionsSlot.Margin.Right;

            foreach (Control child in _parameterRow.Children)
            {
                if (!child.IsVisible
                    || ReferenceEquals(child, _optionsSlot)
                    || child is Popup)
                {
                    continue;
                }

                child.Measure(availableSize);
                requiredWidth += child.DesiredSize.Width;
            }

            _availableWidth = Math.Max(0d, _parameterRow.Bounds.Width - requiredWidth);
            showInline = _inlineWidth <= _availableWidth;
        }

        bool targetChanged = _showInline != showInline;
        _showInline = showInline;

        if (showInline)
        {
            if (!ReferenceEquals(_inlineHost.Content, _optionsPanel))
            {
                _closePopup();
                _flyoutPanel.Child = null;
                _inlineHost.Content = _optionsPanel;
            }

            _inlineHost.IsVisible = true;
        }
        else if (targetChanged)
        {
            foreach (ComboBox comboBox in _optionsPanel.GetLogicalDescendants().OfType<ComboBox>())
            {
                comboBox.IsDropDownOpen = false;
            }

            _inlineHost.IsHitTestVisible = false;
            _settingsButton.IsHitTestVisible = true;
            _settingsButton.Focusable = true;

            if (_optionsPanel.IsKeyboardFocusWithin)
            {
                _settingsButton.Focus();
            }
        }

        _optionsPanel.Classes.Set(InlineClass, ReferenceEquals(_inlineHost.Content, _optionsPanel));
        _optionsPanel.Width = _inlineHost.Content is null ? double.NaN : _inlineWidth;
        _optionsSlot.MaxWidth = _availableWidth;
        _inlineHost.MaxWidth = _availableWidth;

        if (!_hasLayout || !hasOptions || !_isPresented || _animationScheduler is null)
        {
            CompleteTransition();
        }
        else if (targetChanged)
        {
            _animationScheduler.Cancel(_inlineHost);
            _settingsButton.IsHitTestVisible = !showInline;
            _settingsButton.Focusable = !showInline;
            _inlineHost.IsHitTestVisible = false;
            KeyboardNavigation.SetTabNavigation(_inlineHost, KeyboardNavigationMode.None);

            if (showInline && _settingsButton.IsKeyboardFocusWithin)
            {
                TopLevel.GetTopLevel(_parameterRow)?.FocusManager?.Focus(null);
            }

            double targetProgress = showInline ? 1d : 0d;
            int durationMilliseconds = Math.Max(1, (int)Math.Round(
                TransitionDurationMilliseconds * Math.Abs(targetProgress - _progress)));
            _ = _animationScheduler.AnimateValueAsync(
                _inlineHost,
                _progress,
                targetProgress,
                durationMilliseconds,
                0,
                TransitionEasing.Ease,
                ApplyProgress,
                CompleteTransition);
        }
        else
        {
            ApplyProgress(_progress);
        }

        _hasLayout = true;
    }

    private void ApplyProgress(double progress)
    {
        _progress = progress;
        _inlineHost.Opacity = progress;
        _settingsButtonHost.Opacity = 1d - progress;
    }

    private void CompleteTransition()
    {
        _animationScheduler?.Cancel(_inlineHost);
        ApplyProgress(_showInline ? 1d : 0d);

        if (!_showInline)
        {
            _inlineHost.Content = null;
            _flyoutPanel.Child = _optionsPanel;
            _optionsPanel.Classes.Set(InlineClass, false);
            _optionsPanel.Width = double.NaN;
        }

        _inlineHost.IsVisible = _showInline;
        _inlineHost.IsHitTestVisible = _showInline;
        _settingsButton.IsHitTestVisible = !_showInline;
        _settingsButton.Focusable = !_showInline;
        KeyboardNavigation.SetTabNavigation(
            _inlineHost,
            _showInline ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
    }

    private void OnParameterRowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ScheduleUpdate();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UpdateDataContext();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ScheduleUpdate();
    }
}
