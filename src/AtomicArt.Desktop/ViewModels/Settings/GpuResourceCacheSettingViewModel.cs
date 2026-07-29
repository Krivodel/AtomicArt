using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class GpuResourceCacheSettingViewModel :
    SelectableSettingItemViewModel<GpuResourceCacheOptionViewModel>
{
    public string RestartNotice => TextProvider.Get(_definition.RestartNoticeKey);

    protected override IRelayCommand OperationCommand => SaveCommand;

    private readonly GpuResourceCacheSettingDefinition _definition;
    private readonly ISettingsStateService _settingsStateService;

    public GpuResourceCacheSettingViewModel(
        GpuResourceCacheSettingDefinition definition,
        IReadOnlyList<GpuResourceCacheOptionViewModel> options,
        GpuResourceCacheOptionViewModel selectedOption,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, options, selectedOption, errorHandler, textProvider)
    {
        ArgumentNullException.ThrowIfNull(selectedOption);
        ArgumentNullException.ThrowIfNull(settingsStateService);

        _definition = definition;
        _settingsStateService = settingsStateService;
    }

    public override void RefreshLocalization()
    {
        base.RefreshLocalization();

        foreach (GpuResourceCacheOptionViewModel option in Options)
        {
            option.RefreshLocalization();
        }

        OnPropertyChanged(nameof(RestartNotice));
    }

    protected override void OnSelectedOptionChanged(
        GpuResourceCacheOptionViewModel? selectedOption)
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
