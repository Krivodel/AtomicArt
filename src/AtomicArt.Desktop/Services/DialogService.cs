using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.ViewModels.Dialogs;

namespace AtomicArt.Desktop.Services;

public sealed class DialogService :
    IDialogService,
    IRecipient<LocalizationChangedMessage>
{
    private readonly ErrorDialogViewModel _errorDialog;
    private readonly IConfirmationDialogPresenter _confirmationPresenter;
    private readonly ILocalizationTextProvider _textProvider;
    private string? _activeErrorLocalizationKey;
    private LocalizedConfirmationDialogRequest? _activeConfirmationRequest;

    public DialogService(
        ErrorDialogViewModel errorDialog,
        IConfirmationDialogPresenter confirmationPresenter,
        ILocalizationTextProvider textProvider,
        IMessenger messenger)
    {
        _errorDialog = errorDialog
            ?? throw new ArgumentNullException(nameof(errorDialog));
        _confirmationPresenter = confirmationPresenter
            ?? throw new ArgumentNullException(nameof(confirmationPresenter));
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
        ArgumentNullException.ThrowIfNull(messenger);

        messenger.Register<LocalizationChangedMessage>(this);
    }

    public void ShowError(string message)
    {
        _activeErrorLocalizationKey = null;
        _errorDialog.Open(message);
    }

    public void ShowLocalizedError(string localizationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationKey);

        _activeErrorLocalizationKey = localizationKey;
        _errorDialog.Open(_textProvider.Get(localizationKey));
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

    public async Task<bool> ShowConfirmationAsync(
        LocalizedConfirmationDialogRequest request,
        CancellationToken ct)
    {
        ValidateConfirmationRequest(request);
        _activeConfirmationRequest = request;

        try
        {
            return await _confirmationPresenter.ShowAsync(
                    CreateConfirmationPresentation(request),
                    ct)
                .ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_activeConfirmationRequest, request))
            {
                _activeConfirmationRequest = null;
            }
        }
    }

    public void Receive(LocalizationChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_activeErrorLocalizationKey is not null && _errorDialog.IsOpen)
        {
            _errorDialog.Open(_textProvider.Get(_activeErrorLocalizationKey));
        }

        LocalizedConfirmationDialogRequest? request =
            _activeConfirmationRequest;

        if ((request is not null) && _confirmationPresenter.IsOpen)
        {
            _confirmationPresenter.Update(
                CreateConfirmationPresentation(request));
        }
    }

    private static void ValidateConfirmationRequest(
        LocalizedConfirmationDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TitleLocalizationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageLocalizationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ConfirmActionLocalizationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.CancelActionLocalizationKey);
        ArgumentNullException.ThrowIfNull(request.MessageArguments);
    }

    private ConfirmationDialogPresentation CreateConfirmationPresentation(
        LocalizedConfirmationDialogRequest request)
    {
        return new ConfirmationDialogPresentation(
            GetConfirmationTitle(request),
            GetConfirmationMessage(request),
            GetConfirmationActionText(request),
            GetConfirmationCancelActionText(request),
            request.Kind,
            request.BackgroundClickBehavior);
    }

    private string GetConfirmationTitle(
        LocalizedConfirmationDialogRequest request)
    {
        return _textProvider.Get(request.TitleLocalizationKey);
    }

    private string GetConfirmationMessage(
        LocalizedConfirmationDialogRequest request)
    {
        return _textProvider.Format(
            request.MessageLocalizationKey,
            request.MessageArguments.ToArray());
    }

    private string GetConfirmationActionText(
        LocalizedConfirmationDialogRequest request)
    {
        return _textProvider.Get(request.ConfirmActionLocalizationKey);
    }

    private string GetConfirmationCancelActionText(
        LocalizedConfirmationDialogRequest request)
    {
        return _textProvider.Get(request.CancelActionLocalizationKey);
    }
}
