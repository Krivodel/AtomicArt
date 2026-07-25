using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class GpuResourceCacheSettingViewModel :
    SelectableSettingItemViewModel<GpuResourceCacheOption>
{
    public string RestartNotice { get; }

    protected override IRelayCommand OperationCommand => SaveCommand;

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
        RestartNotice = definition.RestartNotice;
    }

    protected override void OnSelectedOptionChanged(GpuResourceCacheOption? selectedOption)
    {
        if (selectedOption is not null)
        {
            SaveCommand.Execute(null);
        }
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
