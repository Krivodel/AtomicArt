using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationRunDispatcher : IGenerationRunDispatcher, IGenerationModelService, IDisposable
{
    private readonly IGenerationConcurrencyLimiter _concurrencyLimiter;
    private readonly IImageGenerationApiClient _apiClient;
    private readonly NanoBanana2GenerationLifecyclePublisher _lifecyclePublisher;
    private readonly IGenerationActivityTracker _generationActivityTracker;
    private readonly ILogger<GenerationRunDispatcher> _logger;
    private readonly object _syncRoot = new();
    private readonly HashSet<GenerationRunLifetime> _activeRunLifetimes = [];
    private bool _isDisposed;

    public GenerationRunDispatcher(
        IGenerationConcurrencyLimiter concurrencyLimiter,
        IImageGenerationApiClient apiClient,
        NanoBanana2GenerationLifecyclePublisher lifecyclePublisher,
        IGenerationResultStorage generationResultStorage,
        IGenerationActivityTracker generationActivityTracker,
        ILogger<GenerationRunDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(concurrencyLimiter);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(lifecyclePublisher);
        ArgumentNullException.ThrowIfNull(generationResultStorage);
        ArgumentNullException.ThrowIfNull(generationActivityTracker);
        ArgumentNullException.ThrowIfNull(logger);

        _concurrencyLimiter = concurrencyLimiter;
        _apiClient = apiClient;
        _lifecyclePublisher = lifecyclePublisher;
        _generationActivityTracker = generationActivityTracker;
        _logger = logger;
    }

    public Task EnqueueAsync(GenerationRunRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        Guid correlationId = Guid.NewGuid();
        GenerationRunState runState = new();
        ImageGenerationRequestDto generationRequest = request.Request;
        string providerCredential = request.ProviderCredential;
        GenerationRunLifetime runLifetime = CreateRunLifetime();
        _generationActivityTracker.Start(
            correlationId,
            GenerationActivityPhase.GenerationRequest);

        try
        {
            _logger.LogInformation(
                "Generation run {CorrelationId} was accepted with {GenerationCount} requested results and {AttachmentCount} attachments.",
                correlationId,
                generationRequest.GenerationCount,
                generationRequest.AttachedImages.Count);
            _lifecyclePublisher.PublishStartRequested(correlationId);
            _lifecyclePublisher.PublishStarted(correlationId, request.StartSnapshot);
            runState.MarkStarted();

            _ = Task.Run(
                () => ExecuteRunAsync(
                    correlationId,
                    generationRequest,
                    providerCredential,
                    runState,
                    runLifetime),
                CancellationToken.None);
        }
        catch
        {
            CompleteRunLifetime(correlationId, runLifetime);

            throw;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IReadOnlyList<GenerationRunLifetime> activeRunLifetimes;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            activeRunLifetimes = _activeRunLifetimes.ToList();
        }

        foreach (GenerationRunLifetime runLifetime in activeRunLifetimes)
        {
            runLifetime.Cancel();
        }

        _logger.LogInformation(
            "Generation run dispatcher disposed and canceled {ActiveRunCount} active runs.",
            activeRunLifetimes.Count);
    }

    private async Task ExecuteRunAsync(
        Guid correlationId,
        ImageGenerationRequestDto request,
        string providerCredential,
        GenerationRunState runState,
        GenerationRunLifetime runLifetime)
    {
        bool limiterAcquired = false;
        CancellationToken ct = runLifetime.Token;

        try
        {
            ct.ThrowIfCancellationRequested();

            _logger.LogDebug(
                "Generation run {CorrelationId} is waiting for the concurrency limiter.",
                correlationId);
            await _concurrencyLimiter.WaitAsync(ct).ConfigureAwait(false);
            limiterAcquired = true;
            _logger.LogInformation(
                "Generation run {CorrelationId} started its API request.",
                correlationId);

            GenerationBatchDto batch = await _apiClient
                .CreateGenerationAsync(request, providerCredential, ct)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Generation run {CorrelationId} completed as batch {BatchId} with {ItemCount} items.",
                correlationId,
                batch.BatchId,
                batch.Items.Count);
            _lifecyclePublisher.PublishCompleted(correlationId, batch);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Generation run {CorrelationId} was canceled.",
                correlationId);
            _lifecyclePublisher.PublishCanceledGeneration(correlationId, runState);
        }
        catch (Exception ex)
        {
            LogBackgroundFailure(ex, correlationId);
            _lifecyclePublisher.PublishFailed(correlationId, UiStrings.GenerationFailed);
        }
        finally
        {
            if (limiterAcquired)
            {
                _concurrencyLimiter.Release();
            }

            providerCredential = string.Empty;
            CompleteRunLifetime(correlationId, runLifetime);
        }
    }

    private GenerationRunLifetime CreateRunLifetime()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            GenerationRunLifetime runLifetime = new();
            _activeRunLifetimes.Add(runLifetime);

            return runLifetime;
        }
    }

    private void CompleteRunLifetime(
        Guid correlationId,
        GenerationRunLifetime runLifetime)
    {
        lock (_syncRoot)
        {
            _activeRunLifetimes.Remove(runLifetime);
        }

        _generationActivityTracker.Complete(
            correlationId,
            GenerationActivityPhase.GenerationRequest);
        runLifetime.Dispose();
    }

    private void LogBackgroundFailure(Exception exception, Guid correlationId)
    {
        _logger.LogWarning(
            exception,
            "Background generation run {CorrelationId} failed",
            correlationId);
    }

    private sealed class GenerationRunLifetime : IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _isDisposed;

        public CancellationToken Token => _cancellationTokenSource.Token;

        public void Cancel()
        {
            lock (_syncRoot)
            {
                if (_isDisposed)
                {
                    return;
                }

                _cancellationTokenSource.Cancel();
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _cancellationTokenSource.Dispose();
            }
        }
    }
}
