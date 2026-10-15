using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class ImageViewerWindowLimitSettingViewModel : SettingItemViewModel, IDisposable
{
    public int Minimum => ImageViewerWindowLimitSettingDefinition.MinimumLimit;
    public int Maximum => ImageViewerWindowLimitSettingDefinition.MaximumLimit;
    public bool IsInputEnabled => IsLimitEnabled && !IsLoading;

    protected override IRelayCommand OperationCommand => ApplyCommand;

    private readonly ImageViewerWindowLimitSettingDefinition _definition;
    private readonly ImageViewerWindowLimitService _limitService;
    private readonly ISettingsStateService _settingsStateService;
    private bool _isSynchronizing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputEnabled))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _isLimitEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private decimal? _count;

    public ImageViewerWindowLimitSettingViewModel(
        ImageViewerWindowLimitSettingDefinition definition,
        ImageViewerWindowLimitService limitService,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, errorHandler, textProvider)
    {
        _definition = definition;
        _limitService = limitService ?? throw new ArgumentNullException(nameof(limitService));
        _settingsStateService = settingsStateService ?? throw new ArgumentNullException(nameof(settingsStateService));
        _isLimitEnabled = limitService.IsEnabled;
        _count = limitService.Count;
        _limitService.ValueChanged += OnValueChanged;
    }

    public void Dispose()
    {
        _limitService.ValueChanged -= OnValueChanged;
    }

    protected override void NotifyOperationCanExecuteChanged()
    {
        base.NotifyOperationCanExecuteChanged();
        OnPropertyChanged(nameof(IsInputEnabled));
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync(CancellationToken ct)
    {
        if (!CanApply())
        {
            return;
        }

        await RunOperationAsync(
            async () =>
            {
                int count = Count is decimal value && ImageViewerWindowLimitSettingDefinition.IsValidLimit(value)
                    ? (int)value
                    : _limitService.Count;
                _limitService.SetLimit(IsLimitEnabled, count);
                await _settingsStateService.SaveValueAsync(_definition, _limitService.GetSerializedValue(), ct);
            },
            ct,
            nameof(ApplyAsync));
    }

    private bool CanApply()
    {
        return !IsLoading
            && (!IsLimitEnabled || (Count is decimal count && ImageViewerWindowLimitSettingDefinition.IsValidLimit(count)))
            && (IsLimitEnabled != _limitService.IsEnabled || Count != _limitService.Count);
    }

    private void OnValueChanged(object? sender, EventArgs e)
    {
        _isSynchronizing = true;

        try
        {
            IsLimitEnabled = _limitService.IsEnabled;
            Count = _limitService.Count;
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    partial void OnIsLimitEnabledChanged(bool value)
    {
        if (!_isSynchronizing && ApplyCommand.CanExecute(null))
        {
            ApplyCommand.Execute(null);
        }
    }
}
