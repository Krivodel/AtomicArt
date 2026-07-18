using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using SukiUI.Controls;

using Pica.Viewer.Controls;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private void OnFloatingMenuPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;

        e.Handled = true;
    }

    private async void OnFilteringTogglePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        _ = sender;

        if (e.Property != PixelFilteringToggleSwitch.IsFilteringEnabledProperty)
        {
            return;
        }

        _isFilteringEnabled = _view.FilteringToggle.IsFilteringEnabled;
        _view.ApplyImageFiltering(_isFilteringEnabled);
        await SaveCurrentStateAsync();
    }

    private async void OnMovementSpeedSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_view.SettingsPanel.MovementSpeedComboBox.SelectedItem
            is not ViewerSettingOption<int> selectedOption)
        {
            return;
        }

        _movementSpeed = selectedOption.Value;
        await SaveCurrentStateAsync();
    }

    private async void OnZoomSpeedSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_view.SettingsPanel.ZoomSpeedComboBox.SelectedItem
            is not ViewerSettingOption<int> selectedOption)
        {
            return;
        }

        _zoomSpeed = selectedOption.Value;
        await SaveCurrentStateAsync();
    }

    private async void OnExpandOnDoubleClickChanged(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _expandOnDoubleClick = _view.SettingsPanel.ExpandOnDoubleClickCheckBox.IsChecked == true;
        _imageDoubleClickTracker.Reset();
        await SaveCurrentStateAsync();
    }

    private async void OnFastLoadingChanged(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _isFastLoadingEnabled = _view.SettingsPanel.FastLoadingCheckBox.IsChecked == true;

        if (_isFastLoadingEnabled)
        {
            StartPreviewCachePriming();
        }
        else
        {
            if (_previewCachePrimingTask is not null)
            {
                CancelPendingImageLoad();
            }

            _previewCache.Clear();
        }

        await SaveCurrentStateAsync();
    }

    private async void OnAllowFreeZoomOutChanged(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _allowFreeZoomOut = _view.SettingsPanel.AllowFreeZoomOutCheckBox.IsChecked == true;

        if (!_allowFreeZoomOut
            && TryGetResetImagePlacement(out double fittedScale, out _, out _)
            && (_scale < fittedScale))
        {
            Size viewport = GetViewportSize();
            BeginScaleAnimation(
                fittedScale,
                new Point(viewport.Width / 2d, viewport.Height / 2d));
        }

        await SaveCurrentStateAsync();
    }

    private async void OnSmoothPanningChanged(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _isSmoothPanningEnabled = _view.SettingsPanel.SmoothPanningCheckBox.IsChecked == true;
        _view.SettingsPanel.PanningInertiaCheckBox.IsEnabled = _isSmoothPanningEnabled;

        if (!_isSmoothPanningEnabled)
        {
            _isPanningInertiaEnabled = false;
            SetPanningInertiaCheckBox(false);
        }

        ResetPanMotion();
        await SaveCurrentStateAsync();
    }

    private async void OnPanningInertiaChanged(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _isPanningInertiaEnabled = _isSmoothPanningEnabled
            && (_view.SettingsPanel.PanningInertiaCheckBox.IsChecked == true);

        if (!_isPanningInertiaEnabled)
        {
            SetPanningInertiaCheckBox(false);
        }

        ResetPanMotion();
        await SaveCurrentStateAsync();
    }

    private async void OnResizeBehaviorSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_view.SettingsPanel.ResizeBehaviorComboBox.SelectedItem
            is not ViewerSettingOption<WindowResizeBehavior> selectedOption)
        {
            return;
        }

        _resizeBehavior = selectedOption.Value;

        if (_isWindowedMode && (_resizeBehavior != WindowResizeBehavior.Free))
        {
            FitWindowToCurrentImage();
        }

        await SaveCurrentStateAsync();
    }

    private async void OnRememberWindowPlacementChanged(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        _rememberWindowPlacement = _view.SettingsPanel.RememberWindowPlacementCheckBox.IsChecked == true;

        if (_rememberWindowPlacement)
        {
            CaptureWindowedPlacement();
        }
        else
        {
            ResetRememberedWindowPlacement();
        }

        await SaveCurrentStateAsync();
    }

    private async Task SaveCurrentStateAsync()
    {
        await _imageViewerStateService.SaveAsync(CreateState(), CancellationToken.None);
    }

    private void ShowSettingsPanel()
    {
        _view.SettingsPanel.IsVisible = true;
        _view.SettingsPanel.IsHitTestVisible = true;
        StartSettingsPanelAnimation(
            ImageViewerVisualMetrics.VisibleControlsOpacity,
            0d,
            null);
    }

    private void HideSettingsPanel()
    {
        _view.SettingsPanel.IsHitTestVisible = false;
        StartSettingsPanelAnimation(
            ImageViewerVisualMetrics.HiddenControlsOpacity,
            ImageViewerVisualMetrics.SettingsPanelHiddenOffset,
            CompleteSettingsPanelHide);
    }

    private void HideSettingsPanelImmediately()
    {
        _settingsPanelAnimationId++;
        _view.SettingsPanel.IsHitTestVisible = false;
        _view.SettingsPanel.IsVisible = false;
        _view.SettingsPanel.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;

        if (_view.SettingsPanel.RenderTransform is TranslateTransform transform)
        {
            transform.Y = ImageViewerVisualMetrics.SettingsPanelHiddenOffset;
        }
    }

    private void StartSettingsPanelAnimation(
        double targetOpacity,
        double targetOffset,
        Action? completed)
    {
        if (_view.SettingsPanel.RenderTransform is not TranslateTransform transform)
        {
            return;
        }

        long animationId = ++_settingsPanelAnimationId;
        double startOpacity = _view.SettingsPanel.Opacity;
        double startOffset = transform.Y;

        StartFrameAnimation(
            SettingsPanelAnimationDuration,
            () => animationId == _settingsPanelAnimationId,
            progress =>
            {
                double easedProgress = 1d - Math.Pow(1d - progress, 3d);
                _view.SettingsPanel.Opacity = startOpacity
                    + ((targetOpacity - startOpacity) * easedProgress);
                transform.Y = startOffset + ((targetOffset - startOffset) * easedProgress);
            },
            completed: completed);
    }

    private void CompleteSettingsPanelHide()
    {
        _view.SettingsPanel.IsVisible = false;
    }

    private void ShowCursor()
    {
        _cursorTimer.Stop();

        if (IsAreaSelectionModeActive())
        {
            return;
        }

        SetVisibleCursor(ArrowCursor);

        if (_view.ViewerArea.IsPointerOver && !_view.SettingsPanel.IsPointerOver)
        {
            _cursorTimer.Start();
        }
    }

    private void HideCursor()
    {
        _isCursorHidden = true;
        Cursor = HiddenCursor;
    }

    private void SetVisibleCursor(Cursor cursor)
    {
        _isCursorHidden = false;
        Cursor = cursor;
    }

    private bool IsAreaSelectionModeActive()
    {
        return _isSelectionActive || _isSelecting || _isSelectionArmed;
    }

    private void CloseWithFade()
    {
        Close();
    }
}
