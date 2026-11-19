using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AtomicArt.Desktop.Controls;

internal sealed class ImmediateSubmenuMenuInteractionHandler : DefaultMenuInteractionHandler
{
    private readonly Dictionary<MenuItem, IDisposable> _pendingSubmenuCloses = [];

    public ImmediateSubmenuMenuInteractionHandler()
        : base(isContextMenu: true)
    {
    }

    protected override void PointerEntered(object? sender, RoutedEventArgs eventArgs)
    {
        base.PointerEntered(sender, eventArgs);

        if (eventArgs.Source is MenuItem { HasSubMenu: true, IsTopLevel: false } menuItem)
        {
            CancelSubmenuClose(menuItem);

            if (menuItem.IsEffectivelyEnabled)
            {
                menuItem.Open();
            }
        }
    }

    protected override void PointerExited(object? sender, RoutedEventArgs eventArgs)
    {
        if ((eventArgs.Source is not MenuItem { HasSubMenu: true, IsTopLevel: false } menuItem)
            || (AnimatedContextSubmenu.FindPopup(menuItem) is not Popup popup))
        {
            base.PointerExited(sender, eventArgs);

            return;
        }

        CancelSubmenuClose(menuItem);

        if (!menuItem.IsSubMenuOpen || !menuItem.IsSelected || popup.IsPointerOverPopup)
        {
            return;
        }

        _pendingSubmenuCloses[menuItem] = DispatcherTimer.RunOnce(
            () =>
            {
                CancelSubmenuClose(menuItem);

                if (!menuItem.IsPointerOver && !popup.IsPointerOverPopup)
                {
                    menuItem.Close();
                }
            },
            MenuShowDelay);
        menuItem.PropertyChanged += OnPendingSubmenuPropertyChanged;
    }

    private void OnPendingSubmenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if ((eventArgs.Property == MenuItem.IsSubMenuOpenProperty)
            && (sender is MenuItem { IsSubMenuOpen: false } menuItem))
        {
            CancelSubmenuClose(menuItem);
        }
    }

    private void CancelSubmenuClose(MenuItem menuItem)
    {
        if (_pendingSubmenuCloses.Remove(menuItem, out IDisposable? pendingClose))
        {
            menuItem.PropertyChanged -= OnPendingSubmenuPropertyChanged;
            pendingClose.Dispose();
        }
    }
}
