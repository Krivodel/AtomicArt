using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class GpuResourceCacheSettingViewModel : ObservableObject, ISettingItemViewModel
{
    public string Key { get; }
    public int Order { get; }
    public string DisplayName { get; }
    public string SaveButtonText { get; }
    public string RestartNotice { get; }
    public IReadOnlyList<GpuResourceCacheOption> Options { get; }

    private readonly GpuResourceCacheSettingDefinition _definition;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IViewModelErrorHandler _errorHandler;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private GpuResourceCacheOption? _selectedOption;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public GpuResourceCacheSettingViewModel(
        GpuResourceCacheSettingDefinition definition,
        IReadOnlyList<GpuResourceCacheOption> options,
        GpuResourceCacheOption selectedOption,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(selectedOption);
        ArgumentNullException.ThrowIfNull(settingsStateService);
        ArgumentNullException.ThrowIfNull(errorHandler);

        _definition = definition;
        _settingsStateService = settingsStateService;
        _errorHandler = errorHandler;
        Key = definition.Key;
        Order = definition.Order;
        DisplayName = definition.DisplayName;
        SaveButtonText = definition.SaveButtonText;
        RestartNotice = definition.RestartNotice;
        Options = options;
        SelectedOption = selectedOption;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (SelectedOption is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            await _settingsStateService.SaveValueAsync(_definition, SelectedOption.Value, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(SaveAsync));
            ErrorMessage = _errorHandler.GetUserMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSave()
    {
        return SelectedOption is not null && !IsLoading;
    }
}
