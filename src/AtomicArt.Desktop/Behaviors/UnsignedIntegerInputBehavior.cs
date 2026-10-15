using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Logging;

namespace AtomicArt.Desktop.Behaviors;

public static class UnsignedIntegerInputBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<NumericUpDown, bool>(
            "IsEnabled", typeof(UnsignedIntegerInputBehavior));

    static UnsignedIntegerInputBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<NumericUpDown>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(NumericUpDown control)
    {
        return AttachedPropertyValueAccessor.Get(control, IsEnabledProperty);
    }

    public static void SetIsEnabled(NumericUpDown control, bool value)
    {
        AttachedPropertyValueAccessor.Set(control, IsEnabledProperty, value);
    }

    private static bool ContainsOnlyDigits(string text)
    {
        return text.All(char.IsAsciiDigit);
    }

    private static void OnIsEnabledChanged(NumericUpDown control, AvaloniaPropertyChangedEventArgs args)
    {
        control.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
        control.RemoveHandler(TextBox.PastingFromClipboardEvent, OnPastingFromClipboard);

        if (args.NewValue is true)
        {
            control.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
            control.AddHandler(TextBox.PastingFromClipboardEvent, OnPastingFromClipboard);
        }
    }

    private static void OnTextInput(object? sender, TextInputEventArgs args)
    {
        if (args.Text is string text && !ContainsOnlyDigits(text))
        {
            args.Handled = true;
        }
    }

    private static async void OnPastingFromClipboard(object? sender, RoutedEventArgs args)
    {
        if (sender is not NumericUpDown control || args.Source is not TextBox textBox)
        {
            return;
        }

        args.Handled = true;
        TopLevel? topLevel = TopLevel.GetTopLevel(textBox);
        IClipboard? clipboard = topLevel?.Clipboard;

        if (clipboard is null || textBox.IsReadOnly || !textBox.IsEffectivelyEnabled)
        {
            return;
        }

        string? originalText = textBox.Text;
        int selectionStart = textBox.SelectionStart;
        int selectionEnd = textBox.SelectionEnd;

        try
        {
            string? text = await clipboard.TryGetTextAsync();

            if (string.IsNullOrEmpty(text)
                || !ContainsOnlyDigits(text)
                || !GetIsEnabled(control)
                || !textBox.IsEffectivelyEnabled
                || textBox.IsReadOnly
                || TopLevel.GetTopLevel(textBox) != topLevel
                || !string.Equals(textBox.Text, originalText, StringComparison.Ordinal)
                || textBox.SelectionStart != selectionStart
                || textBox.SelectionEnd != selectionEnd)
            {
                return;
            }

            textBox.SelectedText = text;
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(
                control, "Failed to read integer input from the clipboard: {Exception}", exception);
        }
    }
}
