using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class GpuResourceCacheSettingViewModel :
    SelectableSettingItemViewModel<GpuResourceCacheOption>
{
    public override string ActionText => SaveButtonText;
    public override IRelayCommand ActionCommand => SaveCommand;
    public string SaveButtonText { get; }
    public string RestartNotice { get; }

    private readonly GpuResourceCacheSettingDefinition _definition;
    private readonly ISettingsStateService _settingsStateService;

    public GpuResourceCacheSettingViewModel(
        GpuResourceCacheSettingDefinition definition,
        IReadOnlyList<GpuResourceCacheOption> options,
        GpuResourceCacheOption selectedOption,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler)
        : base(definition, options, selectedOption, errorHandler)
    {
        ArgumentNullException.ThrowIfNull(selectedOption);
        ArgumentNullException.ThrowIfNull(settingsStateService);

        _definition = definition;
        _settingsStateService = settingsStateService;
        SaveButtonText = definition.SaveButtonText;
        RestartNotice = definition.RestartNotice;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (SelectedOption is not { } selectedOption)
        {
            return;
        }

        await RunOperationAsync(
            () => _settingsStateService.SaveValueAsync(_definition, selectedOption.Value, ct),
            ct,
            nameof(SaveAsync));
    }

    private bool CanSave()
    {
        return HasSelectedOption && !IsLoading;
    }
}
