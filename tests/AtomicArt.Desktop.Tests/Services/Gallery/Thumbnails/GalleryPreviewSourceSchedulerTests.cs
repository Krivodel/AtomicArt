using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

public sealed class GalleryPreviewSourceSchedulerTests
{
    [Fact]
    public void PresentAsync_WithMultipleRequests_PresentsOnePerFrame()
    {
        TestUiFrameScheduler frameScheduler = new();
        GalleryPreviewSourceScheduler scheduler = new(
            frameScheduler,
            TestApiConfiguration.CreateGalleryOptionsWrapper());
        int presentationCount = 0;

        Task first = scheduler.PresentAsync(
            () =>
            {
                presentationCount++;
            },
            CancellationToken.None);
        Task second = scheduler.PresentAsync(
            () =>
            {
                presentationCount++;
            },
            CancellationToken.None);
        Task third = scheduler.PresentAsync(
            () =>
            {
                presentationCount++;
            },
            CancellationToken.None);

        frameScheduler.RequestedFrameCount.Should().Be(1);
        presentationCount.Should().Be(0);
        first.IsCompleted.Should().BeFalse();
        second.IsCompleted.Should().BeFalse();
        third.IsCompleted.Should().BeFalse();

        frameScheduler.RunNextFrame(TimeSpan.Zero);

        presentationCount.Should().Be(1);
        first.IsCompletedSuccessfully.Should().BeTrue();
        second.IsCompleted.Should().BeFalse();
        third.IsCompleted.Should().BeFalse();

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(16d));

        presentationCount.Should().Be(2);
        second.IsCompletedSuccessfully.Should().BeTrue();
        third.IsCompleted.Should().BeFalse();

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(32d));

        presentationCount.Should().Be(3);
        third.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void PresentAsync_WhenFirstRequestIsCanceled_DoesNotConsumeFrameSlot()
    {
        TestUiFrameScheduler frameScheduler = new();
        GalleryPreviewSourceScheduler scheduler = new(
            frameScheduler,
            TestApiConfiguration.CreateGalleryOptionsWrapper());
        using CancellationTokenSource cancellation = new();
        int presentationCount = 0;
        Task canceled = scheduler.PresentAsync(
            () =>
            {
                presentationCount++;
            },
            cancellation.Token);
        Task active = scheduler.PresentAsync(
            () =>
            {
                presentationCount++;
            },
            CancellationToken.None);
        cancellation.Cancel();

        frameScheduler.RunNextFrame(TimeSpan.Zero);

        presentationCount.Should().Be(1);
        canceled.IsCanceled.Should().BeTrue();
        active.IsCompletedSuccessfully.Should().BeTrue();
    }
}
