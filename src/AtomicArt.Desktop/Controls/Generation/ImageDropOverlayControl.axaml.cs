using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Transformation;

namespace AtomicArt.Desktop.Controls.Generation;

public partial class ImageDropOverlayControl : UserControl
{
    public CornerRadius BackdropCornerRadius
    {
        get => GetValue(BackdropCornerRadiusProperty);
        set => SetValue(BackdropCornerRadiusProperty, value);
    }
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }
    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly StyledProperty<CornerRadius> BackdropCornerRadiusProperty =
        AvaloniaProperty.Register<ImageDropOverlayControl, CornerRadius>(
            nameof(BackdropCornerRadius));
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ImageDropOverlayControl, bool>(nameof(IsActive));
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<ImageDropOverlayControl, double>(
            nameof(IconSize),
            defaultValue: 112d);

    private static readonly TransformOperations HiddenTransform =
        TransformOperations.Parse("scale(0.97) translate(0px, 8px)");
    private static readonly TransformOperations VisibleTransform =
        TransformOperations.Parse("scale(1) translate(0px, 0px)");

    static ImageDropOverlayControl()
    {
        IsActiveProperty.Changed.AddClassHandler<ImageDropOverlayControl>(OnIsActiveChanged);
    }

    public ImageDropOverlayControl()
    {
        InitializeComponent();
    }

    private void UpdateVisualState()
    {
        BackdropBlur.Intensity = IsActive ? 1d : 0d;
        OverlayChrome.Opacity = IsActive ? 1d : 0d;
        AnimatedContent.Opacity = IsActive ? 1d : 0d;
        AnimatedContent.RenderTransform = IsActive ? VisibleTransform : HiddenTransform;
    }

    private static void OnIsActiveChanged(
        ImageDropOverlayControl control,
        AvaloniaPropertyChangedEventArgs args)
    {
        _ = args;

        control.UpdateVisualState();
    }
}
