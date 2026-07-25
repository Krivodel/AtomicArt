using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class NumericSettingViewModel :
    SelectableSettingItemViewModel<NumericSettingOption>,
    IDisposable
{
    public override string ActionText { get; }
    public override IRelayCommand ActionCommand => ApplyCommand;

    private readonly IActionSettingDefinition _definition;
    private readonly INumericSettingValueSource _valueSource;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IDoubleSettingValueConverter _valueConverter;

    public NumericSettingViewModel(
        IActionSettingDefinition definition,
        IReadOnlyList<NumericSettingOption> options,
        INumericSettingValueSource valueSource,
        ISettingsStateService settingsStateService,
        IDoubleSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler)
        : base(
            definition,
            options,
            FindSelectedOption(options, valueSource),
            errorHandler)
    {
        ArgumentNullException.ThrowIfNull(valueSource);
        ArgumentNullException.ThrowIfNull(settingsStateService);
        ArgumentNullException.ThrowIfNull(valueConverter);

        _definition = definition;
        ActionText = definition.ActionText;
        _valueSource = valueSource;
        _settingsStateService = settingsStateService;
        _valueConverter = valueConverter;
        _valueSource.ValueChanged += OnValueChanged;
    }

    public void Dispose()
    {
        _valueSource.ValueChanged -= OnValueChanged;
    }

    private static NumericSettingOption? FindSelectedOption(
        IReadOnlyList<NumericSettingOption> options,
        INumericSettingValueSource valueSource)
    {
        ArgumentNullException.ThrowIfNull(valueSource);

        return NumericSettingOptionMatcher.FindByValueOrFirst(
            options,
            valueSource.CurrentValue);
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync(CancellationToken ct)
    {
        if (SelectedOption is not NumericSettingOption selectedOption)
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

    private void OnValueChanged(object? sender, EventArgs e)
    {
        SelectedOption = FindSelectedOption(Options, _valueSource);
    }
}
