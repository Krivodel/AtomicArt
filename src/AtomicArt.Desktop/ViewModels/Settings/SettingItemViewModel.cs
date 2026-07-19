using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Settings;

public abstract class SettingItemViewModel : ObservableValidator, ISettingItemViewModel
{
    public abstract string ActionText { get; }
    public abstract System.Windows.Input.ICommand ActionCommand { get; }
    public string Key { get; }
    public int Order { get; }
    public string DisplayName { get; }
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                NotifyActionCanExecuteChanged();
            }
        }
    }
    public string? ErrorMessage
    {
        get => _errorMessage;
        protected set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    protected IViewModelErrorHandler ErrorHandler => _errorHandler;

    private readonly IViewModelErrorHandler _errorHandler;
    private bool _isLoading;
    private string? _errorMessage;

    protected SettingItemViewModel(
        IDisplaySettingDefinition definition,
        IViewModelErrorHandler errorHandler)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(errorHandler);

        Key = definition.Key;
        Order = definition.Order;
        DisplayName = definition.DisplayName;
        _errorHandler = errorHandler;
    }

    protected async Task RunOperationAsync(
        Func<Task> operation,
        CancellationToken ct,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        await ViewModelAsyncOperation.RunAsync(
            operation,
            ct,
            _errorHandler,
            operationName,
            value => IsLoading = value,
            value => ErrorMessage = value);
    }

    protected abstract void NotifyActionCanExecuteChanged();
}
