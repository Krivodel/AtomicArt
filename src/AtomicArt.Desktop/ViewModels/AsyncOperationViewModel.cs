using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels;

public abstract partial class AsyncOperationViewModel : ObservableObject
{
    public bool IsLoading => _operationCount > 0;
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    protected IDialogService Dialogs { get; }
    protected IViewModelErrorHandler ErrorHandler { get; }

    private int _operationCount;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    protected AsyncOperationViewModel(IViewModelErrorHandler errorHandler, IDialogService dialogs)
    {
        ErrorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        Dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    protected async Task ExecuteAsync(
        Func<CancellationToken, Task> action, string operationName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        _operationCount++;
        OnPropertyChanged(nameof(IsLoading));
        ErrorMessage = null;
        string? operationError = null;

        try
        {
            await ViewModelAsyncOperation.ExecuteAsync(
                ErrorHandler,
                value =>
                {
                    operationError = value;
                    ErrorMessage = value;
                },
                _ => { },
                action,
                operationName,
                ct);

            if (!string.IsNullOrWhiteSpace(operationError))
            {
                await ViewModelAsyncOperation.ExecuteAsync(
                    ErrorHandler,
                    value => ErrorMessage = value,
                    _ => { },
                    token => Dialogs.ShowLocalizedErrorAsync(CommonLocalizationKeys.UnknownError, token),
                    operationName,
                    ct);
            }
        }
        finally
        {
            _operationCount--;
            OnPropertyChanged(nameof(IsLoading));
        }
    }
}
