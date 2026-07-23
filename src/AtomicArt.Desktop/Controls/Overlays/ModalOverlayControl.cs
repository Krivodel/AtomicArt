using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.VisualTree;

using SukiUI.Controls.GlassMorphism;

namespace AtomicArt.Desktop.Controls.Overlays;

public sealed class ModalOverlayControl : TemplatedControl
{
    public double BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }
    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }
    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public bool IsBlurDynamic
    {
        get => GetValue(IsBlurDynamicProperty);
        set => SetValue(IsBlurDynamicProperty, value);
    }
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<ModalOverlayControl, double>(nameof(BlurRadius));
    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<ModalOverlayControl, object?>(nameof(Body));
    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<ModalOverlayControl, ICommand?>(nameof(CloseCommand));
    public static readonly StyledProperty<bool> IsBlurDynamicProperty =
        AvaloniaProperty.Register<ModalOverlayControl, bool>(nameof(IsBlurDynamic));
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ModalOverlayControl, string?>(nameof(Title));

    private const double SukiBlurSizeDivisor = 42d;
    private const double SukiLightThemeBaseBlurRadius = 50d;
    private const double SukiMinimumBaseBlurRadius = 20d;

    private BlurBackground? _blurBackground;

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size arrangedSize = base.ArrangeOverride(finalSize);

        UpdateBlurIntensityFactor();

        return arrangedSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        UpdateBlurIntensityFactor();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _blurBackground = e.NameScope.Find<BlurBackground>("PART_BlurBackground");
        UpdateBlurIntensityFactor();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BlurRadiusProperty)
        {
            UpdateBlurIntensityFactor();
        }

    }

    private static double CalculateBaseBlurRadius(Size blurSize, ThemeVariant themeVariant)
    {
        double baseBlurRadius = themeVariant == ThemeVariant.Dark
            ? (blurSize.Width + blurSize.Height) / SukiBlurSizeDivisor
            : SukiLightThemeBaseBlurRadius;

        return Math.Max(SukiMinimumBaseBlurRadius, baseBlurRadius);
    }

    private void UpdateBlurIntensityFactor()
    {
        BlurBackground? blurBackground = _blurBackground;
        if ((blurBackground is null)
            || (blurBackground.Bounds.Width <= 0d)
            || (blurBackground.Bounds.Height <= 0d))
        {
            return;
        }

        double baseBlurRadius = CalculateBaseBlurRadius(
            blurBackground.Bounds.Size,
            ActualThemeVariant);
        double desiredBlurRadius = double.IsFinite(BlurRadius)
            ? Math.Max(0d, BlurRadius)
            : 0d;

        double intensityFactor = desiredBlurRadius / baseBlurRadius;
        if (blurBackground.IntensityFactor == intensityFactor)
        {
            return;
        }

        blurBackground.IntensityFactor = intensityFactor;
        blurBackground.InvalidateVisual();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        UpdateBlurIntensityFactor();
    }
}
