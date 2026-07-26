using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Media.Imaging;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

public sealed class GalleryPreviewBitmapProviderTests : AnimatedGalleryControlTestBase
{
    private const long SingleBitmapCacheSizeBytes = 4L;
    private const string FirstImagePath = "first.png";
    private const string SecondImagePath = "second.png";

    [Fact]
    public async Task AcquireAsync_WithConcurrentRequests_LoadsOnceAndSharesBitmap()
    {
        await DispatchAsync(async () =>
        {
            Bitmap bitmap = CreateBitmap();
            TaskCompletionSource<Bitmap?> completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            StubGalleryPreviewBitmapLoader loader =
                new((_, _) => completion.Task);
            using GalleryPreviewBitmapProvider provider = CreateProvider(loader);

            Task<GalleryPreviewBitmapLease?> firstLeaseTask =
                provider.AcquireAsync(FirstImagePath, CancellationToken.None);
            Task<GalleryPreviewBitmapLease?> secondLeaseTask =
                provider.AcquireAsync(FirstImagePath, CancellationToken.None);
            completion.SetResult(bitmap);
            using GalleryPreviewBitmapLease firstLease = await firstLeaseTask
                ?? throw new InvalidOperationException("First bitmap lease was not created.");
            using GalleryPreviewBitmapLease secondLease = await secondLeaseTask
                ?? throw new InvalidOperationException("Second bitmap lease was not created.");

            firstLease.Bitmap.Should().BeSameAs(bitmap);
            secondLease.Bitmap.Should().BeSameAs(bitmap);
            loader.InvocationCount.Should().Be(1);
        });
    }

    [Fact]
    public async Task AcquireAsync_WhenCacheLimitExceeded_EvictsLeastRecentlyUsedBitmap()
    {
        await DispatchAsync(async () =>
        {
            Queue<Bitmap> bitmaps = new Queue<Bitmap>(
                new Bitmap[]
                {
                    CreateBitmap(),
                    CreateBitmap(),
                    CreateBitmap()
                });
            StubGalleryPreviewBitmapLoader loader =
                new((_, _) => Task.FromResult<Bitmap?>(bitmaps.Dequeue()));
            using GalleryPreviewBitmapProvider provider = CreateProvider(
                loader,
                SingleBitmapCacheSizeBytes);

            using (GalleryPreviewBitmapLease firstLease =
                await provider.AcquireAsync(FirstImagePath, CancellationToken.None)
                ?? throw new InvalidOperationException("First bitmap lease was not created."))
            {
            }

            using (GalleryPreviewBitmapLease secondLease =
                await provider.AcquireAsync(SecondImagePath, CancellationToken.None)
                ?? throw new InvalidOperationException("Second bitmap lease was not created."))
            {
            }

            using GalleryPreviewBitmapLease reloadedFirstLease =
                await provider.AcquireAsync(FirstImagePath, CancellationToken.None)
                ?? throw new InvalidOperationException("Reloaded bitmap lease was not created.");

            loader.InvocationCount.Should().Be(3);
        });
    }

    [Fact]
    public async Task Dispose_WithActiveLoad_CancelsLoadAndCompletesPendingRequest()
    {
        TaskCompletionSource loadCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubGalleryPreviewBitmapLoader loader = new(
            async (_, ct) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);

                    return null;
                }
                finally
                {
                    loadCompleted.SetResult();
                }
            });
        using GalleryPreviewBitmapProvider provider = CreateProvider(loader);
        Task<GalleryPreviewBitmapLease?> leaseTask =
            provider.AcquireAsync(FirstImagePath, CancellationToken.None);

        provider.Dispose();
        GalleryPreviewBitmapLease? lease = await leaseTask;
        await loadCompleted.Task;

        lease.Should().BeNull();
    }

    [Fact]
    public async Task AcquireAsync_WhenOnlyRequestIsCanceled_CancelsUnderlyingLoad()
    {
        TaskCompletionSource loadCanceled =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        StubGalleryPreviewBitmapLoader loader = new(
            async (_, ct) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);

                    return null;
                }
                finally
                {
                    loadCanceled.SetResult();
                }
            });
        using GalleryPreviewBitmapProvider provider = CreateProvider(loader);
        using CancellationTokenSource cancellation = new();
        Task<GalleryPreviewBitmapLease?> leaseTask =
            provider.AcquireAsync(FirstImagePath, cancellation.Token);

        cancellation.Cancel();
        Func<Task> act = async () => await leaseTask;

        await act.Should().ThrowAsync<OperationCanceledException>();
        await loadCanceled.Task;
    }

    private static GalleryPreviewBitmapProvider CreateProvider(
        IGalleryPreviewBitmapLoader loader,
        long maximumCacheSizeBytes = 64L * 1024L * 1024L)
    {
        return new GalleryPreviewBitmapProvider(
            loader,
            NullLogger<GalleryPreviewBitmapProvider>.Instance,
            maximumCacheSizeBytes);
    }

    private static Bitmap CreateBitmap()
    {
        byte[] bytes = GalleryThumbnailTestImages.CreatePngBytes(2, 2);
        using MemoryStream stream = new(bytes);

        return new Bitmap(stream);
    }
}
