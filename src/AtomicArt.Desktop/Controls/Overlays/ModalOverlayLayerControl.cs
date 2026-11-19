using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Controls.Overlays;

public sealed class ModalOverlayLayerControl : Grid
{
    private TopLevel? _topLevel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        DetachKeyboardHandler();
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(
            InputElement.KeyDownEvent,
            OnTopLevelPreviewKeyDown,
            RoutingStrategies.Tunnel);
        _topLevel?.AddHandler(
            InputElement.KeyDownEvent,
            OnTopLevelKeyDown,
            RoutingStrategies.Bubble,
            false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachKeyboardHandler();

        base.OnDetachedFromVisualTree(e);
    }

    private static bool IsDismissKey(Key key)
    {
        return key is Key.Escape or Key.Cancel;
    }

    private void DetachKeyboardHandler()
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown);
        _topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelPreviewKeyDown);
        _topLevel = null;
    }

    private ModalOverlayPresenterControl? GetTopmostOpenOverlay()
    {
        return Children.OfType<ModalOverlayPresenterControl>()
            .Where(overlay => overlay.IsOpen)
            .OrderBy(overlay => overlay.Order)
            .LastOrDefault();
    }

    private void OnTopLevelPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None
            || GetNavigationDirection(e) is not { } direction
            || GetTopmostOpenOverlay()?.NavigateCommand is not { } command)
        {
            return;
        }

        e.Handled = true;
        if (command.CanExecute(direction))
        {
            command.Execute(direction);
        }
    }

    private static OverlayNavigationDirection? GetNavigationDirection(KeyEventArgs e)
    {
        return e.Key switch
        {
            Key.Left or Key.A => OverlayNavigationDirection.Previous,
            Key.Right or Key.D => OverlayNavigationDirection.Next,
            _ => e.PhysicalKey switch
            {
                PhysicalKey.A => OverlayNavigationDirection.Previous,
                PhysicalKey.D => OverlayNavigationDirection.Next,
                _ => null
            }
        };
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.Handled || !IsDismissKey(e.Key))
        {
            return;
        }

        ICommand? closeCommand = GetTopmostOpenOverlay()?.CloseCommand;

        if (closeCommand is null || !closeCommand.CanExecute(null))
        {
            return;
        }

        closeCommand.Execute(null);
        e.Handled = true;
    }
}
