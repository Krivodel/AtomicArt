using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SukiUI.Controls;
using Pica.Viewer.Services;
using Pica.Protocol;

namespace Pica.Viewer.Views;

public sealed partial class ImageViewerWindow : SukiWindow
{
    private const string AppIconAssetUri = "avares://Pica.Viewer/Assets/AppIcon.ico";
    private const double EdgeRevealRatio = 0.04d;
    private const double BottomRevealSize = 128d;
    private const double ContextMenuGap = 8d;
    private const double ContextMenuFallbackWidth = 172d;
    private const double ContextMenuFallbackHeight = 260d;
    private const double OpenWithMenuFallbackWidth = 220d;
    private const double OpenWithMenuFallbackHeight = 52d;
    private const double SelectionToolbarGap = 10d;
    private const double SelectionHandleSize = 8d;
    private const double MinimumSelectionSize = 12d;
    private const double DefaultZoomButtonFactor = 1.2d;
    private const double WheelZoomBase = 1.0015d;
    private const double SettingsPanelAnimationDurationSeconds = 0.16d;
    private const double ScaleAnimationDurationSeconds = 0.14d;
    private const double CopyFeedbackOpacity = 0.44d;
    private const double CopyFeedbackFadeInDurationSeconds = 0.1d;
    private const double CopyFeedbackFadeOutDurationSeconds = 0.08d;
    private const double MinimumScale = 0.05d;
    private const double MaximumScale = 32d;
    private const int CursorHideDelayMilliseconds = 1000;
    private const int OpenWithMenuHideDelayMilliseconds = 120;
    private const int WindowModeLayoutSettleDelayMilliseconds = 100;
    private const double DefaultWindowWidth = 1280d;
    private const double DefaultWindowHeight = 800d;
    private const double WindowedTitleBarHeight = 36d;
    private const double MinimumWindowWidth = 300d;
    private const double TitleLogoSize = 28d;

    private static readonly TimeSpan ScaleAnimationDuration = TimeSpan.FromSeconds(ScaleAnimationDurationSeconds);
    private static readonly TimeSpan CopyFeedbackFadeInDuration =
        TimeSpan.FromSeconds(CopyFeedbackFadeInDurationSeconds);
    private static readonly TimeSpan CopyFeedbackFadeOutDuration =
        TimeSpan.FromSeconds(CopyFeedbackFadeOutDurationSeconds);
    private static readonly TimeSpan SettingsPanelAnimationDuration =
        TimeSpan.FromSeconds(SettingsPanelAnimationDurationSeconds);
    private static readonly double ZoomButtonStepBase =
        Math.Pow(DefaultZoomButtonFactor, 1d / ViewerSettingsDefaults.ZoomSpeed);
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor HiddenCursor = new(StandardCursorType.None);
    private static readonly Cursor CrosshairCursor = new(StandardCursorType.Cross);
    private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor HorizontalResizeCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor VerticalResizeCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor TopLeftResizeCursor = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor TopRightResizeCursor = new(StandardCursorType.TopRightCorner);
    private readonly PicaViewerRequest _request;
    private readonly IImageViewerStateService _imageViewerStateService;
    private readonly ImagePreviewLoader _imagePreviewLoader;
    private readonly FullResolutionImageLoader _fullResolutionImageLoader;
    private readonly ClipboardImagePreparer _clipboardImagePreparer;
    private readonly TemporarySelectionFileStore _temporarySelectionFileStore;
    private readonly IPlatformFileActions _platformFileActions;
    private readonly ILogger<ImageViewerWindow> _logger;
    private readonly ViewerImageOperations _imageOperations;
    private readonly ImageDoubleClickTracker _imageDoubleClickTracker;
    private readonly ImagePanMotion _panMotion;
    private readonly Bitmap _logoBitmap;
    private readonly ImageViewerView _view;
    private readonly DispatcherTimer _cursorTimer;
    private readonly DispatcherTimer _windowModeLayoutTimer;
    private readonly DispatcherTimer _openWithMenuHideTimer;
    private readonly ImagePreviewCache _previewCache;
    private Bitmap? _bitmap;
    private PicaImageItem? _currentItem;
    private PixelSize _sourcePixelSize;
    private int _selectedIndex;
    private int _movementSpeed;
    private int _zoomSpeed;
    private int _preferredNavigationDirection = 1;
    private double _scale = 1d;
    private double _offsetX;
    private double _offsetY;
    private bool _isPointerPressed;
    private bool _isPanning;
    private bool _isSelecting;
    private bool _isSelectionActive;
    private bool _isSelectionMoving;
    private bool _isSelectionArmed;
    private bool _isControlModifierActive;
    private bool _isFilteringEnabled;
    private bool _expandOnDoubleClick;
    private bool _isFastLoadingEnabled;
    private bool _allowFreeZoomOut;
    private bool _isSmoothPanningEnabled;
    private bool _isPanningInertiaEnabled;
    private bool _isFullResolutionImageReady;
    private bool _isImageOperationRunning;
    private bool _rememberWindowPlacement;
    private WindowResizeBehavior _resizeBehavior;
    private KeyModifiers _activeKeyModifiers;
    private SelectionResizeMode _selectionResizeMode;
    private Point _pointerPressPosition;
    private Point _lastPointerPosition;
    private Point _lastPointerHoverPosition;
    private PixelPoint? _lastPointerScreenPosition;
    private Point _selectionStartPosition;
    private Rect _selectionRect;
    private Rect _selectionStartRect;
    private PixelRect _selectionPixelRect;
    private PixelRect _selectionStartPixelRect;
    private long _scaleAnimationId;
    private long _copyFeedbackAnimationId;
    private long _selectionOverlayAnimationId;
    private long _settingsPanelAnimationId;
    private long _imageLoadId;
    private bool _isChangingWindowMode;
    private bool _isWindowedMode;
    private bool _isApplyingWindowGeometry;
    private bool _isImageClickCandidate;
    private bool _isCursorHidden;
    private bool _isSavingStateBeforeClose;
    private bool _isClosingAfterStateSave;
    private bool _isWindowResizeLayoutPending;
    private bool _isWindowModeLayoutSettling;
    private bool _isPanAnimationFramePending;
    private DateTimeOffset _lastPanAnimationFrameTimestamp;
    private PixelPoint? _windowedPosition;
    private Size? _windowedClientSize;
    private double _windowedPreferredExtent;
    private IWindowResizeSession? _windowResizeSession;
    private CancellationTokenSource? _imageLoadCancellation;
    private CancellationTokenSource? _selectionPreparationCancellation;
    private Task? _activeImageLoadTask;
    private Task? _previewCachePrimingTask;
    private Task<PreparedClipboardImage?>? _selectionPreparationTask;
    private PixelRect _selectionPreparationRect;
    private OpenWithTarget _openWithTarget;
    private Control? _openWithAnchor;

    internal ImageViewerWindow(
        PicaViewerRequest request,
        IViewerClipboardWriter clipboardImageWriter,
        IImageFormatRegistry formatRegistry,
        IImageViewerStateService imageViewerStateService,
        ImagePreviewLoader imagePreviewLoader,
        FullResolutionImageLoader fullResolutionImageLoader,
        PngImageEncoder pngImageEncoder,
        ClipboardImagePreparer clipboardImagePreparer,
        TemporarySelectionFileStore temporarySelectionFileStore,
        IPlatformFileActions platformFileActions,
        IViewerActionDispatcher actionDispatcher,
        ILogger<ImageViewerWindow> logger,
        ImageViewerState initialState)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _imageViewerStateService = imageViewerStateService
            ?? throw new ArgumentNullException(nameof(imageViewerStateService));
        _imagePreviewLoader = imagePreviewLoader
            ?? throw new ArgumentNullException(nameof(imagePreviewLoader));
        _fullResolutionImageLoader = fullResolutionImageLoader
            ?? throw new ArgumentNullException(nameof(fullResolutionImageLoader));
        _clipboardImagePreparer = clipboardImagePreparer
            ?? throw new ArgumentNullException(nameof(clipboardImagePreparer));
        _temporarySelectionFileStore = temporarySelectionFileStore
            ?? throw new ArgumentNullException(nameof(temporarySelectionFileStore));
        _platformFileActions = platformFileActions
            ?? throw new ArgumentNullException(nameof(platformFileActions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _imageOperations = new ViewerImageOperations(
            clipboardImageWriter,
            formatRegistry,
            pngImageEncoder,
            actionDispatcher);
        ArgumentNullException.ThrowIfNull(initialState);
        _selectedIndex = GetItemIndexOrDefault(request.Items, request.SelectedItemId);
        _isFilteringEnabled = initialState.IsFilteringEnabled;
        _movementSpeed = initialState.MovementSpeed;
        _zoomSpeed = initialState.ZoomSpeed;
        _expandOnDoubleClick = initialState.ExpandOnDoubleClick;
        _isFastLoadingEnabled = initialState.IsFastLoadingEnabled;
        _allowFreeZoomOut = initialState.AllowFreeZoomOut;
        _isSmoothPanningEnabled = initialState.IsSmoothPanningEnabled;
        _isPanningInertiaEnabled = initialState.IsPanningInertiaEnabled;
        _resizeBehavior = initialState.ResizeBehavior;
        _rememberWindowPlacement = initialState.RememberWindowPlacement;
        _isWindowedMode = _rememberWindowPlacement && (initialState.IsWindowed == true);
        _windowedPosition = _rememberWindowPlacement
            ? CreateWindowedPosition(initialState)
            : null;
        _windowedClientSize = _rememberWindowPlacement
            ? CreateWindowedClientSize(initialState)
            : null;
        Size initialWindowedClientSize = _windowedClientSize
            ?? new Size(DefaultWindowWidth, DefaultWindowHeight);
        _windowedPreferredExtent = Math.Max(
            initialWindowedClientSize.Width,
            Math.Max(1d, initialWindowedClientSize.Height - WindowedTitleBarHeight));
        _imageDoubleClickTracker = new ImageDoubleClickTracker();
        _panMotion = new ImagePanMotion();
        _previewCache = new ImagePreviewCache();
        _logoBitmap = LoadBitmap(AppIconAssetUri);
        ImageViewerViewEvents viewEvents = new()
        {
            ZoomOutClicked = OnZoomOutClicked,
            ResetClicked = OnResetClicked,
            ZoomInClicked = OnZoomInClicked,
            CloseClicked = OnCloseClicked,
            WindowModeClicked = OnWindowModeClicked,
            SettingsClicked = OnSettingsClicked,
            ContextCopyClicked = OnContextCopyClicked,
            ContextExternalActionClicked = OnContextExternalActionClicked,
            ContextSaveAsClicked = OnContextSaveAsClicked,
            ContextRevealInFolderClicked = OnContextRevealInFolderClicked,
            ContextOpenWithClicked = OnContextOpenWithClicked,
            ContextSelectAreaClicked = OnContextSelectAreaClicked,
            SelectionCopyClicked = OnSelectionCopyClicked,
            SelectionExternalActionClicked = OnSelectionExternalActionClicked,
            SelectionOpenWithClicked = OnSelectionOpenWithClicked,
            SelectionSaveAsClicked = OnSelectionSaveAsClicked,
            SelectionCancelClicked = OnSelectionCancelClicked,
            WindowResizePointerPressed = OnWindowResizePointerPressed,
            WindowResizePointerMoved = OnWindowResizePointerMoved,
            WindowResizePointerReleased = OnWindowResizePointerReleased
        };
        _view = new ImageViewerView(
            initialState,
            request.Actions,
            GetViewerWindowMode(),
            viewEvents);
        _view.ContextOpenWithButton.IsVisible = _platformFileActions.SupportsOpenWith;
        _view.SelectionOpenWithButton.IsVisible = _platformFileActions.SupportsOpenWith;
        _view.ApplyImageFiltering(_isFilteringEnabled);
        _cursorTimer = CreateCursorTimer();
        _windowModeLayoutTimer = CreateWindowModeLayoutTimer();
        _openWithMenuHideTimer = CreateOpenWithMenuHideTimer();

        ConfigureWindow();
        AttachEvents();
    }

    internal async Task PersistStateAsync(CancellationToken ct)
    {
        CaptureWindowedPlacement();
        await _imageViewerStateService.SaveAsync(CreateState(), ct);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _logger.LogInformation(
            "Pica viewer opened with {ItemCount} images in {WindowMode} mode",
            _request.Items.Count,
            GetViewerWindowMode());

        if (Clipboard is { } clipboard)
        {
            _imageOperations.AttachClipboard(clipboard);
        }

        LoadSelectedImage();
        ApplyInitialWindowMode();
        _view.FadeOverlay.Opacity = ImageViewerVisualMetrics.HiddenControlsOpacity;
        _cursorTimer.Start();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_isClosingAfterStateSave)
        {
            base.OnClosing(e);
            return;
        }

        base.OnClosing(e);

        if (e.Cancel)
        {
            return;
        }

        e.Cancel = true;

        if (_isSavingStateBeforeClose)
        {
            return;
        }

        _isSavingStateBeforeClose = true;

        try
        {
            await PersistStateAsync(CancellationToken.None);
            _logger.LogDebug("Persisted Pica viewer state before closing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save the Pica window position before closing.");
        }

        _isClosingAfterStateSave = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.LogInformation("Pica viewer closed");
        _ = _imageOperations.FlushClipboardAsync(CancellationToken.None);
        base.OnClosed(e);

        _cursorTimer.Stop();
        _windowModeLayoutTimer.Stop();
        _openWithMenuHideTimer.Stop();
        StopScaleAnimation();
        StopPanMotion();
        CancelPendingImageLoad();
        CancelSelectionClipboardPreparation();
        _view.Dispose();
        _temporarySelectionFileStore.Dispose();
        _previewCache.Dispose();
        ReleaseDisplayedBitmap();
        _logoBitmap.Dispose();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property != WindowStateProperty) || _isChangingWindowMode)
        {
            return;
        }

        if ((WindowState == WindowState.FullScreen) || (WindowState == WindowState.Maximized))
        {
            EnterFullScreenMode();
        }
        else if ((WindowState == WindowState.Normal) && !_isWindowedMode)
        {
            EnterWindowedMode();
        }
    }

    private void ConfigureWindow()
    {
        Background = Brushes.Black;
        CanResize = false;
        CanFullScreen = true;
        CanPin = false;
        Cursor = ArrowCursor;
        Icon = LoadWindowIcon(AppIconAssetUri);
        IsMenuVisible = false;
        IsTitleBarVisible = _isWindowedMode;
        LogoContent = new Image
        {
            Width = TitleLogoSize,
            Height = TitleLogoSize,
            Source = _logoBitmap,
            Stretch = Stretch.Uniform
        };
        MinWidth = MinimumWindowWidth;
        RightWindowTitleBarControls = _view.TitleBarSettingsControls;
        ShowBottomBorder = false;
        ShowInTaskbar = true;
        ShowTitlebarBackground = _isWindowedMode;
        Title = PicaProtocolConstants.ApplicationName;
        TitleBarAnimationEnabled = false;
        TitleBarVisibilityOnFullScreen = TitleBarVisibilityMode.Hidden;
        WindowState = _isWindowedMode
            ? WindowState.Normal
            : WindowState.FullScreen;
        Content = _view.Root;

        if (_isWindowedMode && (_windowedClientSize is { } windowedClientSize))
        {
            ApplyWindowedClientSize(windowedClientSize);
            ApplyWindowedPosition(windowedClientSize);
        }
    }

    private static Bitmap LoadBitmap(string assetUri)
    {
        using Stream stream = AssetLoader.Open(new Uri(assetUri));

        return new Bitmap(stream);
    }

    private static WindowIcon LoadWindowIcon(string assetUri)
    {
        using Stream stream = AssetLoader.Open(new Uri(assetUri));

        return new WindowIcon(stream);
    }

    private DispatcherTimer CreateCursorTimer()
    {
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(CursorHideDelayMilliseconds)
        };
        timer.Tick += OnCursorTimerTick;

        return timer;
    }

    private DispatcherTimer CreateWindowModeLayoutTimer()
    {
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(WindowModeLayoutSettleDelayMilliseconds)
        };
        timer.Tick += OnWindowModeLayoutTimerTick;

        return timer;
    }

    private DispatcherTimer CreateOpenWithMenuHideTimer()
    {
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(OpenWithMenuHideDelayMilliseconds)
        };
        timer.Tick += OnOpenWithMenuHideTimerTick;

        return timer;
    }

    private void AttachEvents()
    {
        _view.ViewerArea.PointerPressed += OnPointerPressed;
        _view.ViewerArea.PointerMoved += OnPointerMoved;
        _view.ViewerArea.PointerReleased += OnPointerReleased;
        _view.ViewerArea.PointerWheelChanged += OnPointerWheelChanged;
        _view.ViewerArea.SizeChanged += OnViewerAreaSizeChanged;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        PositionChanged += OnWindowPositionChanged;
        Resized += OnWindowResized;
        _view.FilteringToggle.PropertyChanged += OnFilteringTogglePropertyChanged;
        _view.LeftNavigationArea.PointerPressed += OnLeftNavigationPressed;
        _view.RightNavigationArea.PointerPressed += OnRightNavigationPressed;
        _view.ContextMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.ContextOpenWithButton.PointerEntered += OnContextOpenWithAnchorPointerEntered;
        _view.ContextOpenWithButton.PointerExited += OnOpenWithAnchorPointerExited;
        _view.SelectionOpenWithButton.PointerExited += OnOpenWithAnchorPointerExited;
        _view.OpenWithMenu.PointerEntered += OnOpenWithMenuPointerEntered;
        _view.OpenWithMenu.PointerExited += OnOpenWithMenuPointerExited;
        _view.OpenWithMenu.PointerPressed += OnFloatingMenuPointerPressed;
        _view.SelectionToolbar.PointerPressed += OnFloatingMenuPointerPressed;
        _view.SettingsPanel.PointerPressed += OnFloatingMenuPointerPressed;
        _view.Root.PointerExited += OnRootPointerExited;
        _view.SettingsPanel.MovementSpeedComboBox.SelectionChanged += OnMovementSpeedSelectionChanged;
        _view.SettingsPanel.ZoomSpeedComboBox.SelectionChanged += OnZoomSpeedSelectionChanged;
        _view.SettingsPanel.ExpandOnDoubleClickCheckBox.IsCheckedChanged += OnExpandOnDoubleClickChanged;
        _view.SettingsPanel.FastLoadingCheckBox.IsCheckedChanged += OnFastLoadingChanged;
        _view.SettingsPanel.AllowFreeZoomOutCheckBox.IsCheckedChanged += OnAllowFreeZoomOutChanged;
        _view.SettingsPanel.SmoothPanningCheckBox.IsCheckedChanged += OnSmoothPanningChanged;
        _view.SettingsPanel.PanningInertiaCheckBox.IsCheckedChanged += OnPanningInertiaChanged;
        _view.SettingsPanel.ResizeBehaviorComboBox.SelectionChanged += OnResizeBehaviorSelectionChanged;
        _view.SettingsPanel.RememberWindowPlacementCheckBox.IsCheckedChanged +=
            OnRememberWindowPlacementChanged;
    }

    private enum SelectionResizeMode
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }
}
