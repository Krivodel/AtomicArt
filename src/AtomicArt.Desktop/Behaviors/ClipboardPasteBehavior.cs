using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Behaviors;

public static class ClipboardPasteBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsEnabled",
            typeof(ClipboardPasteBehavior));

    public static readonly AttachedProperty<IClipboardImageService?> ClipboardImageServiceProperty =
        AvaloniaProperty.RegisterAttached<Control, IClipboardImageService?>(
            "ClipboardImageService",
            typeof(ClipboardPasteBehavior),
            inherits: true);

    static ClipboardPasteBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(IsEnabledProperty, value);
    }

    public static IClipboardImageService? GetClipboardImageService(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(ClipboardImageServiceProperty);
    }

    public static void SetClipboardImageService(Control control, IClipboardImageService? value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(ClipboardImageServiceProperty, value);
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        bool isEnabled = args.NewValue is true;

        if (isEnabled)
        {
            control.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, true);
            return;
        }

        control.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
    }

    private static async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control control || !IsPasteGesture(e))
        {
            return;
        }

        IClipboardImageService? clipboardImageService = GetClipboardImageService(control);
        int maxInputBytes = ImageAttachmentBehavior.GetMaxInputBytes(control);

        if (clipboardImageService is null || maxInputBytes <= 0)
        {
            return;
        }

        try
        {
            ImageAttachmentInput? input = await clipboardImageService.TryGetImageAsync(
                maxInputBytes,
                CancellationToken.None);
            IReadOnlyList<ImageAttachmentInput> inputs = input is null
                ? Array.Empty<ImageAttachmentInput>()
                : new[] { input };

            if (await ImageAttachmentBehavior.TryAttachAsync(control, inputs))
            {
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            ImageAttachmentBehavior.HandleError(control, ex);
        }
    }

    private static bool IsPasteGesture(KeyEventArgs e)
    {
        return e.Key == Key.V
            && e.KeyModifiers.HasFlag(KeyModifiers.Control);
    }
}
