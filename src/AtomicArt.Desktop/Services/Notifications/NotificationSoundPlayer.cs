using Microsoft.Extensions.Logging;

using Avalonia.Platform;
using NAudio.Wave;

namespace AtomicArt.Desktop.Services.Notifications;

public sealed class NotificationSoundPlayer : INotificationSoundPlayer, IDisposable
{
    private static readonly Uri DefaultSoundUri = new("avares://AtomicArt/Assets/Notification.mp3");
    private static readonly Lazy<byte[]> DefaultSound = new(LoadDefaultSound);

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _playbackGate = new(1, 1);
    private readonly INotificationSoundStore _store;
    private readonly ILogger<NotificationSoundPlayer> _logger;
    private CancellationTokenSource? _playback;
    private bool _disposed;

    public NotificationSoundPlayer(INotificationSoundStore store, ILogger<NotificationSoundPlayer> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Play(CancellationToken ct)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _playback?.Cancel();
            _playback?.Dispose();
            _playback = CancellationTokenSource.CreateLinkedTokenSource(ct);
            CancellationToken playbackToken = _playback.Token;
            _ = Task.Run(() => PlayAsync(playbackToken), CancellationToken.None);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _disposed = true;
            _playback?.Cancel();
            _playback?.Dispose();
            _playback = null;
        }
    }

    private static byte[] LoadDefaultSound()
    {
        using Stream source = AssetLoader.Open(DefaultSoundUri);
        using MemoryStream content = new();
        source.CopyTo(content);

        return content.ToArray();
    }

    private static async Task PlayContentAsync(byte[] content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using WaveStream reader = NotificationSoundDecoder.Open(content);
        using WaveOutEvent output = new();
        TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, _) => stopped.TrySetResult();
        output.Init(reader.ToSampleProvider().ToWaveProvider16());
        ct.ThrowIfCancellationRequested();
        output.Play();

        try
        {
            await Task.WhenAny(stopped.Task, Task.Delay(GenerationNotification.DisplayDuration, ct)).ConfigureAwait(false);
        }
        finally
        {
            output.Stop();
        }
    }

    private async Task PlayAsync(CancellationToken ct)
    {
        bool acquired = false;

        try
        {
            await _playbackGate.WaitAsync(ct).ConfigureAwait(false);
            acquired = true;
            await PlaySelectedSoundAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not play the notification sound.");
        }
        finally
        {
            if (acquired)
            {
                _playbackGate.Release();
            }
        }
    }

    private async Task PlaySelectedSoundAsync(CancellationToken ct)
    {
        try
        {
            byte[]? customSound = await _store.ReadAsync(ct).ConfigureAwait(false);

            if (customSound is not null)
            {
                await PlayContentAsync(customSound, ct).ConfigureAwait(false);
                return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not play the custom notification sound.");
        }

        ct.ThrowIfCancellationRequested();
        await PlayContentAsync(DefaultSound.Value, ct).ConfigureAwait(false);
    }
}
