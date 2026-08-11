using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Overlays;

public partial class AnimatedBlurBackdropControl : UserControl
{
    public CornerRadius BackdropCornerRadius
    {
        get => GetValue(BackdropCornerRadiusProperty);
        set => SetValue(BackdropCornerRadiusProperty, value);
    }
    public double BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly StyledProperty<CornerRadius> BackdropCornerRadiusProperty =
        AvaloniaProperty.Register<AnimatedBlurBackdropControl, CornerRadius>(
            nameof(BackdropCornerRadius));
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<AnimatedBlurBackdropControl, double>(
            nameof(BlurRadius));
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AnimatedBlurBackdropControl, bool>(nameof(IsActive));

    static AnimatedBlurBackdropControl()
    {
        IsActiveProperty.Changed.AddClassHandler<AnimatedBlurBackdropControl>(
            OnIsActiveChanged);
    }

    public AnimatedBlurBackdropControl()
    {
        InitializeComponent();
    }

    private static void OnIsActiveChanged(
        AnimatedBlurBackdropControl control,
        AvaloniaPropertyChangedEventArgs args)
    {
        _ = args;

        control.UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        Opacity = IsActive ? 1d : 0d;
        BackdropBlur.Intensity = IsActive ? 1d : 0d;
    }
}
