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
    public bool UseSnapshot
    {
        get => GetValue(UseSnapshotProperty);
        set => SetValue(UseSnapshotProperty, value);
    }

    public static readonly StyledProperty<CornerRadius> BackdropCornerRadiusProperty =
        AvaloniaProperty.Register<AnimatedBlurBackdropControl, CornerRadius>(
            nameof(BackdropCornerRadius));
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<AnimatedBlurBackdropControl, double>(
            nameof(BlurRadius));
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AnimatedBlurBackdropControl, bool>(nameof(IsActive));
    public static readonly StyledProperty<bool> UseSnapshotProperty =
        AvaloniaProperty.Register<AnimatedBlurBackdropControl, bool>(nameof(UseSnapshot));

    static AnimatedBlurBackdropControl()
    {
        IsActiveProperty.Changed.AddClassHandler<AnimatedBlurBackdropControl>(
            OnIsActiveChanged);
        UseSnapshotProperty.Changed.AddClassHandler<AnimatedBlurBackdropControl>(
            OnIsActiveChanged);
    }

    public AnimatedBlurBackdropControl()
    {
        InitializeComponent();
    }

    private void UpdateVisualState()
    {
        Opacity = IsActive ? 1d : 0d;
        BackdropBlur.IsDynamic = IsActive && !UseSnapshot;
        BackdropBlur.Intensity = IsActive ? 1d : 0d;

        if (IsActive && UseSnapshot)
        {
            BackdropBlur.RefreshCapture();
        }
    }

    private static void OnIsActiveChanged(
        AnimatedBlurBackdropControl control,
        AvaloniaPropertyChangedEventArgs args)
    {
        _ = args;

        control.UpdateVisualState();
    }
}
