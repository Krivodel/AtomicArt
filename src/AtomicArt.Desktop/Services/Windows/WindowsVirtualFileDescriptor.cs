namespace AtomicArt.Desktop.Services.Windows;

internal sealed record WindowsVirtualFileDescriptor(
    string FileName,
    ulong? DeclaredSize,
    bool IsDirectory);
