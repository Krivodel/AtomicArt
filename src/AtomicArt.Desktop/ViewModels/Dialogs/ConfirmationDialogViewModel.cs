using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.ViewModels.Dialogs;

public sealed partial class ConfirmationDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen;
    [ObservableProperty]
    private string _title = string.Empty;
    [ObservableProperty]
    private string _message = string.Empty;
    [ObservableProperty]
    private string _confirmActionText = string.Empty;
    private TaskCompletionSource<bool>? _completion;

    internal async Task<bool> OpenAsync(
        string title,
        string message,
        string confirmActionText,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmActionText);
        ct.ThrowIfCancellationRequested();

        if (_completion is not null)
        {
            throw new InvalidOperationException(
                "A confirmation dialog is already open.");
        }

        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> cancellation = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;
        UpdateText(title, message, confirmActionText);
        IsOpen = true;

        using CancellationTokenRegistration registration =
            ct.Register(() => cancellation.TrySetResult(true));
        await Task.WhenAny(
            completion.Task,
            cancellation.Task);

        if (completion.Task.IsCompleted)
        {
            return await completion.Task;
        }

        Complete(false);
        throw new OperationCanceledException(ct);
    }

    internal void UpdateText(
        string title,
        string message,
        string confirmActionText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmActionText);

        Title = title;
        Message = message;
        ConfirmActionText = confirmActionText;
    }

    [RelayCommand]
    private void Confirm()
    {
        Complete(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        Complete(false);
    }

    private void Complete(bool result)
    {
        TaskCompletionSource<bool>? completion = _completion;

        if (completion is null)
        {
            return;
        }

        _completion = null;
        IsOpen = false;
        completion.TrySetResult(result);
    }
}
