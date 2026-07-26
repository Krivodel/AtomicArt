using AtomicArt.Desktop.Services.FileReveal;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class RecordingStandardFileRevealer
    : IStandardFileRevealer
{
    public string? FilePath { get; private set; }
    public int CallCount { get; private set; }

    public void Reveal(string filePath)
    {
        CallCount++;
        FilePath = filePath;
    }
}
