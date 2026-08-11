using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingDialogService : IDialogService
{
    public IReadOnlyList<string> ErrorMessages => _errorMessages;
    public IReadOnlyList<LocalizedConfirmationDialogRequest> ConfirmationRequests =>
        _confirmationRequests;
    public bool ConfirmationResult { get; set; } = true;

    private readonly List<string> _errorMessages = [];
    private readonly List<LocalizedConfirmationDialogRequest> _confirmationRequests = [];

    public void ShowError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        _errorMessages.Add(message);
    }

    public Task ShowErrorAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ShowError(message);

        return Task.CompletedTask;
    }

    public void ShowLocalizedError(string localizationKey)
    {
        ShowError(TestLocalizationTextProvider.Default.Get(localizationKey));
    }

    public Task ShowLocalizedErrorAsync(
        string localizationKey,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ShowLocalizedError(localizationKey);

        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmationAsync(
        LocalizedConfirmationDialogRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        _confirmationRequests.Add(request);

        return Task.FromResult(ConfirmationResult);
    }
}
