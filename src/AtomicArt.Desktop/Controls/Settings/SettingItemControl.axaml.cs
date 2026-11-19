using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Settings;

public partial class SettingItemControl : UserControl
{
    public Control? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }
    public Control? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }
    public static readonly StyledProperty<Control?> EditorProperty =
        AvaloniaProperty.Register<SettingItemControl, Control?>(nameof(Editor));
    public static readonly StyledProperty<Control?> HeaderProperty =
        AvaloniaProperty.Register<SettingItemControl, Control?>(nameof(Header));
    public SettingItemControl()
    {
        InitializeComponent();
    }
}
