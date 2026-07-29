using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.ViewModels.Dialogs;

namespace AtomicArt.Desktop.Services;

public sealed class DialogService :
    IDialogService,
    IRecipient<LocalizationChangedMessage>
{
    private readonly ErrorDialogViewModel _dialog;
    private readonly ILocalizationTextProvider _textProvider;
    private string? _activeLocalizationKey;

    public DialogService(
        ErrorDialogViewModel dialog,
        ILocalizationTextProvider textProvider,
        IMessenger messenger)
    {
        _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        ArgumentNullException.ThrowIfNull(messenger);

        messenger.Register<LocalizationChangedMessage>(this);
    }

    public void ShowError(string message)
    {
        _activeLocalizationKey = null;
        _dialog.Open(message);
    }

    public void ShowLocalizedError(string localizationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationKey);

        _activeLocalizationKey = localizationKey;
        _dialog.Open(_textProvider.Get(localizationKey));
    }

    public Task ShowErrorAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ShowError(message);

        return Task.CompletedTask;
    }

    public Task ShowLocalizedErrorAsync(
        string localizationKey,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ShowLocalizedError(localizationKey);

        return Task.CompletedTask;
    }

    public void Receive(LocalizationChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_activeLocalizationKey is not null && _dialog.IsOpen)
        {
            _dialog.Open(_textProvider.Get(_activeLocalizationKey));
        }
    }
}
