using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Overlays;

public partial class ModalOverlayControl : UserControl
{
    public Control? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }
    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<Control?> BodyProperty =
        AvaloniaProperty.Register<ModalOverlayControl, Control?>(nameof(Body));
    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<ModalOverlayControl, ICommand?>(nameof(CloseCommand));
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ModalOverlayControl, string?>(nameof(Title));

    public ModalOverlayControl()
    {
        InitializeComponent();
    }
}
