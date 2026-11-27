using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.ViewModels.Notifications;

public sealed class GenerationNotificationViewModel
{
    public string Prompt { get; }
    public string Status { get; }
    public bool IsError { get; }
    public IRelayCommand CloseCommand { get; }

    public GenerationNotificationViewModel(string prompt, string status, bool isError, Action close)
    {
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        Status = status ?? throw new ArgumentNullException(nameof(status));
        IsError = isError;
        CloseCommand = new RelayCommand(close);
    }
}
