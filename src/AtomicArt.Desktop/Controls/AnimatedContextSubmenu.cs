using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls;

internal sealed class AnimatedContextSubmenu : IDisposable
{
    private const string AnimationStyleClass = "custom-submenu-animation";
    private const string PopupPartName = "PART_Popup";

    private readonly MenuItem _menuItem;
    private readonly UiAnimationScheduler _animationScheduler;
    private readonly bool _addedStyleClass;
    private Popup? _popup;
    private LayoutTransformControl? _layoutTransform;
    private ContextMenuRevealHost? _revealHost;
    private int _animationVersion;

    internal AnimatedContextSubmenu(
        MenuItem menuItem,
        UiAnimationScheduler animationScheduler)
    {
        _menuItem = menuItem ?? throw new ArgumentNullException(nameof(menuItem));
        _animationScheduler = animationScheduler
            ?? throw new ArgumentNullException(nameof(animationScheduler));
        _addedStyleClass = !_menuItem.Classes.Contains(AnimationStyleClass);

        if (_addedStyleClass)
        {
            _menuItem.Classes.Add(AnimationStyleClass);
        }

        _menuItem.SubmenuOpened += OnSubmenuOpened;
        _menuItem.PropertyChanged += OnMenuItemPropertyChanged;
    }

    public void Dispose()
    {
        _menuItem.SubmenuOpened -= OnSubmenuOpened;
        _menuItem.PropertyChanged -= OnMenuItemPropertyChanged;
        ReleaseRevealHost();

        if (_addedStyleClass)
        {
            _menuItem.Classes.Remove(AnimationStyleClass);
        }
    }

    private void StartOpeningAnimation(int animationVersion)
    {
        if ((animationVersion != _animationVersion)
            || !_menuItem.IsSubMenuOpen
            || (_popup?.IsOpen != true)
            || _revealHost is not ContextMenuRevealHost revealHost)
        {
            return;
        }

        revealHost.AnimateOpen(
            _animationScheduler,
            ContextMenuRevealOriginResolver.ResolveSubmenu(
                _menuItem,
                revealHost),
            () => CompleteOpeningAnimation(animationVersion));
    }

    private void CompleteOpeningAnimation(int animationVersion)
    {
        if ((animationVersion == _animationVersion)
            && _menuItem.IsSubMenuOpen)
        {
            _revealHost?.CompleteReveal();
        }
    }

    private void ReleaseRevealHost()
    {
        _animationVersion++;

        if (_revealHost is not ContextMenuRevealHost revealHost)
        {
            return;
        }

        _animationScheduler.Cancel(revealHost);
        Control content = revealHost.DetachContent();
        revealHost.Dispose();

        if ((_layoutTransform is LayoutTransformControl layoutTransform)
            && (_popup is Popup popup)
            && ReferenceEquals(popup.Child, revealHost))
        {
            popup.Child = null;
            layoutTransform.Child = content;
            popup.Child = layoutTransform;
        }

        _revealHost = null;
        _layoutTransform = null;
        _popup = null;
    }

    private void OnSubmenuOpened(object? sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;

        if (!ReferenceEquals(sender, _menuItem))
        {
            return;
        }

        ReleaseRevealHost();

        Popup? popup = _menuItem
            .GetVisualDescendants()
            .OfType<Popup>()
            .FirstOrDefault(candidate => string.Equals(
                candidate.Name,
                PopupPartName,
                StringComparison.Ordinal));

        if ((popup?.Child is not LayoutTransformControl layoutTransform)
            || (layoutTransform.Child is not Control content))
        {
            return;
        }

        popup.Child = null;
        layoutTransform.Child = null;
        ContextMenuRevealHost revealHost = new(content);
        popup.Child = revealHost;
        _popup = popup;
        _layoutTransform = layoutTransform;
        _revealHost = revealHost;
        int animationVersion = ++_animationVersion;
        _animationScheduler.RequestAnimationFrame(
            _ => StartOpeningAnimation(animationVersion));
    }

    private void OnMenuItemPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if ((eventArgs.Property == MenuItem.IsSubMenuOpenProperty)
            && !_menuItem.IsSubMenuOpen)
        {
            ReleaseRevealHost();
        }
    }
}
