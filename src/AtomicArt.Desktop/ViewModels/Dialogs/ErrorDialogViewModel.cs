using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Dialogs;

public sealed partial class ErrorDialogViewModel : ObservableObject
{
    private readonly ITextClipboardService _textClipboardService;
    private readonly IViewModelErrorHandler _errorHandler;

    [ObservableProperty]
    private bool _isOpen;
    [ObservableProperty]
    private string _message = string.Empty;

    public ErrorDialogViewModel(
        ITextClipboardService textClipboardService,
        IViewModelErrorHandler errorHandler)
    {
        _textClipboardService = textClipboardService
            ?? throw new ArgumentNullException(nameof(textClipboardService));
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    internal void Open(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Message = message;
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
    }

    [RelayCommand]
    private async Task CopyAsync(CancellationToken ct)
    {
        try
        {
            await _textClipboardService.SetTextAsync(Message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(CopyAsync));
        }
    }
}
