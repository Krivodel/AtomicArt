using Avalonia;
using Avalonia.Platform;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Notifications;
using AtomicArt.Desktop.ViewModels.Notifications;

namespace AtomicArt.Desktop.Views.Notifications;

public sealed class GenerationNotificationPresenter : IGenerationNotificationPresenter, IDisposable
{
    private const int MaximumVisibleNotifications = 3;
    private const int ScreenMargin = 16;
    private const int CardSpacing = 12;

    private readonly object _scopeLock = new();
    private readonly SemaphoreSlim _slots = new(MaximumVisibleNotifications, MaximumVisibleNotifications);
    private readonly List<GenerationNotificationWindow> _windows = [];
    private readonly IUiThreadDispatcher _dispatcher;
    private readonly ILocalizationTextProvider _text;
    private readonly INotificationSoundPlayer _sound;
    private readonly IApplicationFocusService _focus;
    private readonly IWindowStateService _windowState;
    private readonly NotificationHoverGroup _hover;
    private readonly Func<GenerationNotificationWindow> _createWindow;
    private TaskCompletionSource _spaceAvailable = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource _scope = new();
    private bool _disposed;

    public GenerationNotificationPresenter(IUiThreadDispatcher dispatcher, ILocalizationTextProvider text,
        INotificationSoundPlayer sound, TimeProvider time, IApplicationFocusService focus, IWindowStateService windowState)
        : this(dispatcher, text, sound, time, focus, windowState, () => new GenerationNotificationWindow())
    {
    }

    internal GenerationNotificationPresenter(IUiThreadDispatcher dispatcher, ILocalizationTextProvider text,
        INotificationSoundPlayer sound, TimeProvider time, IApplicationFocusService focus, IWindowStateService windowState,
        Func<GenerationNotificationWindow> createWindow)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _sound = sound ?? throw new ArgumentNullException(nameof(sound));
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _windowState = windowState ?? throw new ArgumentNullException(nameof(windowState));
        _hover = new NotificationHoverGroup(time);
        _createWindow = createWindow ?? throw new ArgumentNullException(nameof(createWindow));
    }

    public async Task ShowAsync(GenerationNotification notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);
        CancellationToken scopeToken;

        lock (_scopeLock)
        {
            if (_disposed)
            {
                return;
            }

            scopeToken = _scope.Token;
        }

        _sound.Play(ct);
        using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct, scopeToken);
        CancellationToken displayToken = lifetime.Token;
        GenerationNotificationWindow? window = null;
        bool acquired = false;

        try
        {
            bool focused = false;
            await _dispatcher.InvokeAsync(() => focused = _focus.IsFocused, displayToken);

            if (focused)
            {
                return;
            }

            await _slots.WaitAsync(displayToken);
            acquired = true;
            await _dispatcher.InvokeAsync(() =>
            {
                if (_focus.IsFocused)
                {
                    return;
                }

                window = _createWindow();
                string status = _text.Get(notification.IsError
                    ? GenerationFailureMessageResolver.GetLocalizationKey(notification.FailureCode)
                    : NotificationLocalizationKeys.Completed);
                window.DataContext = new GenerationNotificationViewModel(
                    notification.Prompt, status, notification.IsError, window.RequestClose);
                window.NotificationClicked += OnNotificationClicked;
                window.HoverChanged += OnHoverChanged;
            }, displayToken);

            if (window is not null && await ShowWhenSpaceAvailableAsync(window, displayToken))
            {
                await WaitForDismissalAsync(window, displayToken);
            }
        }
        catch (OperationCanceledException) when (displayToken.IsCancellationRequested)
        {
        }
        finally
        {
            try
            {
                if (window is not null)
                {
                    await _dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            await window.CloseAnimatedAsync();
                        }
                        finally
                        {
                            _hover.SetHovered(window.NotificationId, false);
                            window.NotificationClicked -= OnNotificationClicked;
                            window.HoverChanged -= OnHoverChanged;
                            _windows.Remove(window);
                            ArrangeWindows();
                            TaskCompletionSource previous = _spaceAvailable;
                            _spaceAvailable = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                            previous.TrySetResult();
                        }
                    }, CancellationToken.None);
                }
            }
            finally
            {
                if (acquired)
                {
                    _slots.Release();
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_scopeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _scope.Cancel();
            _scope.Dispose();
        }
    }

    private void ArrangeWindows()
    {
        if (_windows.Count == 0 || _windows[0].Screens.Primary is not Screen screen)
        {
            return;
        }

        PixelRect area = screen.WorkingArea;
        double scaling = screen.Scaling;
        double bottom = area.Bottom - ScreenMargin * scaling;

        for (int index = _windows.Count - 1; index >= 0; index--)
        {
            GenerationNotificationWindow window = _windows[index];
            Size size = window.CardSize;
            window.MoveTo(new PixelPoint(
                (int)Math.Round(area.Right - (ScreenMargin + size.Width + window.CardMargin.Left) * scaling),
                (int)Math.Round(bottom - (size.Height + window.CardMargin.Top) * scaling)));
            bottom -= (size.Height + CardSpacing) * scaling;
        }
    }

    private async Task<bool> ShowWhenSpaceAvailableAsync(GenerationNotificationWindow window, CancellationToken ct)
    {
        while (true)
        {
            bool shown = false;
            bool focused = false;
            Task spaceAvailable = Task.CompletedTask;
            await _dispatcher.InvokeAsync(() =>
            {
                focused = _focus.IsFocused;

                if (focused)
                {
                    return;
                }

                Screen? screen = window.Screens.Primary;
                double availableHeight = screen is null ? double.PositiveInfinity
                    : screen.WorkingArea.Height / screen.Scaling - ScreenMargin * 2;
                double availableWidth = screen is null ? window.Width
                    : screen.WorkingArea.Width / screen.Scaling - ScreenMargin * 2 + window.CardMargin.Left + window.CardMargin.Right;
                window.PrepareLayout(availableWidth, availableHeight);

                if ((_windows.Count > 0)
                    && (_windows.Sum(visible => visible.CardSize.Height + CardSpacing) + window.CardSize.Height > availableHeight))
                {
                    spaceAvailable = _spaceAvailable.Task;
                    return;
                }

                _windows.Add(window);
                ArrangeWindows();
                window.ShowAnimated();
                _hover.SetHovered(window.NotificationId, window.IsHovered);
                shown = true;
            }, ct);

            if (focused || shown)
            {
                return shown;
            }

            await spaceAvailable.WaitAsync(ct);
        }
    }

    private async Task WaitForDismissalAsync(GenerationNotificationWindow window, CancellationToken ct)
    {
        while (true)
        {
            using CancellationTokenSource waitLifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                Task<long> expiration = _hover.WaitForExpirationAsync(waitLifetime.Token);
                Task completed = await Task.WhenAny(window.DismissalRequested, expiration).WaitAsync(ct);

                if (ReferenceEquals(completed, window.DismissalRequested))
                {
                    return;
                }

                long version = await expiration;
                Task? closing = null;
                await _dispatcher.InvokeAsync(() =>
                {
                    if (_hover.CanExpire(version))
                    {
                        closing = window.CloseAnimatedAsync();
                    }
                }, ct);

                if (closing is not null)
                {
                    await closing;
                    return;
                }
            }
            finally
            {
                waitLifetime.Cancel();
            }
        }
    }

    private void OnHoverChanged(object? sender, EventArgs e)
    {
        if (sender is GenerationNotificationWindow window)
        {
            _hover.SetHovered(window.NotificationId, window.IsHovered);
        }
    }

    private void OnNotificationClicked(object? sender, EventArgs e)
    {
        CancellationTokenSource previous;

        lock (_scopeLock)
        {
            if (_disposed)
            {
                return;
            }

            previous = _scope;
            _scope = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
        _windowState.ShowAndActivate();
    }
}
