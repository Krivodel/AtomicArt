using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace AtomicArt.Desktop.Behaviors;

public static class VerticalFadeMaskBehavior
{
    public static readonly AttachedProperty<Thickness> InsetsProperty =
        AvaloniaProperty.RegisterAttached<ScrollContentPresenter, Thickness>(
            "Insets",
            typeof(VerticalFadeMaskBehavior));

    private static readonly AttachedProperty<LinearGradientBrush?> MaskProperty =
        AvaloniaProperty.RegisterAttached<ScrollContentPresenter, LinearGradientBrush?>(
            "Mask",
            typeof(VerticalFadeMaskBehavior));

    static VerticalFadeMaskBehavior()
    {
        InsetsProperty.Changed.AddClassHandler<ScrollContentPresenter>(OnSettingChanged);
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

    internal static (double TopFadeEnd, double BottomFadeStart) CalculateFadeOffsets(
        double height,
        Thickness insets)
    {
        if (height <= 0d)
        {
            return (0d, 1d);
        }

        double topFadeHeight = Math.Max(0d, insets.Top);
        double bottomFadeHeight = Math.Max(0d, insets.Bottom);
        double totalFadeHeight = topFadeHeight + bottomFadeHeight;
        double scale = totalFadeHeight > height
            ? height / totalFadeHeight
            : 1d;

        return (
            topFadeHeight * scale / height,
            1d - (bottomFadeHeight * scale / height));
    }

    private static void Attach(ScrollContentPresenter scrollPresenter)
    {
        if (scrollPresenter.GetValue(MaskProperty) is not null)
        {
            return;
        }

        LinearGradientBrush mask = CreateMask();

        scrollPresenter.SetValue(MaskProperty, mask);
        scrollPresenter.OpacityMask = mask;
        scrollPresenter.PropertyChanged += OnScrollPresenterPropertyChanged;
    }

    private static LinearGradientBrush CreateMask()
    {
        LinearGradientBrush mask = new()
        {
            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0d, 1d, RelativeUnit.Relative)
        };

        mask.GradientStops.Add(new GradientStop(Colors.White, 0d));
        mask.GradientStops.Add(new GradientStop(Colors.White, 0d));
        mask.GradientStops.Add(new GradientStop(Colors.White, 1d));
        mask.GradientStops.Add(new GradientStop(Colors.White, 1d));

        return mask;
    }

    private static void OnScrollPresenterPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs args)
    {
        if (sender is ScrollContentPresenter scrollPresenter
            && args.Property == Visual.BoundsProperty)
        {
            UpdateMask(scrollPresenter);
        }
    }

    private static void OnSettingChanged(
        ScrollContentPresenter scrollPresenter,
        AvaloniaPropertyChangedEventArgs _)
    {
        Attach(scrollPresenter);
        UpdateMask(scrollPresenter);
    }

    private static void UpdateMask(ScrollContentPresenter scrollPresenter)
    {
        LinearGradientBrush? mask = scrollPresenter.GetValue(MaskProperty);

        if (mask is null)
        {
            return;
        }

        Thickness insets = GetInsets(scrollPresenter);
        (double topFadeEnd, double bottomFadeStart) =
            CalculateFadeOffsets(scrollPresenter.Bounds.Height, insets);

        mask.GradientStops[0].Color = insets.Top > 0d
            ? Colors.Transparent
            : Colors.White;
        mask.GradientStops[1].Offset = topFadeEnd;
        mask.GradientStops[2].Offset = bottomFadeStart;
        mask.GradientStops[3].Color = insets.Bottom > 0d
            ? Colors.Transparent
            : Colors.White;
    }
}
