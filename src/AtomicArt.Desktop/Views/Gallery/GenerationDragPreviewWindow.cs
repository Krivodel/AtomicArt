using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationDragPreviewWindow : Window, IDisposable
{
    private const int PreviewSize = 96;
    private const int CursorOffset = 14;
    private const int PollIntervalMilliseconds = 16;
    private const int ExtendedStyleIndex = -20;
    private const int LayeredWindowStyle = 0x00080000;
    private const int TransparentWindowStyle = 0x00000020;
    private const int ToolWindowStyle = 0x00000080;
    private const int NoActivateWindowPositionFlag = 0x0010;
    private const int NoSizeWindowPositionFlag = 0x0001;
    private static readonly nint TopMostWindowHandle = new(-1);

    private readonly Bitmap _bitmap;
    private Timer? _timer;
    private nint _windowHandle;
    private bool _isDisposed;

    public GenerationDragPreviewWindow(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        _bitmap = bitmap;

        Width = PreviewSize;
        Height = PreviewSize;
        CanResize = false;
        ShowActivated = false;
        ShowInTaskbar = false;
        WindowDecorations = WindowDecorations.None;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Cursor = new Cursor(StandardCursorType.None);
        Content = CreateContent(bitmap);
    }

    public void Start(Window? owner)
    {
        if (_isDisposed)
        {
            return;
        }

        MoveToCursor();
        if (owner is not null)
        {
            Show(owner);
        }
        else
        {
            Show();
        }

        ApplyClickThroughStyle();
        IPlatformHandle? handle = TryGetPlatformHandle();
        if (handle is null)
        {
            return;
        }

        _windowHandle = handle.Handle;
        _timer = new Timer(OnTimerTick, null, 0, PollIntervalMilliseconds);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _timer?.Dispose();
        _timer = null;
        _bitmap.Dispose();
        Close();
    }

    private static Control CreateContent(Bitmap bitmap)
    {
        Image image = new()
        {
            Stretch = Stretch.UniformToFill,
            Source = bitmap
        };

        return new Border
        {
            Width = PreviewSize,
            Height = PreviewSize,
            Opacity = 0.86,
            ClipToBounds = true,
            CornerRadius = new CornerRadius(8d),
            Background = Brushes.Transparent,
            Child = image
        };
    }

    private void OnTimerTick(object? state)
    {
        _ = state;

        if (_isDisposed || _windowHandle == 0)
        {
            return;
        }

        MoveNativeWindowToCursor(_windowHandle);
    }

    private void MoveToCursor()
    {
        if (!TryGetCursorPosition(out NativePoint point))
        {
            return;
        }

        Position = new PixelPoint(point.X + CursorOffset, point.Y + CursorOffset);
    }

    private static void MoveNativeWindowToCursor(nint windowHandle)
    {
        if (!TryGetCursorPosition(out NativePoint point))
        {
            return;
        }

        _ = SetWindowPos(
            windowHandle,
            TopMostWindowHandle,
            point.X + CursorOffset,
            point.Y + CursorOffset,
            0,
            0,
            NoActivateWindowPositionFlag | NoSizeWindowPositionFlag);
    }

    private void ApplyClickThroughStyle()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IPlatformHandle? handle = TryGetPlatformHandle();
        if (handle is null)
        {
            return;
        }

        nint styles = GetWindowLongPtr(handle.Handle, ExtendedStyleIndex);
        nint nextStyles = styles | (nint)LayeredWindowStyle | (nint)TransparentWindowStyle | (nint)ToolWindowStyle;
        _ = SetWindowLongPtr(handle.Handle, ExtendedStyleIndex, nextStyles);
    }

    private static bool TryGetCursorPosition(out NativePoint point)
    {
        if (OperatingSystem.IsWindows())
        {
            return GetCursorPos(out point);
        }

        point = default;

        return false;
    }

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport(WindowsNativeLibraryNames.User32, EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport(WindowsNativeLibraryNames.User32, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint value);

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfterWindowHandle,
        int x,
        int y,
        int width,
        int height,
        int flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
