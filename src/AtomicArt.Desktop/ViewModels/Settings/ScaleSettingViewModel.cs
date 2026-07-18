using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class ScaleSettingViewModel : ObservableObject, ISettingItemViewModel
{
    public string Key { get; }
    public int Order { get; }
    public string DisplayName { get; }
    public string ApplyButtonText { get; }
    public IReadOnlyList<UiScaleOption> Options { get; }
    private readonly IScaleSettingDefinition _definition;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IUiScaleSettingValueConverter _valueConverter;
    private readonly IViewModelErrorHandler _errorHandler;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private UiScaleOption? _selectedOption;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ScaleSettingViewModel(
        IScaleSettingDefinition definition,
        IReadOnlyList<UiScaleOption> options,
        UiScaleOption? selectedOption,
        ISettingsStateService settingsStateService,
        IUiScaleSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settingsStateService);
        ArgumentNullException.ThrowIfNull(valueConverter);
        ArgumentNullException.ThrowIfNull(errorHandler);

        _definition = definition;
        Key = definition.Key;
        Order = definition.Order;
        DisplayName = definition.DisplayName;
        ApplyButtonText = definition.ApplyButtonText;
        Options = options;
        SelectedOption = selectedOption;
        _settingsStateService = settingsStateService;
        _valueConverter = valueConverter;
        _errorHandler = errorHandler;
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync(CancellationToken ct)
    {
        if (SelectedOption is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            string value = _valueConverter.Format(SelectedOption.Value);
            _settingsStateService.ApplyValue(_definition, value);
            await _settingsStateService.SaveValueAsync(_definition, value, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(ApplyAsync));
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanApply()
    {
        return SelectedOption is not null && !IsLoading;
    }
}
