using Avalonia;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.Behaviors;

public static class DragHoverBehavior
{
    public static readonly AttachedProperty<IRelayCommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, IRelayCommand?>("Command", typeof(DragHoverBehavior));

    private static readonly AttachedProperty<DragHoverState?> StateProperty =
        AvaloniaProperty.RegisterAttached<Control, DragHoverState?>("State", typeof(DragHoverBehavior));

    static DragHoverBehavior()
    {
        CommandProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
    }

    public static IRelayCommand? GetCommand(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, CommandProperty);
    }

    public static void SetCommand(Control control, IRelayCommand? value)
    {
        AttachedPropertyValueAccessor.Set(control, CommandProperty, value);
    }

    private static void OnCommandChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        control.GetValue(StateProperty)?.Dispose();
        control.SetValue(StateProperty, args.NewValue is IRelayCommand ? new DragHoverState(control) : null);
    }
}
