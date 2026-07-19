using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingTextClipboardService : ITextClipboardService
{
    public string? Text { get; private set; }

    public Task SetTextAsync(string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);
        ct.ThrowIfCancellationRequested();

        Text = text;
        return Task.CompletedTask;
    }
}
