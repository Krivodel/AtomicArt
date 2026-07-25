using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Controls.Settings;

public partial class SettingItemControl : UserControl
{
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
        Bind(DisplayNameProperty, new Binding(nameof(ISettingItemViewModel.DisplayName)));
        Bind(ErrorMessageProperty, new Binding(nameof(ISettingItemViewModel.ErrorMessage)));
        Bind(HasErrorMessageProperty, new Binding(nameof(ISettingItemViewModel.HasErrorMessage)));

        InitializeComponent();
    }
}
