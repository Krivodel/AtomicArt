using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Behaviors;

public static class PromptTextSizeWheelBehavior
{
    public static readonly AttachedProperty<IAsyncRelayCommand<PromptTextSizeAdjustment>?>
        AdjustCommandProperty =
            AvaloniaProperty.RegisterAttached<
                Control,
                IAsyncRelayCommand<PromptTextSizeAdjustment>?>(
                "AdjustCommand",
                typeof(PromptTextSizeWheelBehavior));

    static PromptTextSizeWheelBehavior()
    {
        AdjustCommandProperty.Changed.AddClassHandler<Control>(OnAdjustCommandChanged);
    }

    public static IAsyncRelayCommand<PromptTextSizeAdjustment>? GetAdjustCommand(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, AdjustCommandProperty);
    }

    public static void SetAdjustCommand(
        Control control,
        IAsyncRelayCommand<PromptTextSizeAdjustment>? value)
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

        PromptTextSizeAdjustment adjustment = e.Delta.Y > 0d
            ? PromptTextSizeAdjustment.Increase
            : PromptTextSizeAdjustment.Decrease;
        IAsyncRelayCommand<PromptTextSizeAdjustment>? command = GetAdjustCommand(control);

        if (command?.CanExecute(adjustment) == true)
        {
            command.Execute(adjustment);
        }

        e.Handled = true;
    }
}
