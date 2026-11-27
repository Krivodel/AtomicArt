using Avalonia;
using Moq;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Notifications;
using AtomicArt.Desktop.Tests;
using AtomicArt.Desktop.Tests.Services.Notifications;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Views.Notifications;

namespace AtomicArt.Desktop.Rendering.Tests.Views.Notifications;

internal sealed class NotificationPresentationTestContext : IAsyncDisposable
{
    public NotificationTestClock Clock { get; } = new();
    public Mock<INotificationSoundPlayer> Sound { get; } = new();
    public Mock<IApplicationFocusService> Focus { get; } = new();
    public Mock<IWindowStateService> WindowState { get; } = new();
    public List<GenerationNotificationWindow> Windows { get; } = [];
    public CancellationToken Token => _lifetime.Token;

    private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(15));
    private readonly Dictionary<GenerationNotificationWindow, TestUiFrameScheduler> _frames = [];
    private readonly List<Task> _presentations = [];
    private readonly GenerationNotificationPresenter _presenter;
    private double _frameTime;

    public NotificationPresentationTestContext()
    {
        WindowState.Setup(state => state.ShowAndActivate())
            .Callback(() => Focus.SetupGet(focus => focus.IsFocused).Returns(true));
        _presenter = new GenerationNotificationPresenter(new AvaloniaUiThreadDispatcher(),
            TestLocalizationTextProvider.Default, Sound.Object, Clock.Time, Focus.Object, WindowState.Object, CreateWindow);
    }

    public Task StartAsync(string prompt = "Напиши текстом a1, не генерируй картинку.")
    {
        Task showing = _presenter.ShowAsync(new GenerationNotification(prompt, false), _lifetime.Token);
        _presentations.Add(showing);
        return showing;
    }

    public async Task WaitForVisibleAsync(int count)
    {
        while (Windows.Count(window => window.IsVisible) < count)
        {
            await Task.Delay(1, Token);
        }
    }

    public async Task StepAsync(double milliseconds)
    {
        _frameTime += milliseconds;

        foreach (TestUiFrameScheduler frames in _frames.Values.ToArray())
        {
            frames.RunFrame(TimeSpan.FromMilliseconds(_frameTime));
        }

        await Task.Delay(1);

        foreach (GenerationNotificationWindow window in Windows.Where(window => window.IsVisible && !window.IsNotificationClosed))
        {
            window.UpdateLayout();
        }
    }

    public async Task WaitForClosingAsync(GenerationNotificationWindow window)
    {
        while (!window.IsDismissalInProgress)
        {
            await Task.Delay(1, Token);
        }
    }

    public async Task CompleteAnimationsAsync()
    {
        for (int step = 0; step < 12; step++)
        {
            await StepAsync(100);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        await CompleteAnimationsAsync();

        foreach (GenerationNotificationWindow window in Windows)
        {
            window.Close();
        }

        await Task.WhenAll(_presentations).WaitAsync(TimeSpan.FromSeconds(3));
        _presenter.Dispose();
        _lifetime.Dispose();
    }

    private GenerationNotificationWindow CreateWindow()
    {
        TestUiFrameScheduler frames = new();
        GenerationNotificationWindow window = new(frames);
        _frames.Add(window, frames);
        Windows.Add(window);
        return window;
    }
}
