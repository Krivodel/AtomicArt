using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Behaviors;

public static class TextSizeWheelBehavior
{
    public static readonly AttachedProperty<IAsyncRelayCommand<TextSizeAdjustment>?>
        AdjustCommandProperty =
            AvaloniaProperty.RegisterAttached<
                Control,
                IAsyncRelayCommand<TextSizeAdjustment>?>(
                    "AdjustCommand",
                    typeof(TextSizeWheelBehavior));

    static TextSizeWheelBehavior()
    {
        AdjustCommandProperty.Changed.AddClassHandler<Control>(OnAdjustCommandChanged);
    }

    public static IAsyncRelayCommand<TextSizeAdjustment>? GetAdjustCommand(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, AdjustCommandProperty);
    }

    public static void SetAdjustCommand(
        Control control,
        IAsyncRelayCommand<TextSizeAdjustment>? value)
    {
        AttachedPropertyValueAccessor.Set(control, AdjustCommandProperty, value);
    }

    private static void OnAdjustCommandChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        control.RemoveHandler(
            InputElement.PointerWheelChangedEvent,
            OnPointerWheelChanged);

        if (args.NewValue is not null)
        {
            control.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnPointerWheelChanged,
                RoutingStrategies.Tunnel);
        }
    }

    private static void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not Control control
            || e.Handled
            || (e.KeyModifiers & KeyModifiers.Control) == 0
            || e.Delta.Y.Equals(0d))
        {
            return;
        }

        TextSizeAdjustment adjustment = e.Delta.Y > 0d
                    ? TextSizeAdjustment.Increase
                    : TextSizeAdjustment.Decrease;
        IAsyncRelayCommand<TextSizeAdjustment>? command = TextSizeWheelBehavior.GetAdjustCommand(control);

        if (command?.CanExecute(adjustment) == true)
        {
            command.Execute(adjustment);
        }

        e.Handled = true;
    }
}
