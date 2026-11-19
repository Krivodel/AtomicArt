using Avalonia;
using Avalonia.Media;

namespace AtomicArt.Desktop.Behaviors;

public static class FractionalTextRenderingBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Visual, bool>("IsEnabled", typeof(FractionalTextRenderingBehavior));

    private static readonly AttachedProperty<TextOptions?> PreviousOptionsProperty =
        AvaloniaProperty.RegisterAttached<Visual, TextOptions?>("PreviousOptions", typeof(FractionalTextRenderingBehavior));

    static FractionalTextRenderingBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Visual>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Visual visual)
    {
        return AttachedPropertyValueAccessor.Get(visual, IsEnabledProperty);
    }

    public static void SetIsEnabled(Visual visual, bool value)
    {
        AttachedPropertyValueAccessor.Set(visual, IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(Visual visual, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            TextOptions previousOptions = TextOptions.GetTextOptions(visual);
            visual.SetValue(PreviousOptionsProperty, previousOptions);
            TextOptions.SetTextOptions(visual, previousOptions with
            {
                TextHintingMode = TextHintingMode.None,
                BaselinePixelAlignment = BaselinePixelAlignment.Unaligned
            });
            return;
        }

        if (visual.GetValue(PreviousOptionsProperty) is { } options)
        {
            visual.ClearValue(PreviousOptionsProperty);
            TextOptions.SetTextOptions(visual, options);
        }
    }
}
