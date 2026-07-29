using AtomicArt.Desktop.ViewModels.Dialogs;

namespace AtomicArt.Desktop.Services;

public sealed class DialogService : IDialogService
{
    private readonly ErrorDialogViewModel _dialog;

    public DialogService(ErrorDialogViewModel dialog)
    {
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
    }

    public void ShowError(string message)
    {
        _dialog.Open(message);
    }

    public Task ShowErrorAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ShowError(message);

        return Task.CompletedTask;
    }
}
