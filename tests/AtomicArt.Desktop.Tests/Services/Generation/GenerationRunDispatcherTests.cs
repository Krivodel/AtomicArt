using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;
using TestGenerationCredentials = AtomicArt.Tests.Common.Generation.TestGenerationCredentials;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationRunDispatcherTests
{
    private const string ModelId = "test-model";
    private const int ConcurrentDisposeAttemptCount = 50;
    private const int OverLimitRunCount = GenerationConcurrencyLimiter.MaxConcurrentGenerations + 6;
    private static readonly DateTime RequestedAtUtc = new(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 4, 12, 0, 1, DateTimeKind.Utc);

    [Fact]
    public async Task EnqueueAsync_WithDelayedApi_ReturnsBeforeGenerationCompletes()
    {
        BlockingRunTestContext context = CreateBlockingRunContext();
        GenerationRunRequest request = CreateRunRequest();

        Task enqueueTask = context.Dispatcher.EnqueueAsync(request, CancellationToken.None);
        await enqueueTask;

        enqueueTask.IsCompletedSuccessfully.Should().BeTrue();
        context.LifecycleEventHub.PublishedEvents
            .Select(lifecycleEvent => lifecycleEvent.Status)
            .Take(2)
            .Should()
            .Equal(
                GenerationLifecycleStatus.StartRequested,
                GenerationLifecycleStatus.Started);
        context.LifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed);

        await context.ApiClient.WaitForCallCountAsync(1);
        await CompleteRunAsync(context);
    }

    [Fact]
    public async Task EnqueueAsync_WithRequest_UsesProvidedRequestAndCredential()
    {
        BlockingRunTestContext context = CreateBlockingRunContext();
        GenerationRunRequest runRequest = CreateRunRequest();

        await context.Dispatcher.EnqueueAsync(runRequest, CancellationToken.None);
        await context.ApiClient.WaitForCallCountAsync(1);

        context.ApiClient.CapturedRequests.Should().ContainSingle(request =>
            ReferenceEquals(request, runRequest.Request));
        context.ApiClient.CapturedProviderCredentials.Should().ContainSingle()
            .Which.Should().Be(TestGenerationCredentials.ProviderCredential);

        await CompleteRunAsync(context);
    }

    [Fact]
    public async Task EnqueueAsync_WhenApiThrows_PublishesFailedEvent()
    {
        RunTestContext context = CreateRunContext(new ThrowingImageGenerationApiClient());

        await EnqueueAndWaitForStatusAsync(
            context,
            GenerationLifecycleStatus.Failed);

        List<GenerationLifecycleEvent> events = context.LifecycleEventHub.PublishedEvents.ToList();
        GenerationLifecycleEvent failedEvent = events
            .Single(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Failed);
        GenerationLifecycleEvent startedEvent = events
            .Single(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Started);
        int startedIndex = events.IndexOf(startedEvent);
        int failedIndex = events.IndexOf(failedEvent);
        startedIndex.Should().BeLessThan(failedIndex);
        failedEvent.CorrelationId.Should().Be(startedEvent.CorrelationId);
        failedEvent.ErrorMessage.Should().Be(UiStrings.GenerationFailed);
    }

    [Fact]
    public async Task EnqueueAsync_WithFourRetryableFailuresThenSuccess_CompletesOneLogicalGeneration()
    {
        RetrySequenceImageGenerationApiClient apiClient = new(
            retryableFailureCount: 4,
            succeedAfterFailures: true);
        RunTestContext context = CreateRunContext(apiClient);

        await EnqueueAndWaitForStatusAsync(
            context,
            GenerationLifecycleStatus.Completed);

        apiClient.AttemptNumbers.Should().Equal(1, 2, 3, 4, 5);
        apiClient.LogicalGenerationIds.Distinct().Should().ContainSingle();
        context.LifecycleEventHub.PublishedEvents
            .Count(lifecycleEvent =>
                lifecycleEvent.Status == GenerationLifecycleStatus.Started)
            .Should()
            .Be(1);
        context.LifecycleEventHub.PublishedEvents
            .Count(lifecycleEvent =>
                lifecycleEvent.Status == GenerationLifecycleStatus.Completed)
            .Should()
            .Be(1);
        context.LifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent =>
                lifecycleEvent.Status == GenerationLifecycleStatus.Failed);
    }

    [Fact]
    public async Task EnqueueAsync_WithFiveRetryableFailures_PublishesOneFinalFailure()
    {
        RetrySequenceImageGenerationApiClient apiClient = new(
            retryableFailureCount: 5,
            succeedAfterFailures: false);
        RunTestContext context = CreateRunContext(apiClient);

        await EnqueueAndWaitForStatusAsync(
            context,
            GenerationLifecycleStatus.Failed);

        apiClient.AttemptNumbers.Should().Equal(1, 2, 3, 4, 5);
        context.LifecycleEventHub.PublishedEvents
            .Count(lifecycleEvent =>
                lifecycleEvent.Status == GenerationLifecycleStatus.Started)
            .Should()
            .Be(1);
        context.LifecycleEventHub.PublishedEvents
            .Count(lifecycleEvent =>
                lifecycleEvent.Status == GenerationLifecycleStatus.Failed)
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_WithNonRetryableFailure_DoesNotRetry()
    {
        RetrySequenceImageGenerationApiClient apiClient = new(
            retryableFailureCount: 1,
            succeedAfterFailures: false,
            retryable: false);
        RunTestContext context = CreateRunContext(apiClient);

        await EnqueueAndWaitForStatusAsync(
            context,
            GenerationLifecycleStatus.Failed);

        apiClient.AttemptNumbers.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_WhenCommandTokenCanceledAfterAcceptedRun_KeepsBackgroundGeneration()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        BlockingRunTestContext context = await CreateStartedBlockingRunContextAsync(
            cancellationTokenSource.Token);

        CancellationToken runToken = context.ApiClient.CapturedCancellationTokens.Should().ContainSingle()
            .Which;
        runToken.CanBeCanceled.Should().BeTrue();

        await cancellationTokenSource.CancelAsync();
        runToken.IsCancellationRequested.Should().BeFalse();
        await CompleteRunAsync(context);

        AssertStatusesNotPublished(
            context.LifecycleEventHub,
            GenerationLifecycleStatus.StartFailed,
            GenerationLifecycleStatus.Failed);
        context.ApiClient.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_WithMoreThanLimit_LimitsConcurrentApiCalls()
    {
        BlockingRunTestContext context = CreateBlockingRunContext();
        IEnumerable<GenerationRunDispatcher> dispatchers = Enumerable.Repeat(
            context.Dispatcher,
            OverLimitRunCount);

        await EnqueueAndAssertConcurrencyLimitAsync(context, dispatchers);
    }

    [Fact]
    public async Task EnqueueAsync_WithSharedLimiterAcrossDispatchers_LimitsConcurrentApiCalls()
    {
        GenerationConcurrencyLimiter limiter = new();
        BlockingRunTestContext context = CreateBlockingRunContext(limiter);
        GenerationRunDispatcher secondDispatcher = CreateDispatcher(
            context.ApiClient,
            context.LifecycleEventHub,
            limiter);
        IEnumerable<GenerationRunDispatcher> dispatchers = Enumerable
            .Range(0, OverLimitRunCount)
            .Select(index => (index % 2) == 0
                ? context.Dispatcher
                : secondDispatcher);

        await EnqueueAndAssertConcurrencyLimitAsync(context, dispatchers);
    }

    [Fact]
    public async Task EnqueueAsync_WithTwoRuns_PublishesSeparateCorrelationIds()
    {
        RunTestContext context = CreateRunContext(new SuccessfulImageGenerationApiClient());

        await context.Dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        await context.Dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);

        await WaitForStatusCountAsync(
            context.LifecycleEventHub,
            GenerationLifecycleStatus.Completed,
            2);

        AssertUniqueStartedCorrelationIds(context.LifecycleEventHub, 2);
    }

    [Fact]
    public async Task EnqueueAsync_WithOverlappingRunsAndCanceledFirstCommandToken_DoesNotPublishStartFailed()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        BlockingRunTestContext context = CreateBlockingRunContext();

        await context.Dispatcher.EnqueueAsync(CreateRunRequest(), cancellationTokenSource.Token);
        await context.Dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        await context.ApiClient.WaitForCallCountAsync(2);

        await cancellationTokenSource.CancelAsync();
        context.ApiClient.Complete();

        await WaitForStatusCountAsync(
            context.LifecycleEventHub,
            GenerationLifecycleStatus.Completed,
            2);

        AssertStatusesNotPublished(
            context.LifecycleEventHub,
            GenerationLifecycleStatus.StartFailed);
        AssertUniqueStartedCorrelationIds(context.LifecycleEventHub, 2);
        context.ApiClient.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task Dispose_WithAcceptedStartedRun_PublishesFailedWithoutStartFailed()
    {
        BlockingRunTestContext context = await CreateStartedBlockingRunContextAsync(
            CancellationToken.None);
        CancellationToken runToken = context.ApiClient.CapturedCancellationTokens.Should().ContainSingle()
            .Which;

        await DisposeAndWaitForFailureAsync(context);

        runToken.IsCancellationRequested.Should().BeTrue();
        AssertStatusesNotPublished(
            context.LifecycleEventHub,
            GenerationLifecycleStatus.StartFailed,
            GenerationLifecycleStatus.Completed);
        context.ApiClient.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task Dispose_WithAcceptedRunBeforeApiStarts_PublishesFailedWithoutStartFailed()
    {
        ManualGenerationConcurrencyLimiter limiter = new();
        BlockingRunTestContext context = CreateBlockingRunContext(limiter);

        await context.Dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        context.Dispatcher.Dispose();
        limiter.ReleaseWait();

        await WaitForStatusAsync(
            context.LifecycleEventHub,
            GenerationLifecycleStatus.Failed);

        context.ApiClient.CapturedRequests.Should().BeEmpty();
        AssertStatusesNotPublished(
            context.LifecycleEventHub,
            GenerationLifecycleStatus.StartFailed,
            GenerationLifecycleStatus.Completed);
        limiter.ReleaseCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        RunTestContext context = CreateRunContext(new SuccessfulImageGenerationApiClient());

        Action act = () =>
        {
            context.Dispatcher.Dispose();
            context.Dispatcher.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_AfterCompletedRun_DoesNotThrow()
    {
        RunTestContext context = CreateRunContext(new SuccessfulImageGenerationApiClient());

        await EnqueueAndWaitForStatusAsync(
            context,
            GenerationLifecycleStatus.Completed);

        AssertDisposeDoesNotThrow(context.Dispatcher);
    }

    [Fact]
    public async Task Dispose_AfterCanceledRun_DoesNotThrow()
    {
        BlockingRunTestContext context = await CreateStartedBlockingRunContextAsync(
            CancellationToken.None);

        await DisposeAndWaitForFailureAsync(context);

        AssertDisposeDoesNotThrow(context.Dispatcher);
    }

    [Fact]
    public async Task Dispose_WhenRunCompletesConcurrently_DoesNotThrow()
    {
        for (int i = 0; i < ConcurrentDisposeAttemptCount; i++)
        {
            BlockingRunTestContext context = await CreateStartedBlockingRunContextAsync(
                CancellationToken.None);

            Task disposeTask = Task.Run(context.Dispatcher.Dispose);
            context.ApiClient.Complete();
            Func<Task> act = async () => await disposeTask.ConfigureAwait(false);

            await act.Should().NotThrowAsync();
            await AsyncTestWaiter.WaitForConditionAsync(
                () => context.LifecycleEventHub.PublishedEvents.Any(
                    lifecycleEvent => (lifecycleEvent.Status == GenerationLifecycleStatus.Completed)
                        || (lifecycleEvent.Status == GenerationLifecycleStatus.Failed)),
                CancellationToken.None);
            context.ApiClient.ActiveCount.Should().Be(0);
        }
    }

    private static RunTestContext CreateRunContext(
        IImageGenerationApiClient apiClient,
        IGenerationConcurrencyLimiter? limiter = null)
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(
            apiClient,
            lifecycleEventHub,
            limiter);

        return new RunTestContext(lifecycleEventHub, dispatcher);
    }

    private static BlockingRunTestContext CreateBlockingRunContext(
        IGenerationConcurrencyLimiter? limiter = null)
    {
        BlockingImageGenerationApiClient apiClient = new();
        RunTestContext context = CreateRunContext(apiClient, limiter);

        return new BlockingRunTestContext(
            apiClient,
            context.LifecycleEventHub,
            context.Dispatcher);
    }

    private static GenerationRunDispatcher CreateDispatcher(
        IImageGenerationApiClient apiClient,
        IGenerationLifecycleEventHub lifecycleEventHub,
        IGenerationConcurrencyLimiter? limiter = null)
    {
        return GenerationRunDispatcherTestFactory.Create(
            apiClient,
            lifecycleEventHub,
            limiter);
    }

    private static GenerationRunRequest CreateRunRequest()
    {
        ImageGenerationRequestDto request = ImageGenerationRequestDtoTestFactory.Create(
            modelId: ModelId,
            aspectRatio: "1:1",
            resolution: "1k");
        GenerationStartSnapshot startSnapshot = new(
            request.ModelId,
            "Test Model",
            request.Prompt,
            request.AspectRatio,
            request.Resolution,
            request.GenerationCount,
            request.AttachedImages.Count,
            RequestedAtUtc);

        return new GenerationRunRequest(
            request,
            startSnapshot,
            TestGenerationCredentials.ProviderCredential);
    }

    private static GenerationBatchDto CreateBatch(ImageGenerationRequestDto request)
    {
        GenerationItemDto item = GenerationItemDtoTestFactory.Create(
            modelId: request.ModelId,
            modelDisplayName: "Test Model",
            prompt: request.Prompt,
            aspectRatio: request.AspectRatio,
            resolution: request.Resolution,
            createdAtUtc: CreatedAtUtc);
        GenerationItemDto[] items = [item];

        return new GenerationBatchDto(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            items);
    }

    private static async Task<BlockingRunTestContext> CreateStartedBlockingRunContextAsync(
        CancellationToken ct)
    {
        BlockingRunTestContext context = CreateBlockingRunContext();

        await context.Dispatcher
            .EnqueueAsync(CreateRunRequest(), ct)
            .ConfigureAwait(false);
        await context.ApiClient
            .WaitForCallCountAsync(1)
            .ConfigureAwait(false);

        return context;
    }

    private static void AssertStatusesNotPublished(
        TestGenerationLifecycleEventHub lifecycleEventHub,
        params GenerationLifecycleStatus[] statuses)
    {
        foreach (GenerationLifecycleStatus status in statuses)
        {
            lifecycleEventHub.PublishedEvents
                .Should()
                .NotContain(lifecycleEvent => lifecycleEvent.Status == status);
        }
    }

    private static void AssertUniqueStartedCorrelationIds(
        TestGenerationLifecycleEventHub lifecycleEventHub,
        int expectedCount)
    {
        lifecycleEventHub.PublishedEvents
            .Where(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Started)
            .Select(lifecycleEvent => lifecycleEvent.CorrelationId)
            .Should()
            .OnlyHaveUniqueItems()
            .And.HaveCount(expectedCount);
    }

    private static void AssertDisposeDoesNotThrow(GenerationRunDispatcher dispatcher)
    {
        Action act = dispatcher.Dispose;

        act.Should().NotThrow();
    }

    private static async Task EnqueueAndWaitForStatusAsync(
        RunTestContext context,
        GenerationLifecycleStatus status)
    {
        await context.Dispatcher
            .EnqueueAsync(CreateRunRequest(), CancellationToken.None)
            .ConfigureAwait(false);
        await WaitForStatusAsync(context.LifecycleEventHub, status).ConfigureAwait(false);
    }

    private static async Task EnqueueAndAssertConcurrencyLimitAsync(
        BlockingRunTestContext context,
        IEnumerable<GenerationRunDispatcher> dispatchers)
    {
        await EnqueueRunsAsync(dispatchers).ConfigureAwait(false);
        await CompleteRunsAndAssertConcurrencyLimitAsync(
            context.ApiClient,
            context.LifecycleEventHub).ConfigureAwait(false);
    }

    private static async Task EnqueueRunsAsync(
        IEnumerable<GenerationRunDispatcher> dispatchers)
    {
        foreach (GenerationRunDispatcher dispatcher in dispatchers)
        {
            await dispatcher
                .EnqueueAsync(CreateRunRequest(), CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private static async Task CompleteRunAsync(BlockingRunTestContext context)
    {
        context.ApiClient.Complete();

        await WaitForStatusAsync(
            context.LifecycleEventHub,
            GenerationLifecycleStatus.Completed).ConfigureAwait(false);
    }

    private static async Task DisposeAndWaitForFailureAsync(
        BlockingRunTestContext context)
    {
        context.Dispatcher.Dispose();

        await WaitForStatusAsync(
            context.LifecycleEventHub,
            GenerationLifecycleStatus.Failed).ConfigureAwait(false);
    }

    private static async Task WaitForStatusAsync(
        TestGenerationLifecycleEventHub lifecycleEventHub,
        GenerationLifecycleStatus status)
    {
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == status),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task WaitForStatusCountAsync(
        TestGenerationLifecycleEventHub lifecycleEventHub,
        GenerationLifecycleStatus status,
        int expectedCount)
    {
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Count(
                lifecycleEvent => lifecycleEvent.Status == status) == expectedCount,
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task CompleteRunsAndAssertConcurrencyLimitAsync(
        BlockingImageGenerationApiClient apiClient,
        TestGenerationLifecycleEventHub lifecycleEventHub)
    {
        await apiClient
            .WaitForCallCountAsync(GenerationConcurrencyLimiter.MaxConcurrentGenerations)
            .ConfigureAwait(false);
        apiClient.ActiveCount.Should().Be(GenerationConcurrencyLimiter.MaxConcurrentGenerations);
        apiClient.MaxActiveCount.Should().Be(GenerationConcurrencyLimiter.MaxConcurrentGenerations);

        apiClient.Complete();
        await WaitForStatusCountAsync(
            lifecycleEventHub,
            GenerationLifecycleStatus.Completed,
            OverLimitRunCount).ConfigureAwait(false);

        apiClient.MaxActiveCount.Should().Be(GenerationConcurrencyLimiter.MaxConcurrentGenerations);
    }

    private sealed record RunTestContext(
        TestGenerationLifecycleEventHub LifecycleEventHub,
        GenerationRunDispatcher Dispatcher);

    private sealed record BlockingRunTestContext(
        BlockingImageGenerationApiClient ApiClient,
        TestGenerationLifecycleEventHub LifecycleEventHub,
        GenerationRunDispatcher Dispatcher);

    private sealed class BlockingImageGenerationApiClient : IImageGenerationApiClient
    {
        public int ActiveCount => Volatile.Read(ref _activeCount);
        public int MaxActiveCount => Volatile.Read(ref _maxActiveCount);
        public IReadOnlyList<ImageGenerationRequestDto> CapturedRequests
        {
            get
            {
                lock (_syncRoot)
                {
                    return _capturedRequests.ToList();
                }
            }
        }
        public IReadOnlyList<string> CapturedProviderCredentials
        {
            get
            {
                lock (_syncRoot)
                {
                    return _capturedProviderCredentials.ToList();
                }
            }
        }
        public IReadOnlyList<bool> CapturedCancellationTokenCanBeCanceled
        {
            get
            {
                lock (_syncRoot)
                {
                    return _capturedCancellationTokenCanBeCanceled.ToList();
                }
            }
        }
        public IReadOnlyList<CancellationToken> CapturedCancellationTokens
        {
            get
            {
                lock (_syncRoot)
                {
                    return _capturedCancellationTokens.ToList();
                }
            }
        }

        private readonly object _syncRoot = new();
        private readonly List<ImageGenerationRequestDto> _capturedRequests = [];
        private readonly List<string> _capturedProviderCredentials = [];
        private readonly List<bool> _capturedCancellationTokenCanBeCanceled = [];
        private readonly List<CancellationToken> _capturedCancellationTokens = [];
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeCount;
        private int _callCount;
        private int _maxActiveCount;

        public async Task<GenerationBatchDto> CreateGenerationAsync(
            ImageGenerationRequestDto request,
            Guid logicalGenerationId,
            int attemptNumber,
            string providerCredential,
            CancellationToken ct = default)
        {
            lock (_syncRoot)
            {
                _capturedRequests.Add(request);
                _capturedProviderCredentials.Add(providerCredential);
                _capturedCancellationTokenCanBeCanceled.Add(ct.CanBeCanceled);
                _capturedCancellationTokens.Add(ct);
                _callCount++;
            }

            UpdateMaxActiveCount(Interlocked.Increment(ref _activeCount));

            try
            {
                await _completion.Task.WaitAsync(ct).ConfigureAwait(false);

                return CreateBatch(request);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCount);
            }
        }

        public void Complete()
        {
            _completion.TrySetResult();
        }

        public async Task WaitForCallCountAsync(int expectedCallCount)
        {
            await AsyncTestWaiter.WaitForConditionAsync(
                () =>
                {
                    lock (_syncRoot)
                    {
                        return _callCount >= expectedCallCount;
                    }
                },
                CancellationToken.None).ConfigureAwait(false);
        }

        private void UpdateMaxActiveCount(int activeCount)
        {
            while (true)
            {
                int currentMaxActiveCount = Volatile.Read(ref _maxActiveCount);

                if (activeCount <= currentMaxActiveCount)
                {
                    return;
                }

                int previousMaxActiveCount = Interlocked.CompareExchange(
                    ref _maxActiveCount,
                    activeCount,
                    currentMaxActiveCount);

                if (previousMaxActiveCount == currentMaxActiveCount)
                {
                    return;
                }
            }
        }
    }

    private sealed class SuccessfulImageGenerationApiClient : IImageGenerationApiClient
    {
        public Task<GenerationBatchDto> CreateGenerationAsync(
            ImageGenerationRequestDto request,
            Guid logicalGenerationId,
            int attemptNumber,
            string providerCredential,
            CancellationToken ct = default)
        {
            return Task.FromResult(CreateBatch(request));
        }
    }

    private sealed class RetrySequenceImageGenerationApiClient
        : IImageGenerationApiClient
    {
        public IReadOnlyList<int> AttemptNumbers
        {
            get
            {
                lock (_syncRoot)
                {
                    return _attemptNumbers.ToList();
                }
            }
        }
        public IReadOnlyList<Guid> LogicalGenerationIds
        {
            get
            {
                lock (_syncRoot)
                {
                    return _logicalGenerationIds.ToList();
                }
            }
        }

        private readonly object _syncRoot = new();
        private readonly List<int> _attemptNumbers = [];
        private readonly List<Guid> _logicalGenerationIds = [];
        private readonly int _retryableFailureCount;
        private readonly bool _succeedAfterFailures;
        private readonly bool _retryable;

        public RetrySequenceImageGenerationApiClient(
            int retryableFailureCount,
            bool succeedAfterFailures,
            bool retryable = true)
        {
            _retryableFailureCount = retryableFailureCount;
            _succeedAfterFailures = succeedAfterFailures;
            _retryable = retryable;
        }

        public Task<GenerationBatchDto> CreateGenerationAsync(
            ImageGenerationRequestDto request,
            Guid logicalGenerationId,
            int attemptNumber,
            string providerCredential,
            CancellationToken ct = default)
        {
            lock (_syncRoot)
            {
                _attemptNumbers.Add(attemptNumber);
                _logicalGenerationIds.Add(logicalGenerationId);
            }

            if (attemptNumber <= _retryableFailureCount)
            {
                throw new GenerationAttemptException(
                    "Transient provider failure.",
                    GenerationProviderFailureErrorCodes.InternalError,
                    _retryable);
            }

            if (!_succeedAfterFailures)
            {
                throw new GenerationAttemptException(
                    "Terminal provider failure.",
                    GenerationProviderFailureErrorCodes.InternalError,
                    false);
            }

            return Task.FromResult(CreateBatch(request));
        }
    }

    private sealed class ManualGenerationConcurrencyLimiter : IGenerationConcurrencyLimiter
    {
        private readonly TaskCompletionSource _waitCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ReleaseCount { get; private set; }

        public async Task WaitAsync(CancellationToken ct)
        {
            await _waitCompletion.Task.WaitAsync(ct).ConfigureAwait(false);
        }

        public void Release()
        {
            ReleaseCount++;
        }

        public void ReleaseWait()
        {
            _waitCompletion.TrySetResult();
        }
    }
}
