using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AtomicArt.Desktop.Behaviors;

public static class TextBoxFocusBehavior
{
    public static readonly AttachedProperty<bool> AutoFocusProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "AutoFocus",
            typeof(TextBoxFocusBehavior));
    public static readonly AttachedProperty<bool> FocusOnClearProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "FocusOnClear",
            typeof(TextBoxFocusBehavior));
    public static readonly AttachedProperty<bool> CaretAtEndProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("CaretAtEnd", typeof(TextBoxFocusBehavior));

    static TextBoxFocusBehavior()
    {
        AutoFocusProperty.Changed.AddClassHandler<TextBox>(
            OnAutoFocusChanged);
        FocusOnClearProperty.Changed.AddClassHandler<TextBox>(
            OnFocusOnClearChanged);
    }

    public static bool GetAutoFocus(TextBox textBox)
    {
        return AttachedPropertyValueAccessor.Get(
            textBox,
            AutoFocusProperty);
    }

    public static bool GetCaretAtEnd(TextBox textBox)
    {
        return AttachedPropertyValueAccessor.Get(textBox, CaretAtEndProperty);
    }

    public static void SetCaretAtEnd(TextBox textBox, bool value)
    {
        AttachedPropertyValueAccessor.Set(textBox, CaretAtEndProperty, value);
    }

    public static void SetAutoFocus(TextBox textBox, bool value)
    {
        AttachedPropertyValueAccessor.Set(
            textBox,
            AutoFocusProperty,
            value);
    }

    public static bool GetFocusOnClear(TextBox textBox)
    {
        return AttachedPropertyValueAccessor.Get(
            textBox,
            FocusOnClearProperty);
    }

    public static void SetFocusOnClear(
        TextBox textBox,
        bool value)
    {
        AttachedPropertyValueAccessor.Set(
            textBox,
            FocusOnClearProperty,
            value);
    }

    internal static void RequestFocus(TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);

        PostFocus(textBox);
    }

    private static void PostFocus(TextBox textBox)
    {
        textBox.Dispatcher.Post(
            () => FocusIfAvailable(textBox),
            DispatcherPriority.Input);
    }

    private static void FocusIfAvailable(TextBox textBox)
    {
        if ((GetAutoFocus(textBox) || GetFocusOnClear(textBox))
            && textBox.IsEffectivelyVisible
            && textBox.IsEnabled)
        {
            textBox.Focus();

            if (TextBoxFocusBehavior.GetCaretAtEnd(textBox))
            {
                int end = textBox.Text?.Length ?? 0;
                textBox.SelectionStart = end;
                textBox.SelectionEnd = end;
                textBox.CaretIndex = end;
            }
        }
    }

    private static void OnAutoFocusChanged(
        TextBox textBox,
        AvaloniaPropertyChangedEventArgs args)
    {
        textBox.Loaded -= OnAutoFocusTargetLoaded;

        if (args.NewValue is true)
        {
            textBox.Loaded += OnAutoFocusTargetLoaded;

            if (textBox.IsLoaded)
            {
                PostFocus(textBox);
            }
        }
    }

    private static void OnFocusOnClearChanged(
        TextBox textBox,
        AvaloniaPropertyChangedEventArgs args)
    {
        textBox.TextChanged -= OnTextChanged;

        if (args.NewValue is true)
        {
            textBox.TextChanged += OnTextChanged;
        }
    }

    private static void OnTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        _ = e;

        if (sender is not TextBox textBox
            || !string.IsNullOrEmpty(textBox.Text)
            || !textBox.IsEffectivelyVisible)
        {
            return;
        }

        PostFocus(textBox);
    }

    private static void OnAutoFocusTargetLoaded(
        object? sender,
        RoutedEventArgs e)
    {
        _ = e;

        if (sender is TextBox textBox)
        {
            PostFocus(textBox);
        }
    }
}
