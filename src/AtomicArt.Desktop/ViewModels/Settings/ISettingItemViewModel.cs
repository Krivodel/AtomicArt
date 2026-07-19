namespace AtomicArt.Desktop.ViewModels.Settings;

public interface ISettingItemViewModel
{
    string ActionText { get; }
    System.Windows.Input.ICommand ActionCommand { get; }
    string DisplayName { get; }
    string? ErrorMessage { get; }
    bool HasErrorMessage { get; }
    string Key { get; }
    int Order { get; }
}
