using System.Runtime.InteropServices;

namespace AtomicArt.Desktop.Services.Windows;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativePoint
{
    public readonly int X;
    public readonly int Y;
}
