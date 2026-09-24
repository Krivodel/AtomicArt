using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;

namespace AtomicArt.Desktop.Controls;

internal sealed class ImmediateSubmenuMenuInteractionHandler : DefaultMenuInteractionHandler
{
    public ImmediateSubmenuMenuInteractionHandler()
        : base(isContextMenu: true)
    {
    }

    protected override void PointerEntered(object? sender, RoutedEventArgs eventArgs)
    {
        base.PointerEntered(sender, eventArgs);

        if (eventArgs.Source is MenuItem
            {
                HasSubMenu: true,
                IsTopLevel: false,
                IsEffectivelyEnabled: true
            } menuItem)
        {
            menuItem.Open();
        }
    }
}
