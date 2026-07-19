using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class ScaleSettingViewModel : SelectableSettingItemViewModel<UiScaleOption>
{
    public override string ActionText => ApplyButtonText;
    public override System.Windows.Input.ICommand ActionCommand => ApplyCommand;
    public string ApplyButtonText { get; }

    private readonly IScaleSettingDefinition _definition;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IUiScaleSettingValueConverter _valueConverter;

    public ScaleSettingViewModel(
        IScaleSettingDefinition definition,
        IReadOnlyList<UiScaleOption> options,
        UiScaleOption? selectedOption,
        ISettingsStateService settingsStateService,
        IUiScaleSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler)
        : base(definition, options, selectedOption, errorHandler)
    {
        ArgumentNullException.ThrowIfNull(settingsStateService);
        ArgumentNullException.ThrowIfNull(valueConverter);

        _definition = definition;
        ApplyButtonText = definition.ApplyButtonText;
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
        if (SelectedOption is not { } selectedOption)
        {
            return;
        }

        await RunOperationAsync(
            async () =>
            {
                string value = _valueConverter.Format(selectedOption.Value);
                _settingsStateService.ApplyValue(_definition, value);
                await _settingsStateService.SaveValueAsync(_definition, value, ct);
            },
            ct,
            nameof(ApplyAsync));
    }

    private bool CanApply()
    {
        return HasSelectedOption && !IsLoading;
    }
}
