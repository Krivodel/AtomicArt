using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AtomicArt.Desktop.Controls.Overlays;

public sealed class BlurBackdropControl : Control
{
    public double BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }
    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }
    public bool IsDynamic
    {
        get => GetValue(IsDynamicProperty);
        set => SetValue(IsDynamicProperty, value);
    }

    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<BlurBackdropControl, double>(nameof(BlurRadius));
    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<BlurBackdropControl, double>(
            nameof(Intensity),
            defaultValue: 1d);
    public static readonly StyledProperty<bool> IsDynamicProperty =
        AvaloniaProperty.Register<BlurBackdropControl, bool>(nameof(IsDynamic));

    private const double BlurRadiusToSigmaDivisor = 3d;

    private int _animationRevision;
    private int _captureRevision;
    private bool _isFramePending;

    static BlurBackdropControl()
    {
        AffectsRender<BlurBackdropControl>(
            BlurRadiusProperty,
            IsDynamicProperty,
            IntensityProperty);
    }

    public BlurBackdropControl()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double normalizedRadius = NormalizeBlurRadius(BlurRadius)
            * NormalizeIntensity(Intensity);
        if ((normalizedRadius <= 0d)
            || (Bounds.Width <= 0d)
            || (Bounds.Height <= 0d))
        {
            return;
        }

        Rect bounds = new(Bounds.Size);
        float sigma = (float)(normalizedRadius / BlurRadiusToSigmaDivisor);
        context.Custom(
            new BackdropBlurDrawOperation(
                bounds,
                sigma,
                IsDynamic,
                _captureRevision));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        RefreshCapture();
        RestartDynamicFrames();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        StopDynamicFrames();

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsDynamicProperty)
        {
            RefreshCapture();
            RestartDynamicFrames();
        }
    }

    private static double NormalizeBlurRadius(double blurRadius)
    {
        return double.IsFinite(blurRadius)
            ? Math.Max(0d, blurRadius)
            : 0d;
    }

    private static double NormalizeIntensity(double intensity)
    {
        return double.IsFinite(intensity)
            ? Math.Clamp(intensity, 0d, 1d)
            : 0d;
    }

    private void RefreshCapture()
    {
        _captureRevision++;
        InvalidateVisual();
    }

    private void RestartDynamicFrames()
    {
        StopDynamicFrames();
        RequestNextDynamicFrame();
    }

    private void StopDynamicFrames()
    {
        _animationRevision++;
        _isFramePending = false;
    }

    private void RequestNextDynamicFrame()
    {
        if (!IsDynamic || _isFramePending)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        int revision = _animationRevision;
        _isFramePending = true;
        topLevel.RequestAnimationFrame(_ => OnDynamicFrame(revision));
    }

    private void OnDynamicFrame(int revision)
    {
        if (revision != _animationRevision)
        {
            return;
        }

        _isFramePending = false;
        if (!IsDynamic || (VisualRoot is null))
        {
            return;
        }

        RefreshCapture();
        RequestNextDynamicFrame();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        RefreshCapture();
    }
}
