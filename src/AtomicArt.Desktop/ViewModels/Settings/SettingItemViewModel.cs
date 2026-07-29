using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Settings;

public abstract class SettingItemViewModel : ObservableValidator, ISettingItemViewModel
{
    public string Key { get; }
    public int Order { get; }
    public string DisplayName => _textProvider.Get(_definition.DisplayNameKey);
    public SettingsSection Section { get; }
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                NotifyOperationCanExecuteChanged();
            }
        }
    }
    public string? ErrorMessage
    {
        get => _errorMessage;
        protected set
        {
            if (value is null)
            {
                _errorLocalizationKey = null;
            }

            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    protected IViewModelErrorHandler ErrorHandler => _errorHandler;
    protected ILocalizationTextProvider TextProvider => _textProvider;
    protected abstract IRelayCommand OperationCommand { get; }

    private readonly IDisplaySettingDefinition _definition;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;
    private bool _isLoading;
    private string? _errorMessage;
    private string? _errorLocalizationKey;

    protected SettingItemViewModel(
        IDisplaySettingDefinition definition,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(textProvider);

        Key = definition.Key;
        Order = definition.Order;
        Section = definition.Section;
        _definition = definition;
        _errorHandler = errorHandler;
        _textProvider = textProvider;
    }

    public virtual void RefreshLocalization()
    {
        OnPropertyChanged(nameof(DisplayName));

        if (_errorLocalizationKey is not null)
        {
            ErrorMessage = _textProvider.Get(_errorLocalizationKey);
        }
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
            value => ErrorMessage = value,
            value => _errorLocalizationKey = value);
    }

    protected virtual void NotifyOperationCanExecuteChanged()
    {
        OperationCommand.NotifyCanExecuteChanged();
    }
}
