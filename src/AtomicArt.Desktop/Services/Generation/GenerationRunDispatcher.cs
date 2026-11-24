using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationRunDispatcher : IGenerationRunDispatcher, IGenerationModelService, IDisposable
{
    private readonly IGenerationConcurrencyLimiter _concurrencyLimiter;
    private readonly IImageGenerationApiClient _apiClient;
    private readonly LocalCodexGenerationClient? _localCodex;
    private readonly NanoBanana2GenerationLifecyclePublisher _lifecyclePublisher;
    private readonly IGenerationActivityTracker _generationActivityTracker;
    private readonly IGenerationAdmissionGate _generationAdmissionGate;
    private readonly ILogger<GenerationRunDispatcher> _logger;
    private readonly IGenerationCancellationService _cancellationService;
    private readonly IGenerationReferenceStore _referenceStore;
    private readonly SaveReferencesSetting _saveReferences;
    private readonly int _maxAutomaticRetries;
    private readonly object _syncRoot = new();
    private readonly HashSet<GenerationRunLifetime> _activeRunLifetimes = [];
    private bool _isDisposed;

    public GenerationRunDispatcher(
        IGenerationConcurrencyLimiter concurrencyLimiter,
        IImageGenerationApiClient apiClient,
        NanoBanana2GenerationLifecyclePublisher lifecyclePublisher,
        IGenerationResultStorage generationResultStorage,
        IGenerationActivityTracker generationActivityTracker,
        IGenerationAdmissionGate generationAdmissionGate,
        ILogger<GenerationRunDispatcher> logger,
        IOptions<GenerationClientOptions> options,
        IGenerationReferenceStore referenceStore,
        SaveReferencesSetting saveReferences,
        IGenerationCancellationService? cancellationService = null,
        LocalCodexGenerationClient? localCodex = null)
    {
        ArgumentNullException.ThrowIfNull(concurrencyLimiter);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(lifecyclePublisher);
        ArgumentNullException.ThrowIfNull(generationResultStorage);
        ArgumentNullException.ThrowIfNull(generationActivityTracker);
        ArgumentNullException.ThrowIfNull(generationAdmissionGate);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _concurrencyLimiter = concurrencyLimiter;
        _apiClient = apiClient;
        _localCodex = localCodex;
        _lifecyclePublisher = lifecyclePublisher;
        _generationActivityTracker = generationActivityTracker;
        _generationAdmissionGate = generationAdmissionGate;
        _logger = logger;
        _cancellationService = cancellationService
            ?? NullGenerationCancellationService.Instance;
        _referenceStore = referenceStore ?? throw new ArgumentNullException(nameof(referenceStore));
        _saveReferences = saveReferences ?? throw new ArgumentNullException(nameof(saveReferences));
        _maxAutomaticRetries = options.Value.MaxAutomaticRetries;
    }

    public async Task EnqueueAsync(GenerationRunRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        GenerationAdmissionLease admissionLease =
            await _generationAdmissionGate.EnterAsync(ct).ConfigureAwait(false);
        Guid correlationId = request.ChatRun?.Id ?? Guid.NewGuid();
        GenerationRunState runState = new();
        ImageGenerationRequestDto generationRequest = request.Request;
        string providerCredential = request.ProviderCredential;
        GenerationRunLifetime runLifetime = CreateRunLifetime();
        _cancellationService.Register(correlationId, runLifetime.Cancel);
        _generationActivityTracker.Start(
            correlationId,
            GenerationActivityPhase.GenerationRequest);

        try
        {
            IReadOnlyList<GenerationReference>? references = request.ChatReferences;

            if (references is null && (_saveReferences.CurrentValue || LocalCodexImageModel.Matches(generationRequest.ModelId)
                || generationRequest.AttachedImages.Count == 0))
            {
                references = await _referenceStore
                    .SaveAsync(correlationId, generationRequest.AttachedImages, ct)
                    .ConfigureAwait(false);
            }

            if (request.StartSnapshot.ChatHistory is { } history)
            {
                await _referenceStore.RetainAsync(correlationId,
                    history.Messages.SelectMany(message => message.Images).Concat(references ?? Array.Empty<GenerationReference>()), ct)
                    .ConfigureAwait(false);
            }

            GenerationStartSnapshot start = request.StartSnapshot with { References = references };

            _logger.LogInformation(
                "Generation run {CorrelationId} was accepted with {GenerationCount} requested results and {AttachmentCount} attachments.",
                correlationId,
                generationRequest.GenerationCount,
                generationRequest.AttachedImages.Count);
            _lifecyclePublisher.PublishStartRequested(correlationId);
            _lifecyclePublisher.PublishStarted(correlationId, start);
            runState.MarkStarted();

            _ = Task.Run(
                () => ExecuteRunAsync(
                    correlationId,
                    generationRequest,
                    providerCredential,
                    request,
                    runState,
                    runLifetime,
                    admissionLease),
                CancellationToken.None);
        }
        catch
        {
            _referenceStore.Release(correlationId);
            admissionLease.Dispose();
            CompleteRunLifetime(correlationId, runLifetime);

            throw;
        }
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
        GenerationRunRequest runRequest,
        GenerationRunState runState,
        GenerationRunLifetime runLifetime,
        GenerationAdmissionLease admissionLease)
    {
        bool limiterAcquired = false;
        bool completionPublished = false;
        GenerationChatResult? chatResult = null;
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
                "Generation run {CorrelationId} started its request.",
                correlationId);

            GenerationBatchDto batch;

            if (LocalCodexImageModel.Matches(request.ModelId))
            {
                LocalCodexGenerationClient client = _localCodex ?? throw new GenerationAttemptException(
                    "Local Codex is unavailable.", GenerationClientFailureCodes.CodexUnavailable, false);
                (batch, chatResult) = await client.CreateGenerationAsync(
                    request, correlationId, runRequest.CodexTurnOptions, runRequest.StartSnapshot.ChatHistory, ct)
                    .ConfigureAwait(false);
            }
            else
            {
                batch = await ExecuteAttemptsAsync(correlationId, request, providerCredential, ct)
                    .ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Generation run {CorrelationId} completed as batch {BatchId} with {ItemCount} items.",
                correlationId,
                batch.BatchId,
                batch.Items.Count);
            _lifecyclePublisher.PublishCompleted(correlationId, batch, chatResult, runRequest.ChatRun);
            completionPublished = true;
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
            string failureCode =
                GenerationFailureCodeResolver.GetFailureCode(ex);
            _lifecyclePublisher.PublishFailed(correlationId, failureCode);
        }
        finally
        {
            if (limiterAcquired)
            {
                _concurrencyLimiter.Release();
            }

            providerCredential = string.Empty;

            if (!completionPublished)
            {
                if (chatResult?.StorageLeaseId is Guid storageLeaseId)
                {
                    _referenceStore.Release(storageLeaseId);
                }

                runRequest.ChatRun?.Complete(null);
            }

            CompleteRunLifetime(correlationId, runLifetime);
            admissionLease.Dispose();
        }
    }

    private async Task<GenerationBatchDto> ExecuteAttemptsAsync(
        Guid logicalGenerationId,
        ImageGenerationRequestDto request,
        string providerCredential,
        CancellationToken ct)
    {
        int maximumAttemptNumber = checked(_maxAutomaticRetries + 1);

        for (int attemptNumber = 1;
                     attemptNumber <= maximumAttemptNumber;
                     attemptNumber++)
        {
            try
            {
                return await _apiClient
                    .CreateGenerationAsync(
                        request,
                        logicalGenerationId,
                        attemptNumber,
                        providerCredential,
                        ct)
                    .ConfigureAwait(false);
            }
            catch (GenerationAttemptException exception)
                when (exception.Retryable
                    && attemptNumber < maximumAttemptNumber)
            {
                _logger.LogWarning(
                    exception,
                    "Generation attempt {AttemptNumber} for logical generation {LogicalGenerationId} failed transiently and will be retried immediately. Safe error code {SafeErrorCode}.",
                    attemptNumber,
                    logicalGenerationId,
                    exception.SafeErrorCode);
            }
        }

        throw new InvalidOperationException(
            "Generation retry loop completed without a terminal result.");
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
        _cancellationService.Unregister(correlationId);
        runLifetime.Dispose();
    }

    private void LogBackgroundFailure(Exception exception, Guid correlationId)
    {
        if (exception is GenerationAttemptException attemptException)
        {
            _logger.LogWarning(
                attemptException,
                "Background generation run {CorrelationId} failed with safe error code {SafeErrorCode}; retryable {Retryable}.",
                correlationId,
                attemptException.SafeErrorCode,
                attemptException.Retryable);

            return;
        }

        _logger.LogWarning(
            exception,
            "Background generation run {CorrelationId} failed",
            correlationId);
    }

    private sealed class GenerationRunLifetime : IDisposable
    {
        public CancellationToken Token => _cancellationTokenSource.Token;

        private readonly object _syncRoot = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _isDisposed;

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
