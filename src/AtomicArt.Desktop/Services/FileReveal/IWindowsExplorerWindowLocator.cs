namespace AtomicArt.Desktop.Services.FileReveal;

internal interface IWindowsExplorerWindowLocator
{
    IWindowsExplorerWindow? Find(string directoryPath);
}
