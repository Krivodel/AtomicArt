using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class BooleanSettingViewModel : SettingItemViewModel, IDisposable
{
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value) && !_isSynchronizingValue)
            {
                ApplyCommand.Execute(null);
            }
        }
    }

    protected override IRelayCommand OperationCommand => ApplyCommand;

    private readonly IDisplaySettingDefinition _definition;
    private readonly IBooleanSettingValueSource _valueSource;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IBooleanSettingValueConverter _valueConverter;
    private bool _isChecked;
    private bool _isSynchronizingValue;

    public BooleanSettingViewModel(
        IDisplaySettingDefinition definition,
        IBooleanSettingValueSource valueSource,
        ISettingsStateService settingsStateService,
        IBooleanSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, errorHandler, textProvider)
    {
        ArgumentNullException.ThrowIfNull(valueSource);
        ArgumentNullException.ThrowIfNull(settingsStateService);
        ArgumentNullException.ThrowIfNull(valueConverter);

        _definition = definition;
        _valueSource = valueSource;
        _settingsStateService = settingsStateService;
        _valueConverter = valueConverter;
        _isChecked = valueSource.CurrentValue;
        _valueSource.ValueChanged += OnValueChanged;
    }

    public void Dispose()
    {
        _valueSource.ValueChanged -= OnValueChanged;
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync(CancellationToken ct)
    {
        await RunOperationAsync(
            async () =>
            {
                string value = _valueConverter.Format(IsChecked);
                _settingsStateService.ApplyValue(_definition, value);
                await _settingsStateService.SaveValueAsync(_definition, value, ct);
            },
            ct,
            nameof(ApplyAsync));
    }

    private bool CanApply()
    {
        return !IsLoading;
    }

    private void SynchronizeValue(bool value)
    {
        _isSynchronizingValue = true;

        try
        {
            IsChecked = value;
        }
        finally
        {
            _isSynchronizingValue = false;
        }
    }

    private void OnValueChanged(object? sender, EventArgs e)
    {
        SynchronizeValue(_valueSource.CurrentValue);
    }
}
