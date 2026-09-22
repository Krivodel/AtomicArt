using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

using SkiaSharp;

using AtomicArt.Tests.Avalonia;

namespace AtomicArt.Desktop.Tests.Common;

public abstract class DesktopControlTestBase
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<DesktopControlTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    private protected static void Dispatch(Action action)
    {
        HeadlessTestSessionDispatcher.Dispatch(
            typeof(DesktopControlTestBase),
            SessionLock,
            action);
    }

    private protected static async Task DispatchAsync(Func<Task> action)
    {
        await HeadlessTestSessionDispatcher.DispatchAsync(
            typeof(DesktopControlTestBase),
            SessionLock,
            action);
    }

    private protected static Window Show(Control control)
    {
        return DesktopControlTestBase.Show(control, 640d, 640d);
    }

    private protected static void Show(Control control, Action<Window> action)
    {
        DesktopControlTestBase.Show(control, 640d, 640d, action);
    }

    private protected static Window Show(
        Control control,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(control);

        Window window = new()
        {
            Width = width,
            Height = height,
            Content = control
        };

        window.Show();
        window.CaptureRenderedFrame();

        return window;
    }

    private protected static void Show(
        Control control,
        double width,
        double height,
        Action<Window> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Window window = DesktopControlTestBase.Show(control, width, height);

        try
        {
            action(window);
        }
        finally
        {
            window.Close();
        }
    }

    private protected SKBitmap CaptureRenderedBitmap(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        using Bitmap frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Rendered frame was not captured.");
        using MemoryStream stream = new();
        frame.Save(stream);
        stream.Position = 0;

        return SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("Rendered frame could not be decoded.");
    }
}
