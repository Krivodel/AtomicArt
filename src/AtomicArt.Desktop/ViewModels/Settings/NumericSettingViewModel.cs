using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class NumericSettingViewModel :
    SelectableSettingItemViewModel<NumericSettingOption>,
    IDisposable
{
    protected override IRelayCommand OperationCommand => ApplyCommand;

    private readonly IDisplaySettingDefinition _definition;
    private readonly INumericSettingValueSource _valueSource;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IDoubleSettingValueConverter _valueConverter;

    public NumericSettingViewModel(
        IDisplaySettingDefinition definition,
        IReadOnlyList<NumericSettingOption> options,
        INumericSettingValueSource valueSource,
        ISettingsStateService settingsStateService,
        IDoubleSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(
            definition,
            options,
            FindSelectedOption(options, valueSource),
            errorHandler,
            textProvider)
    {
        ArgumentNullException.ThrowIfNull(valueSource);
        ArgumentNullException.ThrowIfNull(settingsStateService);
        ArgumentNullException.ThrowIfNull(valueConverter);

        _definition = definition;
        _valueSource = valueSource;
        _settingsStateService = settingsStateService;
        _valueConverter = valueConverter;
        _valueSource.ValueChanged += OnValueChanged;
    }

    public void Dispose()
    {
        _valueSource.ValueChanged -= OnValueChanged;
    }

    protected override void OnSelectedOptionChanged(NumericSettingOption? selectedOption)
    {
        if (selectedOption is not null)
        {
            ApplyCommand.Execute(null);
        }
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
        SynchronizeSelectedOption(FindSelectedOption(Options, _valueSource));
    }
}
