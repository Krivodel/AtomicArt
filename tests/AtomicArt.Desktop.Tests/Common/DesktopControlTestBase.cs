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

    protected static void Dispatch(Action action)
    {
        HeadlessTestSessionDispatcher.Dispatch(
            typeof(DesktopControlTestBase),
            SessionLock,
            action);
    }

    protected static Task DispatchAsync(Func<Task> action)
    {
        return DesktopControlTestBase.DispatchAsync(typeof(DesktopControlTestBase), action);
    }

    protected static Task DispatchAsync(Action action)
    {
        return DispatchAsync(typeof(DesktopControlTestBase), action);
    }

    protected static Task DispatchAsync(Type testApplicationType, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return DesktopControlTestBase.DispatchAsync(
            testApplicationType,
            () =>
            {
                action();

                return Task.CompletedTask;
            });
    }

    protected static Task DispatchAsync(Type testApplicationType, Func<Task> action)
    {
        return HeadlessTestSessionDispatcher.DispatchAsync(testApplicationType, SessionLock, action);
    }

    protected static Window Show(Control control)
    {
        return DesktopControlTestBase.Show(control, 640d, 640d);
    }

    protected static void Show(Control control, Action<Window> action)
    {
        DesktopControlTestBase.Show(control, 640d, 640d, action);
    }

    protected static Window Show(
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

    protected static void Show(
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

    protected SKBitmap CaptureRenderedBitmap(Window window)
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
