using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.Behaviors;

public static class Dlss5ClipboardPasteBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsEnabled",
            typeof(Dlss5ClipboardPasteBehavior));
    public static readonly AttachedProperty<IAsyncRelayCommand?> PasteCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, IAsyncRelayCommand?>(
            "PasteCommand",
            typeof(Dlss5ClipboardPasteBehavior));

    static Dlss5ClipboardPasteBehavior()
    {
        Dlss5ClipboardPasteBehavior.IsEnabledProperty.Changed.AddClassHandler<Control>(
            Dlss5ClipboardPasteBehavior.OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, Dlss5ClipboardPasteBehavior.IsEnabledProperty);
    }

    public static void SetIsEnabled(Control control, bool value)
    {
        AttachedPropertyValueAccessor.Set(control, Dlss5ClipboardPasteBehavior.IsEnabledProperty, value);
    }

    public static IAsyncRelayCommand? GetPasteCommand(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, Dlss5ClipboardPasteBehavior.PasteCommandProperty);
    }

    public static void SetPasteCommand(
        Control control,
        IAsyncRelayCommand? value)
    {
        AttachedPropertyValueAccessor.Set(control, Dlss5ClipboardPasteBehavior.PasteCommandProperty, value);
    }

    private static bool IsPasteGesture(KeyEventArgs args)
    {
        return (args.Key == Key.V) && (args.KeyModifiers.HasFlag(KeyModifiers.Control));
    }

    private static bool IsTextInputSource(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        TextBox? textBox = visual as TextBox
            ?? visual.GetVisualAncestors().OfType<TextBox>().FirstOrDefault();
        return (textBox is not null)
            && (textBox.IsEffectivelyVisible)
            && (textBox.IsEffectivelyEnabled);
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            control.AddHandler(InputElement.KeyDownEvent, Dlss5ClipboardPasteBehavior.OnKeyDown, RoutingStrategies.Tunnel, true);
            return;
        }

        control.RemoveHandler(InputElement.KeyDownEvent, Dlss5ClipboardPasteBehavior.OnKeyDown);
    }

    private static async void OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if ((sender is not Control control)
            || (!Dlss5ClipboardPasteBehavior.IsPasteGesture(eventArgs))
            || (Dlss5ClipboardPasteBehavior.IsTextInputSource(eventArgs.Source)))
        {
            return;
        }

        IAsyncRelayCommand? command = Dlss5ClipboardPasteBehavior.GetPasteCommand(control);
        if (command?.CanExecute(null) != true)
        {
            return;
        }

        eventArgs.Handled = true;

        try
        {
            await command.ExecuteAsync(null);
        }
        catch (Exception exception)
        {
            ImageAttachmentBehavior.HandleError(control, exception);
        }
    }
}
