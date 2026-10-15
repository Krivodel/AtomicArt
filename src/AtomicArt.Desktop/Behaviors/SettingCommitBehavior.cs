using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AtomicArt.Desktop.Behaviors;

public static class SettingCommitBehavior
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>(
            "Command",
            typeof(SettingCommitBehavior));

    static SettingCommitBehavior()
    {
        CommandProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
    }

    public static ICommand? GetCommand(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, CommandProperty);
    }

    public static void SetCommand(Control control, ICommand? value)
    {
        AttachedPropertyValueAccessor.Set(control, CommandProperty, value);
    }

    private static void OnCommandChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        control.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        control.RemoveHandler(InputElement.LostFocusEvent, OnLostFocus);

        if (args.NewValue is not null)
        {
            control.AddHandler(
                InputElement.KeyDownEvent,
                OnKeyDown,
                RoutingStrategies.Tunnel);
            control.AddHandler(InputElement.LostFocusEvent, OnLostFocus);
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is Control control
            && e.Key == Key.Enter
            && TryExecute(control))
        {
            e.Handled = true;
        }
    }

    private static void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            TryExecute(control);
        }
    }

    private static bool TryExecute(Control control)
    {
        ICommand? command = GetCommand(control);

        if (command?.CanExecute(null) != true)
        {
            return false;
        }

        command.Execute(null);
        return true;
    }
}
