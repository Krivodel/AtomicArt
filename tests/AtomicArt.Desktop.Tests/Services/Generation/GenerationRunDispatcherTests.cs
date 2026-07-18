using Microsoft.Extensions.Logging.Abstractions;

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
    private static readonly DateTime RequestedAtUtc = new(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 4, 12, 0, 1, DateTimeKind.Utc);

    [Fact]
    public async Task EnqueueAsync_WithDelayedApi_ReturnsBeforeGenerationCompletes()
    {
        BlockingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);
        GenerationRunRequest request = CreateRunRequest();

        Task enqueueTask = dispatcher.EnqueueAsync(request, CancellationToken.None);
        await enqueueTask;

        enqueueTask.IsCompletedSuccessfully.Should().BeTrue();
        lifecycleEventHub.PublishedEvents
            .Select(lifecycleEvent => lifecycleEvent.Status)
            .Take(2)
            .Should()
            .Equal(
                GenerationLifecycleStatus.StartRequested,
                GenerationLifecycleStatus.Started);
        lifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed);

        await apiClient.WaitForCallCountAsync(1);
        apiClient.Complete();
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed),
            CancellationToken.None);
    }

    [Fact]
    public async Task EnqueueAsync_WithRequest_UsesProvidedRequestAndCredential()
    {
        BlockingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);
        GenerationRunRequest runRequest = CreateRunRequest();

        await dispatcher.EnqueueAsync(runRequest, CancellationToken.None);
        await apiClient.WaitForCallCountAsync(1);

        apiClient.CapturedRequests.Should().ContainSingle(request =>
            ReferenceEquals(request, runRequest.Request));
        apiClient.CapturedProviderCredentials.Should().ContainSingle()
            .Which.Should().Be(TestGenerationCredentials.ProviderCredential);

        apiClient.Complete();
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed),
            CancellationToken.None);
    }

    [Fact]
    public async Task EnqueueAsync_WhenApiThrows_PublishesFailedEvent()
    {
        ThrowingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

        await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);

        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Failed),
            CancellationToken.None);

        List<GenerationLifecycleEvent> events = lifecycleEventHub.PublishedEvents.ToList();
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
    public async Task EnqueueAsync_WhenCommandTokenCanceledAfterAcceptedRun_KeepsBackgroundGeneration()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        BlockingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

        await dispatcher.EnqueueAsync(CreateRunRequest(), cancellationTokenSource.Token);
        await apiClient.WaitForCallCountAsync(1);

        CancellationToken runToken = apiClient.CapturedCancellationTokens.Should().ContainSingle()
            .Which;
        runToken.CanBeCanceled.Should().BeTrue();

        await cancellationTokenSource.CancelAsync();
        runToken.IsCancellationRequested.Should().BeFalse();
        apiClient.Complete();

        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed),
            CancellationToken.None);

        lifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.StartFailed);
        lifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Failed);
        apiClient.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_WithMoreThanLimit_LimitsConcurrentApiCalls()
    {
        BlockingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

        for (int i = 0; i < GenerationConcurrencyLimiter.MaxConcurrentGenerations + 6; i++)
        {
            await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        }

        await apiClient.WaitForCallCountAsync(GenerationConcurrencyLimiter.MaxConcurrentGenerations);
        apiClient.ActiveCount.Should().Be(GenerationConcurrencyLimiter.MaxConcurrentGenerations);
        apiClient.MaxActiveCount.Should().Be(GenerationConcurrencyLimiter.MaxConcurrentGenerations);

        apiClient.Complete();
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Count(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed)
                == GenerationConcurrencyLimiter.MaxConcurrentGenerations + 6,
            CancellationToken.None);
        apiClient.MaxActiveCount.Should().Be(GenerationConcurrencyLimiter.MaxConcurrentGenerations);
    }

    [Fact]
    public async Task EnqueueAsync_WithSharedLimiterAcrossDispatchers_LimitsConcurrentApiCalls()
    {
        BlockingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationConcurrencyLimiter limiter = new();
        GenerationRunDispatcher firstDispatcher = CreateDispatcher(apiClient, lifecycleEventHub, limiter);
        GenerationRunDispatcher secondDispatcher = CreateDispatcher(apiClient, lifecycleEventHub, limiter);

        for (int i = 0; i < GenerationConcurrencyLimiter.MaxConcurrentGenerations + 6; i++)
        {
            GenerationRunDispatcher dispatcher = (i % 2) == 0
                ? firstDispatcher
                : secondDispatcher;
            await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        }

        await apiClient.WaitForCallCountAsync(GenerationConcurrencyLimiter.MaxConcurrentGenerations);
        apiClient.ActiveCount.Should().Be(GenerationConcurrencyLimiter.MaxConcurrentGenerations);
        apiClient.MaxActiveCount.Should().Be(GenerationConcurrencyLimiter.MaxConcurrentGenerations);

        apiClient.Complete();
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Count(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed)
                == GenerationConcurrencyLimiter.MaxConcurrentGenerations + 6,
            CancellationToken.None);
        apiClient.MaxActiveCount.Should().Be(GenerationConcurrencyLimiter.MaxConcurrentGenerations);
    }

    [Fact]
    public async Task EnqueueAsync_WithTwoRuns_PublishesSeparateCorrelationIds()
    {
        SuccessfulImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

        await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);

        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Count(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed) == 2,
            CancellationToken.None);

        lifecycleEventHub.PublishedEvents
            .Where(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Started)
            .Select(lifecycleEvent => lifecycleEvent.CorrelationId)
            .Should()
            .OnlyHaveUniqueItems()
            .And.HaveCount(2);
    }

    [Fact]
    public async Task EnqueueAsync_WithOverlappingRunsAndCanceledFirstCommandToken_DoesNotPublishStartFailed()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        BlockingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

        await dispatcher.EnqueueAsync(CreateRunRequest(), cancellationTokenSource.Token);
        await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        await apiClient.WaitForCallCountAsync(2);

        await cancellationTokenSource.CancelAsync();
        apiClient.Complete();

        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Count(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed) == 2,
            CancellationToken.None);

        lifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.StartFailed);
        lifecycleEventHub.PublishedEvents
            .Where(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Started)
            .Select(lifecycleEvent => lifecycleEvent.CorrelationId)
            .Should()
            .OnlyHaveUniqueItems()
            .And.HaveCount(2);
        apiClient.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task Dispose_WithAcceptedStartedRun_PublishesFailedWithoutStartFailed()
    {
        BlockingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

        await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        await apiClient.WaitForCallCountAsync(1);
        CancellationToken runToken = apiClient.CapturedCancellationTokens.Should().ContainSingle()
            .Which;

        dispatcher.Dispose();

        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Failed),
            CancellationToken.None);

        runToken.IsCancellationRequested.Should().BeTrue();
        lifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.StartFailed);
        lifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed);
        apiClient.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task Dispose_WithAcceptedRunBeforeApiStarts_PublishesFailedWithoutStartFailed()
    {
        BlockingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        ManualGenerationConcurrencyLimiter limiter = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub, limiter);

        await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        dispatcher.Dispose();
        limiter.ReleaseWait();

        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Failed),
            CancellationToken.None);

        apiClient.CapturedRequests.Should().BeEmpty();
        lifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.StartFailed);
        lifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed);
        limiter.ReleaseCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        SuccessfulImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

        Action act = () =>
        {
            dispatcher.Dispose();
            dispatcher.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_AfterCompletedRun_DoesNotThrow()
    {
        SuccessfulImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

        await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed),
            CancellationToken.None);

        Action act = dispatcher.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_AfterCanceledRun_DoesNotThrow()
    {
        BlockingImageGenerationApiClient apiClient = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

        await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
        await apiClient.WaitForCallCountAsync(1);
        dispatcher.Dispose();
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Failed),
            CancellationToken.None);

        Action act = dispatcher.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_WhenRunCompletesConcurrently_DoesNotThrow()
    {
        for (int i = 0; i < ConcurrentDisposeAttemptCount; i++)
        {
            BlockingImageGenerationApiClient apiClient = new();
            TestGenerationLifecycleEventHub lifecycleEventHub = new();
            GenerationRunDispatcher dispatcher = CreateDispatcher(apiClient, lifecycleEventHub);

            await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);
            await apiClient.WaitForCallCountAsync(1);

            Task disposeTask = Task.Run(dispatcher.Dispose);
            apiClient.Complete();
            Func<Task> act = async () => await disposeTask.ConfigureAwait(false);

            await act.Should().NotThrowAsync();
            await AsyncTestWaiter.WaitForConditionAsync(
                () => lifecycleEventHub.PublishedEvents.Any(
                    lifecycleEvent => (lifecycleEvent.Status == GenerationLifecycleStatus.Completed)
                        || (lifecycleEvent.Status == GenerationLifecycleStatus.Failed)),
                CancellationToken.None);
            apiClient.ActiveCount.Should().Be(0);
        }
    }

    private static GenerationRunDispatcher CreateDispatcher(
        IImageGenerationApiClient apiClient,
        IGenerationLifecycleEventHub lifecycleEventHub,
        IGenerationConcurrencyLimiter? limiter = null)
    {
        return new GenerationRunDispatcher(
            limiter ?? new GenerationConcurrencyLimiter(),
            apiClient,
            new NanoBanana2GenerationLifecyclePublisher(lifecycleEventHub),
            new NullGenerationResultStorage(),
            TestGenerationActivityTrackerFactory.Create(),
            NullLogger<GenerationRunDispatcher>.Instance);
    }

    private static GenerationRunRequest CreateRunRequest()
    {
        ImageGenerationRequestDto request = new(
            ModelId,
            "Prompt",
            "1:1",
            "1k",
            1d,
            1,
            []);
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
        GenerationItemDto item = new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            request.ModelId,
            "Test Model",
            request.Prompt,
            request.AspectRatio,
            request.Resolution,
            CreatedAtUtc,
            GenerationItemStatus.Generated,
            null,
            null);

        return new GenerationBatchDto(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            [item]);
    }

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
            string providerCredential,
            CancellationToken ct = default)
        {
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
