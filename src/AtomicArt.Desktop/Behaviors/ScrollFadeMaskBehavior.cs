using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;

namespace AtomicArt.Desktop.Behaviors;

public static class ScrollFadeMaskBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollContentPresenter, bool>(
            "IsEnabled", typeof(ScrollFadeMaskBehavior), defaultValue: true);
    public static readonly AttachedProperty<Thickness> InsetsProperty =
        AvaloniaProperty.RegisterAttached<ScrollContentPresenter, Thickness>(
            "Insets",
            typeof(ScrollFadeMaskBehavior));
    public static readonly AttachedProperty<Orientation> OrientationProperty =
        AvaloniaProperty.RegisterAttached<ScrollContentPresenter, Orientation>(
            "Orientation",
            typeof(ScrollFadeMaskBehavior),
            Orientation.Vertical);
    public static readonly AttachedProperty<bool> FadeWhenClippedProperty =
        AvaloniaProperty.RegisterAttached<ScrollContentPresenter, bool>(
            "FadeWhenClipped",
            typeof(ScrollFadeMaskBehavior));

    private static readonly AttachedProperty<LinearGradientBrush?> MaskProperty =
        AvaloniaProperty.RegisterAttached<ScrollContentPresenter, LinearGradientBrush?>(
            "Mask",
            typeof(ScrollFadeMaskBehavior));

    static ScrollFadeMaskBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollContentPresenter>(OnSettingChanged);
        InsetsProperty.Changed.AddClassHandler<ScrollContentPresenter>(OnSettingChanged);
        OrientationProperty.Changed.AddClassHandler<ScrollContentPresenter>(OnSettingChanged);
        FadeWhenClippedProperty.Changed.AddClassHandler<ScrollContentPresenter>(OnSettingChanged);
    }

    public static bool GetIsEnabled(ScrollContentPresenter scrollPresenter)
    {
        return AttachedPropertyValueAccessor.Get(scrollPresenter, IsEnabledProperty);
    }

    public static void SetIsEnabled(ScrollContentPresenter scrollPresenter, bool value)
    {
        AttachedPropertyValueAccessor.Set(scrollPresenter, IsEnabledProperty, value);
    }

    public static Thickness GetInsets(ScrollContentPresenter scrollPresenter)
    {
        return AttachedPropertyValueAccessor.Get(scrollPresenter, InsetsProperty);
    }

    public static void SetInsets(
        ScrollContentPresenter scrollPresenter,
        Thickness value)
    {
        AttachedPropertyValueAccessor.Set(scrollPresenter, InsetsProperty, value);
    }

    public static Orientation GetOrientation(ScrollContentPresenter scrollPresenter)
    {
        return AttachedPropertyValueAccessor.Get(scrollPresenter, OrientationProperty);
    }

    public static void SetOrientation(ScrollContentPresenter scrollPresenter, Orientation value)
    {
        AttachedPropertyValueAccessor.Set(scrollPresenter, OrientationProperty, value);
    }

    public static bool GetFadeWhenClipped(ScrollContentPresenter scrollPresenter)
    {
        return AttachedPropertyValueAccessor.Get(scrollPresenter, FadeWhenClippedProperty);
    }

    public static void SetFadeWhenClipped(ScrollContentPresenter scrollPresenter, bool value)
    {
        AttachedPropertyValueAccessor.Set(scrollPresenter, FadeWhenClippedProperty, value);
    }

    internal static (double LeadingFadeEnd, double TrailingFadeStart) CalculateFadeOffsets(
        double length,
        double leadingInset,
        double trailingInset)
    {
        if (length <= 0d)
        {
            return (0d, 1d);
        }

        double leadingFade = Math.Max(0d, leadingInset);
        double trailingFade = Math.Max(0d, trailingInset);
        double totalFade = leadingFade + trailingFade;
        double scale = totalFade > length
            ? length / totalFade
            : 1d;

        return (
            leadingFade * scale / length,
            1d - (trailingFade * scale / length));
    }

    private static void Attach(ScrollContentPresenter scrollPresenter)
    {
        if (scrollPresenter.GetValue(MaskProperty) is not null)
        {
            return;
        }

        LinearGradientBrush mask = CreateMask();

        scrollPresenter.SetValue(MaskProperty, mask);
        scrollPresenter.PropertyChanged += OnScrollPresenterPropertyChanged;
    }

    private static LinearGradientBrush CreateMask()
    {
        LinearGradientBrush mask = new()
        {
            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative)
        };

        mask.GradientStops.Add(new GradientStop(Colors.White, 0d));
        mask.GradientStops.Add(new GradientStop(Colors.White, 0d));
        mask.GradientStops.Add(new GradientStop(Colors.White, 1d));
        mask.GradientStops.Add(new GradientStop(Colors.White, 1d));

        return mask;
    }

    private static void UpdateMask(ScrollContentPresenter scrollPresenter)
    {
        LinearGradientBrush? mask = scrollPresenter.GetValue(MaskProperty);

        if (mask is null)
        {
            return;
        }

        Thickness insets = ScrollFadeMaskBehavior.GetInsets(scrollPresenter);
        bool horizontal = ScrollFadeMaskBehavior.GetOrientation(scrollPresenter) == Orientation.Horizontal;
        double length = horizontal ? scrollPresenter.Bounds.Width : scrollPresenter.Bounds.Height;
        double leadingInset = horizontal ? insets.Left : insets.Top;
        double trailingInset = horizontal ? insets.Right : insets.Bottom;

        if (ScrollFadeMaskBehavior.GetFadeWhenClipped(scrollPresenter))
        {
            double offset = horizontal ? scrollPresenter.Offset.X : scrollPresenter.Offset.Y;
            double extent = horizontal ? scrollPresenter.Extent.Width : scrollPresenter.Extent.Height;
            double viewport = horizontal ? scrollPresenter.Viewport.Width : scrollPresenter.Viewport.Height;
            leadingInset = Math.Min(leadingInset, Math.Max(0d, offset));
            trailingInset = Math.Min(trailingInset, Math.Max(0d, extent - viewport - offset));
        }

        (double leadingFadeEnd, double trailingFadeStart) =
            ScrollFadeMaskBehavior.CalculateFadeOffsets(length, leadingInset, trailingInset);

        mask.EndPoint = horizontal
            ? new RelativePoint(1d, 0d, RelativeUnit.Relative)
            : new RelativePoint(0d, 1d, RelativeUnit.Relative);
        mask.GradientStops[0].Color = leadingInset > 0d
            ? Colors.Transparent
            : Colors.White;
        mask.GradientStops[1].Offset = leadingFadeEnd;
        mask.GradientStops[2].Offset = trailingFadeStart;
        mask.GradientStops[3].Color = trailingInset > 0d
            ? Colors.Transparent
            : Colors.White;
        scrollPresenter.OpacityMask = leadingInset > 0d || trailingInset > 0d ? mask : null;
    }

    private static void OnScrollPresenterPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs args)
    {
        if (sender is ScrollContentPresenter scrollPresenter
            && (args.Property == Visual.BoundsProperty
                || (GetFadeWhenClipped(scrollPresenter)
                    && (args.Property == ScrollContentPresenter.OffsetProperty
                        || args.Property == ScrollContentPresenter.ExtentProperty
                        || args.Property == ScrollContentPresenter.ViewportProperty))))
        {
            UpdateMask(scrollPresenter);
        }
    }

    private static void OnSettingChanged(
        ScrollContentPresenter scrollPresenter,
        AvaloniaPropertyChangedEventArgs _)
    {
        if (!ScrollFadeMaskBehavior.GetIsEnabled(scrollPresenter))
        {
            LinearGradientBrush? mask = scrollPresenter.GetValue(MaskProperty);

            if (object.ReferenceEquals(scrollPresenter.OpacityMask, mask))
            {
                scrollPresenter.OpacityMask = null;
            }

            scrollPresenter.ClearValue(MaskProperty);
            scrollPresenter.PropertyChanged -= OnScrollPresenterPropertyChanged;
            return;
        }

        Attach(scrollPresenter);
        UpdateMask(scrollPresenter);
    }
}
