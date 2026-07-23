using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

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
        _topLevel = null;
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.Handled || !IsDismissKey(e.Key))
        {
            return;
        }

        ModalOverlayPresenterControl? topmostOverlay = Children
            .OfType<ModalOverlayPresenterControl>()
            .Where(overlay => overlay.IsOpen)
            .OrderBy(overlay => overlay.Order)
            .LastOrDefault();
        ICommand? closeCommand = topmostOverlay?.CloseCommand;

        if (closeCommand is null || !closeCommand.CanExecute(null))
        {
            return;
        }

        closeCommand.Execute(null);
        e.Handled = true;
    }
}
