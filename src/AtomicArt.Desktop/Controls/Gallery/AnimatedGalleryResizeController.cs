using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class AnimatedGalleryResizeController
{
    private const double MinimumMeasuredSize = 1d;
    private const double SizeComparisonTolerance = 0.5d;

    private readonly AnimatedGalleryControl _owner;
    private readonly ScrollViewer _scrollViewer;
    private readonly AnimatedGallerySceneController _sceneController;
    private readonly Func<bool> _isAttached;
    private readonly ILogger<AnimatedGalleryResizeController> _logger;
    private readonly GalleryAnimationTracker _animatedControls = [];
    private Size _lastAvailableAreaSize;
    private int _resizeVersion;
    private bool _animationScheduled;

    internal AnimatedGalleryResizeController(
        AnimatedGalleryControl owner,
        ScrollViewer scrollViewer,
        AnimatedGallerySceneController sceneController,
        Func<bool> isAttached,
        ILogger<AnimatedGalleryResizeController> logger)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
        _sceneController = sceneController ?? throw new ArgumentNullException(nameof(sceneController));
        _isAttached = isAttached ?? throw new ArgumentNullException(nameof(isAttached));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal void Attach()
    {
        _owner.SizeChanged -= OnSizeChanged;
        _owner.SizeChanged += OnSizeChanged;
        _scrollViewer.SizeChanged -= OnSizeChanged;
        _scrollViewer.SizeChanged += OnSizeChanged;
        _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
        _scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
    }

    internal void Detach()
    {
        _animationScheduled = false;
        _owner.SizeChanged -= OnSizeChanged;
        _scrollViewer.SizeChanged -= OnSizeChanged;
        _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
    }

    internal void Schedule()
    {
        if (!_isAttached() || _animationScheduled)
        {
            return;
        }

        _sceneController.EnsureScene();
        AnimatedGalleryScene? scene = _sceneController.Scene;
        if (scene is null)
        {
            return;
        }

        _animationScheduled = true;
        scene.AnimationScheduler.RequestAnimationFrame(_ =>
        {
            _animationScheduled = false;
            if (_isAttached())
            {
                RequestAnimation();
            }
        });
    }

    internal void CancelResizeAnimation()
    {
        _resizeVersion++;
        AnimatedGalleryScene? scene = _sceneController.Scene;
        if (scene is not null)
        {
            scene.AnimationScheduler.Cancel(_animatedControls);
        }

        ResetAnimatedControls();
        _animatedControls.Clear();
    }

    private static bool IsSameSize(Size left, Size right)
    {
        return (Math.Abs(left.Width - right.Width) < SizeComparisonTolerance)
               && (Math.Abs(left.Height - right.Height) < SizeComparisonTolerance);
    }

    private void RequestAnimation()
    {
        Size availableAreaSize = GetAvailableAreaSize();
        if (IsSameSize(availableAreaSize, _lastAvailableAreaSize))
        {
            return;
        }

        _lastAvailableAreaSize = availableAreaSize;
        _ = AnimateAvailableAreaChangeSafelyAsync();
    }

    private async Task AnimateAvailableAreaChangeSafelyAsync()
    {
        try
        {
            await AnimateAvailableAreaChangeAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to animate gallery resize.");
        }
    }

    private async Task AnimateAvailableAreaChangeAsync()
    {
        if (!PrepareSceneForResize(out AnimatedGalleryScene? scene, out int resizeVersion))
        {
            return;
        }

        scene = AnimatedGallerySceneController.RequireScene(scene);
        Dictionary<Guid, Rect> firstSnapshot = TakeResizeSnapshot(scene);
        if (firstSnapshot.Count == 0)
        {
            _sceneController.RefreshScene();
            return;
        }

        await RenderResizedSceneAsync(scene);
        if (!IsResizeStillCurrent(resizeVersion))
        {
            return;
        }

        await scene.MotionAnimator.AnimateResizeRetargetAsync(
            scene.OperationCoordinator,
            firstSnapshot,
            _animatedControls);

        FinishResizeAnimation(resizeVersion);
    }

    private Dictionary<Guid, Rect> TakeResizeSnapshot(AnimatedGalleryScene scene)
    {
        scene.GalleryLayout.SynchronizeCardControlIds(scene.OperationCoordinator);

        return scene.GalleryLayout.TakeSnapshot(scene.OperationCoordinator);
    }

    private async Task RenderResizedSceneAsync(AnimatedGalleryScene scene)
    {
        HashSet<Guid> hiddenItemIds = scene.OperationCoordinator.HiddenItemIds.ToHashSet();
        scene.GalleryLayout.RenderCards(scene.OperationCoordinator, hiddenItemIds);
        _sceneController.HasRenderedScene = true;
        _sceneController.UpdateCardCommands();
        await scene.OperationCoordinator.WaitForLayoutAsync();
    }

    private bool IsResizeStillCurrent(int resizeVersion)
    {
        return _isAttached()
               && (_sceneController.Scene is not null)
               && (resizeVersion == _resizeVersion);
    }

    private bool PrepareSceneForResize(
        out AnimatedGalleryScene? scene,
        out int resizeVersion)
    {
        resizeVersion = _resizeVersion;
        scene = null;
        if (!_isAttached())
        {
            return false;
        }

        _sceneController.EnsureScene();
        scene = _sceneController.Scene;
        if (scene is null)
        {
            return false;
        }

        if (!_sceneController.HasRenderedScene || (scene.OperationCoordinator.CardControls.Count == 0))
        {
            _sceneController.RefreshScene();
            return false;
        }

        RetargetResizeAnimation();
        resizeVersion = _resizeVersion;

        return true;
    }

    private void FinishResizeAnimation(int resizeVersion)
    {
        if (resizeVersion != _resizeVersion)
        {
            return;
        }

        ResetAnimatedControls();
        _animatedControls.Clear();
    }

    private void RetargetResizeAnimation()
    {
        _resizeVersion++;
        AnimatedGalleryScene? scene = _sceneController.Scene;
        if (scene is not null)
        {
            scene.AnimationScheduler.Cancel(_animatedControls);
        }

        _animatedControls.Clear();
    }

    private Size GetAvailableAreaSize()
    {
        Size viewport = _scrollViewer.Viewport;
        if ((viewport.Width > MinimumMeasuredSize) || (viewport.Height > MinimumMeasuredSize))
        {
            return viewport;
        }

        Size bounds = _scrollViewer.Bounds.Size;
        if ((bounds.Width > MinimumMeasuredSize) || (bounds.Height > MinimumMeasuredSize))
        {
            return bounds;
        }

        return _owner.Bounds.Size;
    }

    private void ResetAnimatedControls()
    {
        foreach (Control control in _animatedControls)
        {
            MotionFrameApplier.Apply(control, MotionFrame.Identity);
            control.ZIndex = 0;
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        Schedule();
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        _ = sender;

        if ((e.Property == ScrollViewer.ViewportProperty) || (e.Property == Visual.BoundsProperty))
        {
            Schedule();
            return;
        }

        if (e.Property == ScrollViewer.OffsetProperty)
        {
            _sceneController.RefreshVirtualizedScene();
        }
    }
}
