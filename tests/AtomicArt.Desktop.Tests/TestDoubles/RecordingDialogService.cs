using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingDialogService : IDialogService
{
    public IReadOnlyList<string> ErrorMessages => _errorMessages;

    private readonly List<string> _errorMessages = [];

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
}
