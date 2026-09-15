using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Tests.Services.Dlss5;

public sealed class Dlss5RenderSchedulerTests
{
    private const int TestImageSize = 2;

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Request_WhenNewerRevisionArrives_CancelsStaleWorkAndPublishesOnlyLatest()
    {
        BlockingNativeEngine engine = new();
        using Dlss5RenderScheduler scheduler = new(
            engine,
            new DataRootAccessCoordinator(),
            NullLogger<Dlss5RenderScheduler>.Instance);
        using SKBitmap source = new(TestImageSize, TestImageSize);
        TaskCompletionSource<long> published = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<Exception> failures = [];

        scheduler.Request(
            source,
            Dlss5RenderSettings.Default,
            1,
            (revision, result) =>
            {
                result.Bitmap.Dispose();
                published.TrySetResult(revision);
                return Task.CompletedTask;
            },
            exception =>
            {
                failures.Add(exception);
                return Task.CompletedTask;
            });

        await engine.FirstStarted.Task.WaitAsync(Dlss5RenderSchedulerTests.TestTimeout);
        scheduler.Request(
            source,
            Dlss5RenderSettings.Default with { GeneralIntensity = 1f },
            2,
            (revision, result) =>
            {
                result.Bitmap.Dispose();
                published.TrySetResult(revision);
                return Task.CompletedTask;
            },
            exception =>
            {
                failures.Add(exception);
                return Task.CompletedTask;
            });

        (await published.Task.WaitAsync(Dlss5RenderSchedulerTests.TestTimeout)).Should().Be(2);
        engine.CallCount.Should().Be(2);
        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_WhenSourceIsDisposedImmediately_StillRendersTheSnapshottedPixels()
    {
        SnapshotNativeEngine engine = new();
        using Dlss5RenderScheduler scheduler = new(
            engine,
            new DataRootAccessCoordinator(),
            NullLogger<Dlss5RenderScheduler>.Instance);
        SKBitmap source = new(TestImageSize, TestImageSize, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        source.SetPixel(1, 1, new SKColor(12, 34, 56, 255));

        scheduler.Request(
            source,
            Dlss5RenderSettings.Default,
            1,
            (_, result) =>
            {
                result.Bitmap.Dispose();
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask);
        source.Dispose();

        (await engine.Pixel.Task.WaitAsync(Dlss5RenderSchedulerTests.TestTimeout)).Should().Be(new SKColor(12, 34, 56, 255));
    }

    private sealed class BlockingNativeEngine : IDlss5NativeEngine
    {
        public TaskCompletionSource<object?> FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsInitialized => true;
        public int CallCount => Volatile.Read(ref _callCount);

        private int _callCount;

        public Task InitializeAsync(IProgress<int>? progress, CancellationToken ct)
        {
            _ = progress;
            _ = ct;
            return Task.CompletedTask;
        }

        public void Stop()
        {
        }

        public async Task<Dlss5NativeRenderResult> RenderAsync(
            SKBitmap source,
            Dlss5RenderSettings settings,
            CancellationToken ct)
        {
            _ = source;
            _ = settings;
            int call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                FirstStarted.TrySetResult(null);
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            ct.ThrowIfCancellationRequested();
            return new Dlss5NativeRenderResult(
                new SKBitmap(TestImageSize, TestImageSize),
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero);
        }
    }

    private sealed class SnapshotNativeEngine : IDlss5NativeEngine
    {
        public TaskCompletionSource<SKColor> Pixel { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsInitialized => true;

        public Task InitializeAsync(IProgress<int>? progress, CancellationToken ct)
        {
            _ = progress;
            _ = ct;
            return Task.CompletedTask;
        }

        public void Stop()
        {
        }

        public Task<Dlss5NativeRenderResult> RenderAsync(
            SKBitmap source,
            Dlss5RenderSettings settings,
            CancellationToken ct)
        {
            _ = settings;
            ct.ThrowIfCancellationRequested();
            Pixel.TrySetResult(source.GetPixel(1, 1));
            return Task.FromResult(
                new Dlss5NativeRenderResult(
                    new SKBitmap(TestImageSize, TestImageSize),
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero));
        }
    }
}
