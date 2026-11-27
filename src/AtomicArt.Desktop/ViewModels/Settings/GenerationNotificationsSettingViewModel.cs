using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Notifications;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class GenerationNotificationsSettingViewModel : BooleanSettingViewModel
{
    public bool HasCustomSound => _soundStore.HasCustomSound;
    public string SoundButtonText => TextProvider.Get(NotificationLocalizationKeys.UploadSound);

    private readonly INotificationSoundStore _soundStore;
    private readonly ISoundFilePickerService _picker;

    public GenerationNotificationsSettingViewModel(
        GenerationNotificationsSettingDefinition definition,
        GenerationNotificationSettings settings,
        ISettingsStateService state,
        IBooleanSettingValueConverter converter,
        INotificationSoundStore soundStore,
        ISoundFilePickerService picker,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, settings, state, converter, errorHandler, textProvider)
    {
        _soundStore = soundStore ?? throw new ArgumentNullException(nameof(soundStore));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
    }

    public override void RefreshLocalization()
    {
        base.RefreshLocalization();
        OnPropertyChanged(nameof(SoundButtonText));
    }

    protected override void NotifyOperationCanExecuteChanged()
    {
        base.NotifyOperationCanExecuteChanged();
        UploadSoundCommand.NotifyCanExecuteChanged();
        ResetSoundCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanChangeSound))]
    private async Task UploadSoundAsync(CancellationToken ct)
    {
        await RunOperationAsync(async () =>
        {
            string? path = await _picker.PickSoundAsync(ct);
            if (path is not null)
            {
                await _soundStore.ImportAsync(path, ct);
                RefreshSound();
            }
        }, ct, nameof(UploadSoundAsync));
    }

    [RelayCommand(CanExecute = nameof(CanChangeSound))]
    private async Task ResetSoundAsync(CancellationToken ct)
    {
        await RunOperationAsync(async () =>
        {
            await _soundStore.ResetAsync(ct);
            RefreshSound();
        }, ct, nameof(ResetSoundAsync));
    }

    private bool CanChangeSound()
    {
        return !IsLoading;
    }

    private void RefreshSound()
    {
        OnPropertyChanged(nameof(HasCustomSound));
    }
}
