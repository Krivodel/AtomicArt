using AtomicArt.Desktop.Services;

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
}
