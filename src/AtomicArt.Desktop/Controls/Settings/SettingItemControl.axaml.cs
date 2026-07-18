using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Settings;

public partial class SettingItemControl : UserControl
{
    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }
    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }
    public string? DisplayName
    {
        get => GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }
    public Control? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }
    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }
    public bool HasErrorMessage
    {
        get => GetValue(HasErrorMessageProperty);
        set => SetValue(HasErrorMessageProperty, value);
    }

    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<SettingItemControl, string?>(nameof(ActionText));
    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<SettingItemControl, ICommand?>(nameof(ActionCommand));
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<SettingItemControl, string?>(nameof(DisplayName));
    public static readonly StyledProperty<Control?> EditorProperty =
        AvaloniaProperty.Register<SettingItemControl, Control?>(nameof(Editor));
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<SettingItemControl, string?>(nameof(ErrorMessage));
    public static readonly StyledProperty<bool> HasErrorMessageProperty =
        AvaloniaProperty.Register<SettingItemControl, bool>(nameof(HasErrorMessage));

    public SettingItemControl()
    {
        InitializeComponent();
    }
}
