using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class ScaleSettingViewModel : SettingItemViewModel
{
    public string ApplyButtonText { get; }
    public IReadOnlyList<UiScaleOption> Options { get; }

    private readonly IScaleSettingDefinition _definition;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IUiScaleSettingValueConverter _valueConverter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private UiScaleOption? _selectedOption;

    public ScaleSettingViewModel(
        IScaleSettingDefinition definition,
        IReadOnlyList<UiScaleOption> options,
        UiScaleOption? selectedOption,
        ISettingsStateService settingsStateService,
        IUiScaleSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler)
        : base(definition, errorHandler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settingsStateService);
        ArgumentNullException.ThrowIfNull(valueConverter);

        _definition = definition;
        ApplyButtonText = definition.ApplyButtonText;
        Options = options;
        SelectedOption = selectedOption;
        _settingsStateService = settingsStateService;
        _valueConverter = valueConverter;
    }

    protected override void NotifyActionCanExecuteChanged()
    {
        ApplyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync(CancellationToken ct)
    {
        if (SelectedOption is null)
        {
            return;
        }

        await RunOperationAsync(
            async () =>
            {
                string value = _valueConverter.Format(SelectedOption.Value);
                _settingsStateService.ApplyValue(_definition, value);
                await _settingsStateService.SaveValueAsync(_definition, value, ct);
            },
            ct,
            nameof(ApplyAsync));
    }

    private bool CanApply()
    {
        return SelectedOption is not null && !IsLoading;
    }
}
