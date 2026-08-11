using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AtomicArt.Desktop.Views.Gallery;

public partial class GallerySelectionOverlayView : UserControl
{
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<GallerySelectionOverlayView, bool>(nameof(IsActive));

    private const double FallbackHiddenOffset = 48d;

    private readonly TranslateTransform _selectionPanelTranslation;

    static GallerySelectionOverlayView()
    {
        IsActiveProperty.Changed.AddClassHandler<GallerySelectionOverlayView>(
            OnIsActiveChanged);
    }

    public GallerySelectionOverlayView()
    {
        InitializeComponent();
        _selectionPanelTranslation = SelectionPanel.RenderTransform as TranslateTransform
            ?? throw new InvalidOperationException(
                "The gallery selection panel translation was not created.");
        UpdateVisualState();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property == BoundsProperty) && !IsActive)
        {
            _selectionPanelTranslation.Y = -GetHiddenOffset();
        }
    }

    private static void OnIsActiveChanged(
        GallerySelectionOverlayView control,
        AvaloniaPropertyChangedEventArgs args)
    {
        _ = args;

        control.UpdateVisualState();
    }

    private double GetHiddenOffset()
    {
        return Math.Max(Bounds.Height, FallbackHiddenOffset);
    }

    private void UpdateVisualState()
    {
        IsHitTestVisible = IsActive;
        AnimatedBackdrop.IsActive = IsActive;
        SelectionPanel.Opacity = IsActive ? 1d : 0d;
        _selectionPanelTranslation.Y = IsActive ? 0d : -GetHiddenOffset();
    }
}
