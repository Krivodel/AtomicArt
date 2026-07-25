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
        CommandProperty.Changed.AddClassHandler<TextBox>(OnCommandChanged);
    }

    public static ICommand? GetCommand(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, CommandProperty);
    }

    public static void SetCommand(Control control, ICommand? value)
    {
        AttachedPropertyValueAccessor.Set(control, CommandProperty, value);
    }

    private static void OnCommandChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs args)
    {
        textBox.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        textBox.RemoveHandler(InputElement.LostFocusEvent, OnLostFocus);

        if (args.NewValue is not null)
        {
            textBox.AddHandler(
                InputElement.KeyDownEvent,
                OnKeyDown,
                RoutingStrategies.Tunnel);
            textBox.AddHandler(InputElement.LostFocusEvent, OnLostFocus);
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox
            && e.Key == Key.Enter
            && TryExecute(textBox))
        {
            e.Handled = true;
        }
    }

    private static void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            TryExecute(textBox);
        }
    }

    private static bool TryExecute(TextBox textBox)
    {
        ICommand? command = GetCommand(textBox);

        if (command?.CanExecute(null) != true)
        {
            return false;
        }

        command.Execute(null);
        return true;
    }
}
