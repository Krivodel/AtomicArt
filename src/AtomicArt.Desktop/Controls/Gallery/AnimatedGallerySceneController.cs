using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class AnimatedGallerySceneController
{
    internal AnimatedGalleryScene? Scene => _scene;
    internal bool HasRenderedScene { get; set; }

    private readonly AnimatedGalleryControl _owner;
    private readonly ScrollViewer _scrollViewer;
    private readonly Canvas _galleryPanel;
    private readonly Canvas _overlayCanvas;
    private readonly IAnimatedGallerySceneFactory? _sceneFactory;
    private readonly Func<bool> _isAttached;
    private readonly Action _cancelResizeAnimation;
    private readonly List<object> _sceneItems = [];
    private IAnimatedGalleryOperationsRegistration? _registeredOperationsRegistration;
    private IAnimatedGalleryOperations? _registeredSceneOperations;
    private AnimatedGalleryScene? _scene;

    internal AnimatedGallerySceneController(
        AnimatedGalleryControl owner,
        ScrollViewer scrollViewer,
        Canvas galleryPanel,
        Canvas overlayCanvas,
        IAnimatedGallerySceneFactory? sceneFactory,
        Func<bool> isAttached,
        Action cancelResizeAnimation)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
        _galleryPanel = galleryPanel ?? throw new ArgumentNullException(nameof(galleryPanel));
        _overlayCanvas = overlayCanvas ?? throw new ArgumentNullException(nameof(overlayCanvas));
        _sceneFactory = sceneFactory;
        _isAttached = isAttached ?? throw new ArgumentNullException(nameof(isAttached));
        _cancelResizeAnimation = cancelResizeAnimation ?? throw new ArgumentNullException(nameof(cancelResizeAnimation));
    }

    internal static AnimatedGalleryScene RequireScene(AnimatedGalleryScene? scene)
    {
        return scene
            ?? throw new InvalidOperationException("Animated gallery scene was not created.");
    }

    internal void EnsureScene()
    {
        if (_scene is not null)
        {
            AttachScene();
            RegisterSceneOperations();
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(_owner);
        if (topLevel is null)
        {
            return;
        }

        _scene = GetSceneFactory().Create(topLevel);
        AttachScene();
        RegisterSceneOperations();
    }

    internal void RefreshItems()
    {
        _sceneItems.Clear();

        IEnumerable<object>? items = _owner.Items?.Cast<object>();
        if (items is null)
        {
            return;
        }

        foreach (object item in items)
        {
            _sceneItems.Add(item);
        }
    }

    internal void RefreshScene()
    {
        if (!_isAttached())
        {
            return;
        }

        _cancelResizeAnimation();
        EnsureScene();
        if (_scene is null)
        {
            return;
        }

        _scene.GalleryLayout.RenderCards(_scene.OperationCoordinator);
        HasRenderedScene = true;
        UpdateCardCommands();
    }

    internal void RefreshVirtualizedScene()
    {
        if (!_isAttached())
        {
            return;
        }

        EnsureScene();
        if (_scene is null)
        {
            return;
        }

        _scene.GalleryLayout.RefreshGalleryVirtualization(_scene.OperationCoordinator);
        HasRenderedScene = true;
        UpdateCardCommands();
    }

    internal void RegisterSceneOperations()
    {
        if (_scene is null)
        {
            return;
        }

        if (_owner.Operations is not IAnimatedGalleryOperationsRegistration registration)
        {
            return;
        }

        if (ReferenceEquals(_registeredOperationsRegistration, registration)
            && ReferenceEquals(_registeredSceneOperations, _scene.OperationCoordinator))
        {
            return;
        }

        DetachSceneOperations();
        registration.Attach(_scene.OperationCoordinator);
        _registeredOperationsRegistration = registration;
        _registeredSceneOperations = _scene.OperationCoordinator;
    }

    internal void DetachScene()
    {
        HasRenderedScene = false;
        DetachSceneOperations();
        _scene?.OperationCoordinator.CardControls.Clear();
        _scene?.OperationCoordinator.HiddenItemIds.Clear();
        _galleryPanel.Children.Clear();
        _overlayCanvas.Children.Clear();
        _scene?.Dispose();
        _scene = null;
    }

    internal void DetachSceneOperations()
    {
        if ((_registeredOperationsRegistration is not null)
            && (_registeredSceneOperations is not null))
        {
            _registeredOperationsRegistration.Detach(_registeredSceneOperations);
        }

        _registeredOperationsRegistration = null;
        _registeredSceneOperations = null;
    }

    internal void UpdateCardCommands()
    {
        if (_scene is null)
        {
            return;
        }

        foreach (Control control in _scene.OperationCoordinator.CardControls.Values)
        {
            _scene.CardControlFactory.ApplyCommands(control, CreateCardCommands());
        }
    }

    internal Task WaitForLayoutAsync()
    {
        _scrollViewer.UpdateLayout();
        _galleryPanel.UpdateLayout();
        _overlayCanvas.UpdateLayout();

        return Task.CompletedTask;
    }

    private void AttachScene()
    {
        if (_scene is null)
        {
            return;
        }

        _scene.OperationCoordinator.AttachScene(
            _scrollViewer,
            _galleryPanel,
            _overlayCanvas,
            _sceneItems,
            _owner.GetItemId,
            CreateCard,
            WaitForLayoutAsync);
    }

    private IAnimatedGallerySceneFactory GetSceneFactory()
    {
        if (_owner.Operations is IAnimatedGallerySceneFactoryProvider sceneFactoryProvider)
        {
            return sceneFactoryProvider.SceneFactory;
        }

        if (_sceneFactory is not null)
        {
            return _sceneFactory;
        }

        throw new InvalidOperationException("Animated gallery control requires gallery operations with a scene factory.");
    }

    private Control CreateCard(object item)
    {
        AnimatedGalleryScene scene = AnimatedGallerySceneController.RequireScene(_scene);

        return scene.CardControlFactory.Create(
            item,
            CreateCardCommands(),
            _owner.PreviewExpansionHost);
    }

    private GalleryCardCommands CreateCardCommands()
    {
        return new GalleryCardCommands(
            _owner.OpenViewerCommand,
            _owner.RevealInFolderCommand,
            _owner.OpenMetadataCommand,
            _owner.DeleteOrCancelCommand);
    }

}
