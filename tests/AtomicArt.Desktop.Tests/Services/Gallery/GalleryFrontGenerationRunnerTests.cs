using Microsoft.Extensions.Logging.Abstractions;

using Avalonia;
using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;
using AtomicArt.Desktop.Tests.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryFrontGenerationRunnerTests
{
    private const int FrontGenerationBatchSize = 4;
    private const int ExistingItemCount = 40;
    private const int FirstExistingItemIndex = 0;
    private const int ExistingItemIdStart = 100;
    private const int FrontItemIdStart = 200;

    [Fact]
    public async Task RunAsync_WhenAnimationsComplete_ClearsOverlayCanvas()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        UiAnimationScheduler animationScheduler =
            UiAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        GalleryFrontGenerationRunner runner = CreateRunner(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        GalleryOperation operation = new GenerateFrontGalleryOperation(new List<object> { Guid.NewGuid() });

        Task runnerTask = runner.RunAsync(new List<GalleryOperation> { operation }, context, CancellationToken.None);

        context.OverlayCanvas.Children.Should().HaveCount(4);
        runnerTask.IsCompleted.Should().BeFalse();

        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(949d));

        runnerTask.IsCompleted.Should().BeFalse();

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(950d));
        await runnerTask;

        context.OverlayCanvas.Children.Should().BeEmpty();
        operation.Completion.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenAnimationCreationFails_ClearsOverlayCanvasAndFailsOperation()
    {
        TestUiFrameScheduler frameScheduler = new();
        UiAnimationScheduler animationScheduler = new(
            frameScheduler,
            (_, _) =>
            {
                throw new InvalidOperationException("Frame application failed.");
            });
        GalleryFrontGenerationRunner runner = CreateRunner(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        Guid itemId = Guid.NewGuid();
        GalleryOperation operation = new GenerateFrontGalleryOperation(new List<object> { itemId });

        await runner.RunAsync(new List<GalleryOperation> { operation }, context, CancellationToken.None);

        context.OverlayCanvas.Children.Should().BeEmpty();
        context.HiddenItemIds.Should().NotContain(itemId);
        operation.Completion.Task.IsFaulted.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenCancelledDuringSpawn_ClearsOverlayCanvasAndCancelsOperation()
    {
        TestUiFrameScheduler frameScheduler = new();
        UiAnimationScheduler animationScheduler = new(frameScheduler);
        GalleryFrontGenerationRunner runner = CreateRunner(animationScheduler);
        GalleryOperationCoordinator context = CreateContext(frameScheduler);
        CancellationTokenSource cancellationTokenSource = new();
        GalleryOperation operation = new GenerateFrontGalleryOperation(new List<object> { Guid.NewGuid() });

        Task runnerTask = runner.RunAsync(new List<GalleryOperation> { operation }, context, cancellationTokenSource.Token);

        context.OverlayCanvas.Children.Should().HaveCount(4);

        cancellationTokenSource.Cancel();
        await runnerTask;

        context.OverlayCanvas.Children.Should().BeEmpty();
        operation.Completion.Task.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenGenerateFrontQueuedDuringActiveFlight_RetargetsFlyingCopyAndCleansOverlay()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        UiAnimationScheduler animationScheduler =
            UiAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        GalleryFrontGenerationRunner runner = CreateRunner(animationScheduler);
        GalleryOperationRunnerRegistry registry = new(
            new List<IGalleryOperationRunner> { runner });
        GalleryOperationCoordinator context = CreateContext(frameScheduler, registry);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        GalleryOperation firstOperation = new GenerateFrontGalleryOperation(new List<object> { firstId });

        Task runnerTask = runner.RunAsync(new List<GalleryOperation> { firstOperation }, context, CancellationToken.None);

        context.OverlayCanvas.Children.Should().HaveCount(4);
        appliedFrames.Clear();

        Task secondTask = context.GenerateFrontAsync(new List<object> { secondId }, CancellationToken.None);

        HasRetargetFrame(appliedFrames).Should().BeTrue();

        RunUntilCompleted(frameScheduler, runnerTask, TimeSpan.FromMilliseconds(1200d));
        await Task.WhenAll(runnerTask, secondTask, firstOperation.Completion.Task);

        context.OverlayCanvas.Children.Should().BeEmpty();
        firstOperation.Completion.Task.IsCompletedSuccessfully.Should().BeTrue();
        secondTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenTopCardsWereVirtualizedBeforeFrontGeneration_AnimatesTopCards()
    {
        TestUiFrameScheduler frameScheduler = new();
        List<AppliedMotionFrame> appliedFrames = [];
        UiAnimationScheduler animationScheduler =
            UiAnimationSchedulerTestFactory.Create(frameScheduler, appliedFrames);
        GalleryLayoutService layout = new();
        GalleryFrontGenerationRunner runner = CreateRunner(animationScheduler, layout);
        List<object> existingItems = CreateFixedItems(ExistingItemCount, ExistingItemIdStart);
        Guid firstExistingId = (Guid)existingItems[FirstExistingItemIndex];
        GalleryOperationCoordinator context = CreateContextWithVirtualizedTopCard(
            frameScheduler,
            layout,
            existingItems);
        GalleryOperation operation = new GenerateFrontGalleryOperation(
            CreateFixedItems(FrontGenerationBatchSize, FrontItemIdStart));

        Task runnerTask = runner.RunAsync(new List<GalleryOperation> { operation }, context, CancellationToken.None);

        Control firstExistingControl = context.CardControls[firstExistingId];
        appliedFrames.Should().Contain(frame =>
            ReferenceEquals(frame.Control, firstExistingControl)
            && frame.Frame.Y == -GalleryLayoutService.CardHeight);

        RunUntilCompleted(frameScheduler, runnerTask, TimeSpan.FromMilliseconds(1200d));
        await Task.WhenAll(runnerTask, operation.Completion.Task);
    }

    private static GalleryFrontGenerationRunner CreateRunner(UiAnimationScheduler animationScheduler)
    {
        return CreateRunner(animationScheduler, new GalleryLayoutService());
    }

    private static GalleryFrontGenerationRunner CreateRunner(
        UiAnimationScheduler animationScheduler,
        GalleryLayoutService layout)
    {
        GalleryOverlayEffects overlayEffects = new(animationScheduler);
        GalleryMotionAnimator animator = GalleryMotionAnimatorTestFactory.Create(animationScheduler, overlayEffects, layout);

        return new GalleryFrontGenerationRunner(
            animationScheduler,
            animator,
            layout,
            NullLogger<GalleryFrontGenerationRunner>.Instance,
            new GalleryFrontGenerationRetargetWaiter(animationScheduler));
    }

    private static GalleryOperationCoordinator CreateContext(TestUiFrameScheduler frameScheduler)
    {
        return CreateContext(
            frameScheduler,
            new GalleryOperationRunnerRegistry(new List<IGalleryOperationRunner>()));
    }

    private static GalleryOperationCoordinator CreateContext(
        TestUiFrameScheduler frameScheduler,
        IGalleryOperationRunnerRegistry registry)
    {
        return GalleryOperationCoordinatorTestFactory.CreateAttached(
            frameScheduler,
            registry,
            new List<object>());
    }

    private static GalleryOperationCoordinator CreateContextWithVirtualizedTopCard(
        TestUiFrameScheduler frameScheduler,
        GalleryLayoutService layout,
        List<object> existingItems)
    {
        ScrollViewer scrollViewer = new();
        scrollViewer.Arrange(new Rect(0d, 0d, 980d, 640d));
        GalleryOperationCoordinator context = GalleryOperationCoordinatorTestFactory.Create(
            frameScheduler,
            new GalleryOperationRunnerRegistry(new List<IGalleryOperationRunner>()));
        context.AttachScene(
            scrollViewer,
            new Canvas(),
            new Canvas(),
            existingItems,
            item => (Guid)item,
            _ => new Border(),
            () => Task.CompletedTask);
        layout.RenderCards(context);
        Guid firstExistingId = (Guid)existingItems[FirstExistingItemIndex];
        Control firstExistingControl = context.CardControls[firstExistingId];
        context.GalleryPanel.Children.Remove(firstExistingControl);
        context.CardControls.Remove(firstExistingId);

        return context;
    }

    private static List<object> CreateFixedItems(int count, int start)
    {
        List<object> items = [];
        for (int i = 0; i < count; i++)
        {
            items.Add(CreateFixedId(start + i));
        }

        return items;
    }

    private static Guid CreateFixedId(int value)
    {
        return new Guid($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private static bool HasRetargetFrame(List<AppliedMotionFrame> appliedFrames)
    {
        return appliedFrames.Any(frame =>
            frame.Frame is { Opacity: 1d, Scale: >= 0.30d and <= 1.10d });
    }

    private static void RunUntilCompleted(
        TestUiFrameScheduler frameScheduler,
        Task task,
        TimeSpan endTime)
    {
        double step = Math.Max(1d, endTime.TotalMilliseconds / 20d);
        for (int i = 0; i <= 20 && !task.IsCompleted; i++)
        {
            if (!frameScheduler.HasQueuedFrame)
            {
                continue;
            }

            frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(i * step));
        }

        if (!task.IsCompleted && frameScheduler.HasQueuedFrame)
        {
            frameScheduler.RunNextFrame(endTime);
        }
    }

}
