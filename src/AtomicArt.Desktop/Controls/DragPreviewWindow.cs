using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using WindowsNativeLibraryNames = Pica.Viewer.Services.WindowsNativeLibraryNames;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls;

internal sealed class DragPreviewWindow : Window, IDisposable
{
    private const int ImagePreviewSize = 96;
    private const int PromptPreviewWidth = 248;
    private const int PromptPreviewHeight = 112;
    private const int PromptPreviewMaxLines = 4;
    private const int PreviewAnimationDurationMilliseconds = 160;
    private const int CursorOffset = 14;
    private const int PollIntervalMilliseconds = 16;
    private const int ExtendedStyleIndex = -20;
    private const int LayeredWindowStyle = 0x00080000;
    private const int TransparentWindowStyle = 0x00000020;
    private const int ToolWindowStyle = 0x00000080;
    private const int NoActivateWindowPositionFlag = 0x0010;
    private const int NoSizeWindowPositionFlag = 0x0001;

    private static readonly nint TopMostWindowHandle = new(-1);

    private readonly Bitmap? _ownedBitmap;
    private readonly IUiFrameScheduler? _providedFrameScheduler;
    private readonly Control _previewContent;
    private readonly AnimatedTransformState _previewTransformState;
    private AvaloniaUiFrameScheduler? _ownedFrameScheduler;
    private UiAnimationScheduler? _animationScheduler;
    private Timer? _timer;
    private nint _windowHandle;
    private bool _isDisposed;

    private DragPreviewWindow(
        Control previewContent,
        double width,
        double height,
        Bitmap? ownedBitmap,
        IUiFrameScheduler? frameScheduler)
    {
        ArgumentNullException.ThrowIfNull(previewContent);

        _ownedBitmap = ownedBitmap;
        _providedFrameScheduler = frameScheduler;
        _previewContent = previewContent;
        _previewTransformState =
            AnimatedTransformState.GetOrCreate(_previewContent);
        ApplyPreviewScale(0d);

        Width = width;
        Height = height;
        CanResize = false;
        ShowActivated = false;
        ShowInTaskbar = false;
        WindowDecorations = WindowDecorations.None;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Cursor = new Cursor(StandardCursorType.None);
        Content = _previewContent;
    }

    public static DragPreviewWindow CreateOwnedImage(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        return new DragPreviewWindow(
            CreateImageContent(bitmap),
            ImagePreviewSize,
            ImagePreviewSize,
            bitmap,
            null);
    }

    public static DragPreviewWindow CreateBorrowedImage(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        return new DragPreviewWindow(
            CreateImageContent(bitmap),
            ImagePreviewSize,
            ImagePreviewSize,
            null,
            null);
    }

    public static DragPreviewWindow CreatePrompt(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        return new DragPreviewWindow(
            CreatePromptContent(prompt),
            PromptPreviewWidth,
            PromptPreviewHeight,
            null,
            null);
    }

    public static async Task DoDragDropAsync(
        PointerPressedEventArgs pointerPressedEventArgs,
        DataTransfer dataTransfer,
        DragPreviewWindow? previewWindow,
        Window? owner)
    {
        ArgumentNullException.ThrowIfNull(pointerPressedEventArgs);
        ArgumentNullException.ThrowIfNull(dataTransfer);

        using DragPreviewWindow? ownedPreviewWindow = previewWindow;
        ownedPreviewWindow?.Start(owner);

        try
        {
            await DragDrop.DoDragDropAsync(
                pointerPressedEventArgs,
                dataTransfer,
                DragDropEffects.Copy);
        }
        finally
        {
            if (ownedPreviewWindow is not null)
            {
                await ownedPreviewWindow.FinishAsync();
            }
        }
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

        StartAppearanceAnimation();
        ApplyClickThroughStyle();
        IPlatformHandle? handle = TryGetPlatformHandle();

        if (handle is null)
        {
            return;
        }

        _windowHandle = handle.Handle;
        _timer = new Timer(OnTimerTick, null, 0, PollIntervalMilliseconds);
    }

    public Task FinishAsync()
    {
        if (_isDisposed || _animationScheduler is null)
        {
            return Task.CompletedTask;
        }

        _animationScheduler.Cancel(_previewContent);

        return _animationScheduler.AnimateValueAsync(
            _previewContent,
            _previewTransformState.Scale.ScaleX,
            0d,
            PreviewAnimationDurationMilliseconds,
            0,
            MotionEasing.EaseMaterial,
            ApplyPreviewScale);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _animationScheduler?.Cancel(_previewContent);
        _animationScheduler = null;
        _ownedFrameScheduler?.Dispose();
        _ownedFrameScheduler = null;
        _timer?.Dispose();
        _timer = null;
        _ownedBitmap?.Dispose();
        Close();
    }

    internal static DragPreviewWindow CreateBorrowedImage(
        Bitmap bitmap,
        IUiFrameScheduler frameScheduler)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(frameScheduler);

        return new DragPreviewWindow(
            CreateImageContent(bitmap),
            ImagePreviewSize,
            ImagePreviewSize,
            null,
            frameScheduler);
    }

    private static Control CreateImageContent(Bitmap bitmap)
    {
        Image image = new()
        {
            Stretch = Stretch.UniformToFill,
            Source = bitmap
        };

        return new Border
        {
            Width = ImagePreviewSize,
            Height = ImagePreviewSize,
            Opacity = 0.86,
            ClipToBounds = true,
            CornerRadius = new CornerRadius(8d),
            Background = Brushes.Transparent,
            Child = image
        };
    }

    private static Control CreatePromptContent(string prompt)
    {
        OverflowEllipsisTextBlock text = new()
        {
            Text = prompt,
            MaxLines = PromptPreviewMaxLines,
            TextWrapping = TextWrapping.Wrap
        };
        text.Classes.Add("prompt-drag-preview-text");

        Border border = new()
        {
            Width = PromptPreviewWidth,
            Height = PromptPreviewHeight,
            Child = text
        };
        border.Classes.Add("prompt-drag-preview");

        return border;
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

    [DllImport(
        WindowsNativeLibraryNames.User32,
        EntryPoint = "GetWindowLongPtrW",
        SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport(
        WindowsNativeLibraryNames.User32,
        EntryPoint = "SetWindowLongPtrW",
        SetLastError = true)]
    private static extern nint SetWindowLongPtr(
        nint windowHandle,
        int index,
        nint value);

    [DllImport(WindowsNativeLibraryNames.User32, SetLastError = true)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfterWindowHandle,
        int x,
        int y,
        int width,
        int height,
        int flags);

    private void StartAppearanceAnimation()
    {
        _ownedFrameScheduler = _providedFrameScheduler is null
            ? new AvaloniaUiFrameScheduler(this)
            : null;
        IUiFrameScheduler frameScheduler =
            _providedFrameScheduler ?? _ownedFrameScheduler
            ?? throw new InvalidOperationException(
                "Drag preview animation scheduler was not created.");
        _animationScheduler = new UiAnimationScheduler(frameScheduler);

        _ = _animationScheduler.AnimateValueAsync(
            _previewContent,
            _previewTransformState.Scale.ScaleX,
            1d,
            PreviewAnimationDurationMilliseconds,
            0,
            MotionEasing.EaseMaterial,
            ApplyPreviewScale);
    }

    private void ApplyPreviewScale(double scale)
    {
        _previewTransformState.Scale.ScaleX = scale;
        _previewTransformState.Scale.ScaleY = scale;
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
        nint nextStyles = styles
            | (nint)LayeredWindowStyle
            | (nint)TransparentWindowStyle
            | (nint)ToolWindowStyle;
        _ = SetWindowLongPtr(handle.Handle, ExtendedStyleIndex, nextStyles);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
