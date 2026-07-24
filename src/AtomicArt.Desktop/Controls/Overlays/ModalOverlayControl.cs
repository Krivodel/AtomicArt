using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AtomicArt.Desktop.Controls.Overlays;

public sealed class ModalOverlayControl : TemplatedControl
{
    public double BlurIntensity
    {
        get => GetValue(BlurIntensityProperty);
        set => SetValue(BlurIntensityProperty, value);
    }
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

    public static readonly StyledProperty<double> BlurIntensityProperty =
        ModalOverlayPresenterControl.BlurIntensityProperty.AddOwner<ModalOverlayControl>();
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

    internal BlurBackdropControl? BlurBackdrop => _blurBackdrop;

    private BlurBackdropControl? _blurBackdrop;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _blurBackdrop = e.NameScope.Find<BlurBackdropControl>("PART_BlurBackdrop");
    }
}
