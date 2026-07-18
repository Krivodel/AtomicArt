using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class GpuResourceCacheSettingViewModel : SettingItemViewModel
{
    public string SaveButtonText { get; }
    public string RestartNotice { get; }
    public IReadOnlyList<GpuResourceCacheOption> Options { get; }

    private readonly GpuResourceCacheSettingDefinition _definition;
    private readonly ISettingsStateService _settingsStateService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private GpuResourceCacheOption? _selectedOption;

    public GpuResourceCacheSettingViewModel(
        GpuResourceCacheSettingDefinition definition,
        IReadOnlyList<GpuResourceCacheOption> options,
        GpuResourceCacheOption selectedOption,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler)
        : base(definition, errorHandler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(selectedOption);
        ArgumentNullException.ThrowIfNull(settingsStateService);

        _definition = definition;
        _settingsStateService = settingsStateService;
        SaveButtonText = definition.SaveButtonText;
        RestartNotice = definition.RestartNotice;
        Options = options;
        SelectedOption = selectedOption;
    }

    protected override void NotifyActionCanExecuteChanged()
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (SelectedOption is null)
        {
            return;
        }

        await RunOperationAsync(
            () => _settingsStateService.SaveValueAsync(_definition, SelectedOption.Value, ct),
            ct,
            nameof(SaveAsync));
    }

    private bool CanSave()
    {
        return SelectedOption is not null && !IsLoading;
    }
}
