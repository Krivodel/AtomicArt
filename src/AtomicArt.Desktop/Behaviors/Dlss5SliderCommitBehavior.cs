using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AtomicArt.Desktop.Behaviors;

public static class Dlss5SliderCommitBehavior
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Slider, ICommand?>(
            "Command",
            typeof(Dlss5SliderCommitBehavior));

    private static readonly AttachedProperty<bool> IsTrackingProperty =
        AvaloniaProperty.RegisterAttached<Slider, bool>(
            "IsTracking",
            typeof(Dlss5SliderCommitBehavior));

    static Dlss5SliderCommitBehavior()
    {
        Dlss5SliderCommitBehavior.CommandProperty.Changed.AddClassHandler<Slider>(Dlss5SliderCommitBehavior.OnCommandChanged);
    }

    public static ICommand? GetCommand(Slider slider)
    {
        return AttachedPropertyValueAccessor.Get(slider, Dlss5SliderCommitBehavior.CommandProperty);
    }

    public static void SetCommand(Slider slider, ICommand? value)
    {
        AttachedPropertyValueAccessor.Set(slider, Dlss5SliderCommitBehavior.CommandProperty, value);
    }

    private static void Commit(Slider slider)
    {
        Dlss5SliderCommitBehavior.SetIsTracking(slider, false);
        ICommand? command = Dlss5SliderCommitBehavior.GetCommand(slider);
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    private static bool GetIsTracking(Slider slider)
    {
        return AttachedPropertyValueAccessor.Get(slider, Dlss5SliderCommitBehavior.IsTrackingProperty);
    }

    private static void SetIsTracking(Slider slider, bool value)
    {
        AttachedPropertyValueAccessor.Set(slider, Dlss5SliderCommitBehavior.IsTrackingProperty, value);
    }

    private static bool IsValueChangingKey(Key key)
    {
        return key is Key.Left
            or Key.Right
            or Key.Up
            or Key.Down
            or Key.Home
            or Key.End
            or Key.PageUp
            or Key.PageDown;
    }

    private static void OnCommandChanged(Slider slider, AvaloniaPropertyChangedEventArgs args)
    {
        slider.RemoveHandler(InputElement.PointerPressedEvent, Dlss5SliderCommitBehavior.OnPointerPressed);
        slider.RemoveHandler(InputElement.PointerReleasedEvent, Dlss5SliderCommitBehavior.OnPointerReleased);
        slider.RemoveHandler(InputElement.KeyUpEvent, Dlss5SliderCommitBehavior.OnKeyUp);
        slider.PointerCaptureLost -= Dlss5SliderCommitBehavior.OnLostPointerCapture;
        slider.PropertyChanged -= Dlss5SliderCommitBehavior.OnSliderPropertyChanged;

        if (args.NewValue is not null)
        {
            slider.AddHandler(
                InputElement.PointerPressedEvent,
                Dlss5SliderCommitBehavior.OnPointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            slider.AddHandler(
                InputElement.PointerReleasedEvent,
                Dlss5SliderCommitBehavior.OnPointerReleased,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            slider.AddHandler(
                InputElement.KeyUpEvent,
                Dlss5SliderCommitBehavior.OnKeyUp,
                RoutingStrategies.Tunnel);
            slider.PointerCaptureLost += Dlss5SliderCommitBehavior.OnLostPointerCapture;
            slider.PropertyChanged += Dlss5SliderCommitBehavior.OnSliderPropertyChanged;
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is Slider slider)
        {
            // Start a fresh drag. Value changes mark it dirty; releasing a
            // click that did not move the thumb must not trigger a render.
            Dlss5SliderCommitBehavior.SetIsTracking(slider, false);
        }
    }

    private static void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if ((sender is Slider slider) && (args.Property == RangeBase.ValueProperty))
        {
            Dlss5SliderCommitBehavior.SetIsTracking(slider, true);
        }
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if ((sender is Slider slider) && (Dlss5SliderCommitBehavior.GetIsTracking(slider)))
        {
            Dlss5SliderCommitBehavior.Commit(slider);
        }
    }

    private static void OnLostPointerCapture(object? sender, PointerCaptureLostEventArgs args)
    {
        if ((sender is Slider slider) && (Dlss5SliderCommitBehavior.GetIsTracking(slider)))
        {
            Dlss5SliderCommitBehavior.Commit(slider);
        }
    }

    private static void OnKeyUp(object? sender, KeyEventArgs args)
    {
        if ((sender is Slider slider) && (Dlss5SliderCommitBehavior.IsValueChangingKey(args.Key)))
        {
            Dlss5SliderCommitBehavior.Commit(slider);
        }
    }
}
