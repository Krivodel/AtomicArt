using Microsoft.Extensions.Logging;

using SkiaSharp;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Dlss5;

public sealed class Dlss5RenderScheduler : IDlss5RenderScheduler, IDisposable
{
    private const int WorkSignalInitialCount = 0;
    private const int WorkSignalMaximumCount = 1;

    private readonly IDlss5NativeEngine _engine;
    private readonly ILogger<Dlss5RenderScheduler> _logger;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _workSignal = new(WorkSignalInitialCount, WorkSignalMaximumCount);
    private readonly CancellationTokenSource _disposeSource = new();
    private readonly Task _worker;
    private Dlss5RenderWork? _pendingWork;
    private CancellationTokenSource? _activeWorkCancellation;
    private long _latestRevision;
    private bool _isDisposed;

    public Dlss5RenderScheduler(
        IDlss5NativeEngine engine,
        IDataRootAccessCoordinator accessCoordinator,
        ILogger<Dlss5RenderScheduler> logger)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _accessCoordinator = accessCoordinator ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _worker = RunAsync(_disposeSource.Token);
    }

    public void Request(
        SKBitmap source,
        Dlss5RenderSettings settings,
        long revision,
        Func<long, Dlss5NativeRenderResult, Task> publishResult,
        Func<Exception, Task> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(publishResult);
        ArgumentNullException.ThrowIfNull(reportFailure);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        Interlocked.Exchange(ref _latestRevision, revision);
        SKImage sourceSnapshot = Dlss5BitmapSnapshot.CreateImage(source);
        Dlss5RenderWork work = new(
            sourceSnapshot,
            settings,
            revision,
            publishResult,
            reportFailure);

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                work.Dispose();
                throw new ObjectDisposedException(nameof(Dlss5RenderScheduler));
            }

            _activeWorkCancellation?.Cancel();
            bool shouldSignal = _pendingWork is null;
            _pendingWork?.Dispose();
            _pendingWork = work;
            if (shouldSignal)
            {
                _workSignal.Release();
            }
        }
    }

    public void Cancel()
    {
        Interlocked.Increment(ref _latestRevision);
        lock (_syncRoot)
        {
            _activeWorkCancellation?.Cancel();
            _pendingWork?.Dispose();
            _pendingWork = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        lock (_syncRoot)
        {
            _activeWorkCancellation?.Cancel();
            _pendingWork?.Dispose();
            _pendingWork = null;
        }

        _disposeSource.Cancel();
        _disposeSource.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await _workSignal.WaitAsync(ct).ConfigureAwait(false);
                Dlss5RenderWork? work;

                lock (_syncRoot)
                {
                    work = _pendingWork;
                    _pendingWork = null;
                }

                if (work is null)
                {
                    continue;
                }

                using (work)
                {
                    using CancellationTokenSource workCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(ct);
                    lock (_syncRoot)
                    {
                        _activeWorkCancellation = workCancellation;
                    }

                    try
                    {
                        using DataRootAccessLease accessLease = await _accessCoordinator
                            .AcquireAccessAsync(workCancellation.Token)
                            .ConfigureAwait(false);
                        using SKBitmap source = work.CreateBitmap();
                        Dlss5NativeRenderResult result = await _engine
                            .RenderAsync(source, work.Settings, workCancellation.Token)
                            .ConfigureAwait(false);

                        _logger.LogDebug(
                            "DLSS 5 render revision {RenderRevision} timings: upload {UploadMilliseconds} ms, evaluate {EvaluateMilliseconds} ms, download {DownloadMilliseconds} ms.",
                            work.Revision,
                            result.UploadDuration.TotalMilliseconds,
                            result.EvaluateDuration.TotalMilliseconds,
                            result.DownloadDuration.TotalMilliseconds);

                        if (work.Revision != Volatile.Read(ref _latestRevision))
                        {
                            result.Bitmap.Dispose();
                            continue;
                        }

                        await work.PublishResult(work.Revision, result).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        // A newer slider value superseded this render. The worker
                        // process is cancelled and its stale result is discarded.
                    }
                    catch (Exception) when ((workCancellation.IsCancellationRequested) && (!ct.IsCancellationRequested))
                    {
                        // A pipe can report EOF instead of OperationCanceledException
                        // after the superseded worker is terminated.
                    }
                    catch (Exception exception)
                    {
                        if (work.Revision == Volatile.Read(ref _latestRevision))
                        {
                            await work.ReportFailure(exception).ConfigureAwait(false);
                        }

                        _logger.LogError(exception, "DLSS 5 render revision {RenderRevision} failed.", work.Revision);
                    }
                    finally
                    {
                        lock (_syncRoot)
                        {
                            if (ReferenceEquals(_activeWorkCancellation, workCancellation))
                            {
                                _activeWorkCancellation = null;
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private sealed class Dlss5RenderWork : IDisposable
    {
        public SKImage Source { get; }
        public Dlss5RenderSettings Settings { get; }
        public long Revision { get; }
        public Func<long, Dlss5NativeRenderResult, Task> PublishResult { get; }
        public Func<Exception, Task> ReportFailure { get; }

        public Dlss5RenderWork(
            SKImage source,
            Dlss5RenderSettings settings,
            long revision,
            Func<long, Dlss5NativeRenderResult, Task> publishResult,
            Func<Exception, Task> reportFailure)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Revision = revision;
            PublishResult = publishResult ?? throw new ArgumentNullException(nameof(publishResult));
            ReportFailure = reportFailure ?? throw new ArgumentNullException(nameof(reportFailure));
        }

        public SKBitmap CreateBitmap()
        {
            return Dlss5BitmapSnapshot.CreateBitmap(Source);
        }

        public void Dispose()
        {
            Source.Dispose();
        }
    }
}
