namespace AtomicArt.Desktop.Services.FileReveal;

internal interface IWindowsExplorerWindow : IDisposable
{
    void SelectFile(string fileName);
}
