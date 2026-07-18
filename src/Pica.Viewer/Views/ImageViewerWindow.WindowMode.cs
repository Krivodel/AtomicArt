using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using SukiUI.Controls;
using Pica.Viewer.Services;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private void OnCursorTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (IsAreaSelectionModeActive())
        {
            _cursorTimer.Stop();
            UpdateSelectionCursor(_lastPointerHoverPosition);
            return;
        }

        if (!_view.Root.IsPointerOver || _view.SettingsPanel.IsPointerOver)
        {
            SetVisibleCursor(ArrowCursor);
            _cursorTimer.Stop();
            return;
        }

        HideCursor();
    }

    private void OnWindowModeLayoutTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        CompleteWindowModeLayoutSettlement();
    }

    private void OnRootPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_view.Root.IsPointerOver)
        {
            return;
        }

        HideViewerControls();
        _lastPointerScreenPosition = null;
        _cursorTimer.Stop();
        SetVisibleCursor(ArrowCursor);
    }

    private void OnWindowResizeAnimationFrame(TimeSpan frameTime)
    {
        _ = frameTime;

        _isWindowResizeLayoutPending = false;
        ResetScaleAndCenter();
    }

    private void ToggleWindowMode()
    {
        if (_isWindowedMode)
        {
            EnterFullScreenMode();
            return;
        }

        EnterWindowedMode();
    }

    private ViewerWindowMode GetViewerWindowMode()
    {
        return _isWindowedMode
            ? ViewerWindowMode.Windowed
            : ViewerWindowMode.FullScreen;
    }

    private void ApplyInitialWindowMode()
    {
        _view.UpdateSettingsPanelPlacement(GetViewerWindowMode());

        if (_isWindowedMode)
        {
            IsTitleBarVisible = true;
            ShowTitlebarBackground = true;
            _view.WindowResizeOverlay.IsVisible = true;
            _view.FullscreenSettingsButton.IsVisible = false;
            _view.WindowModeButton.IsVisible = false;
            _view.CloseButton.IsVisible = false;

            if (_resizeBehavior == WindowResizeBehavior.Free)
            {
                RestoreWindowedGeometry();
            }
            else
            {
                FitWindowToCurrentImage();
            }

            BeginWindowModeLayoutSettlement();
            return;
        }

        IsTitleBarVisible = false;
        ShowTitlebarBackground = false;
        _view.WindowResizeOverlay.IsVisible = false;
        _view.FullscreenSettingsButton.IsVisible = true;
        _view.WindowModeButton.IsVisible = true;
        _view.CloseButton.IsVisible = true;
        BeginWindowModeLayoutSettlement();
    }

    private void EnterWindowedMode()
    {
        _isChangingWindowMode = true;
        _isWindowedMode = true;
        _view.UpdateSettingsPanelPlacement(GetViewerWindowMode());

        try
        {
            WindowState = WindowState.Normal;
            IsTitleBarVisible = true;
            ShowTitlebarBackground = true;
            _view.WindowResizeOverlay.IsVisible = true;
            _view.FullscreenSettingsButton.IsVisible = false;
            _view.WindowModeButton.IsVisible = false;
            _view.CloseButton.IsVisible = false;
            if (_resizeBehavior == WindowResizeBehavior.Free)
            {
                RestoreWindowedGeometry();
            }
        }
        finally
        {
            _isChangingWindowMode = false;
        }

        BeginWindowModeLayoutSettlement();
    }

    private void EnterFullScreenMode()
    {
        CaptureWindowedPlacement();
        _isChangingWindowMode = true;
        _isWindowedMode = false;
        _view.UpdateSettingsPanelPlacement(GetViewerWindowMode());

        try
        {
            HideSettingsPanelImmediately();
            IsTitleBarVisible = false;
            ShowTitlebarBackground = false;
            _view.WindowResizeOverlay.IsVisible = false;
            _view.FullscreenSettingsButton.IsVisible = true;
            _view.WindowModeButton.IsVisible = true;
            _view.CloseButton.IsVisible = true;
            WindowState = WindowState.FullScreen;
        }
        finally
        {
            _isChangingWindowMode = false;
        }

        BeginWindowModeLayoutSettlement();
    }

    private void FitWindowToCurrentImage()
    {
        if (_bitmap is null)
        {
            return;
        }

        Size imageSize = ImageWindowGeometry.FitImage(
            GetCurrentSourcePixelSize(),
            _windowedPreferredExtent,
            GetMaximumWindowedImageSize());
        Size targetSize = new(
            imageSize.Width,
            imageSize.Height + GetWindowedTitleBarHeight());
        ApplyWindowedClientSize(targetSize);
        ApplyWindowedPosition(targetSize);
        ResetScaleAndCenterAfterLayout();
    }

    private void RestoreWindowedGeometry()
    {
        Size maximumImageSize = GetMaximumWindowedImageSize();
        Size maximumWindowSize = new(
            maximumImageSize.Width,
            maximumImageSize.Height + GetWindowedTitleBarHeight());
        Size storedSize = _windowedClientSize
            ?? new Size(DefaultWindowWidth, DefaultWindowHeight);
        Size targetSize = new(
            Math.Min(storedSize.Width, maximumWindowSize.Width),
            Math.Min(storedSize.Height, maximumWindowSize.Height));
        ApplyWindowedClientSize(targetSize);
        ApplyWindowedPosition(targetSize);
        ResetScaleAndCenterAfterLayout();
    }

    private void ResetRememberedWindowPlacement()
    {
        _windowedPosition = null;
        _windowedClientSize = null;
        _windowedPreferredExtent = Math.Max(DefaultWindowWidth, DefaultWindowHeight);
    }

    private void ApplyWindowedClientSize(Size clientSize)
    {
        _isApplyingWindowGeometry = true;

        try
        {
            Width = clientSize.Width;
            Height = clientSize.Height;
            _windowedClientSize = clientSize;
        }
        finally
        {
            _isApplyingWindowGeometry = false;
        }
    }

    private void ApplyWindowedPosition(Size clientSize)
    {
        Screen? screen = GetWindowedScreen();

        if (screen is null)
        {
            return;
        }

        double scaling = screen.Scaling;
        int windowPixelWidth = (int)Math.Ceiling(clientSize.Width * scaling);
        int windowPixelHeight = (int)Math.Ceiling(clientSize.Height * scaling);
        int maximumLeft = Math.Max(screen.WorkingArea.X, screen.WorkingArea.Right - windowPixelWidth);
        int maximumTop = Math.Max(screen.WorkingArea.Y, screen.WorkingArea.Bottom - windowPixelHeight);
        int left = _windowedPosition?.X
            ?? screen.WorkingArea.X + ((screen.WorkingArea.Width - windowPixelWidth) / 2);
        int top = _windowedPosition?.Y
            ?? screen.WorkingArea.Y + ((screen.WorkingArea.Height - windowPixelHeight) / 2);
        PixelPoint position = new(
            Math.Clamp(left, screen.WorkingArea.X, maximumLeft),
            Math.Clamp(top, screen.WorkingArea.Y, maximumTop));

        _isApplyingWindowGeometry = true;

        try
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = position;
            _windowedPosition = position;
        }
        finally
        {
            _isApplyingWindowGeometry = false;
        }
    }

    private Size GetMaximumWindowedImageSize()
    {
        Screen? screen = GetWindowedScreen();

        if (screen is null)
        {
            return new Size(DefaultWindowWidth, DefaultWindowHeight);
        }

        double maximumWidth = screen.WorkingArea.Width / screen.Scaling;
        double maximumHeight = Math.Max(
            1d,
            (screen.WorkingArea.Height / screen.Scaling) - GetWindowedTitleBarHeight());

        return new Size(maximumWidth, maximumHeight);
    }

    private Screen? GetWindowedScreen()
    {
        Screen? storedScreen = _windowedPosition is { } storedPosition
            ? Screens.ScreenFromPoint(storedPosition)
            : null;

        return storedScreen ?? Screens.ScreenFromWindow(this) ?? Screens.Primary;
    }

    private void CaptureWindowedPlacement()
    {
        if (!_isWindowedMode || (WindowState != WindowState.Normal))
        {
            return;
        }

        if (ClientSize is { Width: > 0d, Height: > 0d })
        {
            _windowedClientSize = ClientSize;
            _windowedPreferredExtent = Math.Max(
                ClientSize.Width,
                Math.Max(1d, ClientSize.Height - GetWindowedTitleBarHeight()));
        }

        _windowedPosition = Position;
    }

    private ImageViewerState CreateState()
    {
        return new ImageViewerState
        {
            IsFilteringEnabled = _isFilteringEnabled,
            MovementSpeed = _movementSpeed,
            ZoomSpeed = _zoomSpeed,
            ExpandOnDoubleClick = _expandOnDoubleClick,
            IsFastLoadingEnabled = _isFastLoadingEnabled,
            AllowFreeZoomOut = _allowFreeZoomOut,
            IsSmoothPanningEnabled = _isSmoothPanningEnabled,
            IsPanningInertiaEnabled = _isPanningInertiaEnabled,
            ResizeBehavior = _resizeBehavior,
            RememberWindowPlacement = _rememberWindowPlacement,
            IsWindowed = _rememberWindowPlacement && _isWindowedMode,
            WindowX = _rememberWindowPlacement ? _windowedPosition?.X : null,
            WindowY = _rememberWindowPlacement ? _windowedPosition?.Y : null,
            WindowWidth = _rememberWindowPlacement ? _windowedClientSize?.Width : null,
            WindowHeight = _rememberWindowPlacement ? _windowedClientSize?.Height : null
        };
    }

    private static PixelPoint? CreateWindowedPosition(ImageViewerState state)
    {
        return state is { WindowX: { } x, WindowY: { } y }
            ? new PixelPoint(x, y)
            : null;
    }

    private static Size? CreateWindowedClientSize(ImageViewerState state)
    {
        return state is { WindowWidth: { } width, WindowHeight: { } height }
            ? new Size(width, height)
            : null;
    }

    private double GetWindowedTitleBarHeight()
    {
        double measuredHeight = ClientSize.Height - _view.ViewerArea.Bounds.Height;

        return measuredHeight > 0d
            ? measuredHeight
            : WindowedTitleBarHeight;
    }

    private void ResetScaleAndCenterAfterLayout()
    {
        Dispatcher.Post(ScheduleWindowResizeLayout, DispatcherPriority.Render);
    }

    private void BeginWindowModeLayoutSettlement()
    {
        _isWindowModeLayoutSettling = true;
        _windowModeLayoutTimer.Stop();
        _windowModeLayoutTimer.Start();
        ScheduleWindowResizeLayout();
        Dispatcher.Post(CompleteWindowModeLayoutSettlement, DispatcherPriority.Render);
    }

    private void CompleteWindowModeLayoutSettlement()
    {
        if (!_isWindowModeLayoutSettling)
        {
            return;
        }

        _windowModeLayoutTimer.Stop();
        _isWindowModeLayoutSettling = false;

        if (_isWindowedMode && (_resizeBehavior != WindowResizeBehavior.Free))
        {
            FitWindowToCurrentImage();
            return;
        }

        ResetScaleAndCenterAfterLayout();
    }
}
