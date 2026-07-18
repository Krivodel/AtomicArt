using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryRemoveRunnerTests
{
    [Fact]
    public async Task RunAsync_WhenRemovalStarts_CompletesOperationBeforeOverlayAnimationCompletes()
    {
        RemoveTestContext context = CreateContext();

        await context.RunAsync(CancellationToken.None);

        context.Coordinator.OverlayCanvas.Children.Should().ContainSingle();
        context.Operation.Completion.Task.IsCompletedSuccessfully.Should().BeTrue();

        context.FrameScheduler.RunNextFrame(TimeSpan.Zero);
        context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(519d));

        context.Coordinator.OverlayCanvas.Children.Should().ContainSingle();

        context.FrameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(520d));

        context.Coordinator.OverlayCanvas.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenCancelledBeforeRemoval_CancelsOperation()
    {
        RemoveTestContext context = CreateContext();
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        await context.RunAsync(cancellationTokenSource.Token);

        context.Coordinator.OverlayCanvas.Children.Should().BeEmpty();
        context.Operation.Completion.Task.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenRemovalAnimationFails_ClearsOverlayCanvasAndFailsOperation()
    {
        RemoveTestContext context = CreateContext(frameScheduler =>
            new GalleryAnimationScheduler(
                frameScheduler,
                (_, _) =>
                {
                    throw new InvalidOperationException("Frame application failed.");
                }));

        await context.RunAsync(CancellationToken.None);

        context.Coordinator.OverlayCanvas.Children.Should().BeEmpty();
        context.Operation.Completion.Task.IsFaulted.Should().BeTrue();
    }

    private static RemoveTestContext CreateContext(
        Func<TestUiFrameScheduler, GalleryAnimationScheduler>? animationSchedulerFactory = null)
    {
        TestUiFrameScheduler frameScheduler = new();
        GalleryAnimationScheduler animationScheduler =
            animationSchedulerFactory?.Invoke(frameScheduler)
            ?? new GalleryAnimationScheduler(frameScheduler);
        GalleryLayoutService layout = new();
        GalleryMotionAnimator animator = GalleryMotionAnimatorTestFactory.Create(
            animationScheduler,
            new GalleryOverlayEffects(animationScheduler),
            layout);
        GalleryRemoveRunner runner = new(
            animationScheduler,
            animator,
            layout,
            NullLogger<GalleryRemoveRunner>.Instance);
        Guid itemId = Guid.NewGuid();
        List<object> items = [itemId];
        GalleryOperationCoordinator coordinator = GalleryOperationCoordinatorTestFactory.CreateAttached(
            frameScheduler,
            items);
        GalleryOperation operation = new RemoveGalleryOperation(itemId);
        layout.RenderCards(coordinator);

        return new RemoveTestContext(
            frameScheduler,
            runner,
            coordinator,
            operation);
    }

    private sealed class RemoveTestContext
    {
        public TestUiFrameScheduler FrameScheduler { get; }
        public GalleryRemoveRunner Runner { get; }
        public GalleryOperationCoordinator Coordinator { get; }
        public GalleryOperation Operation { get; }

        public RemoveTestContext(
            TestUiFrameScheduler frameScheduler,
            GalleryRemoveRunner runner,
            GalleryOperationCoordinator coordinator,
            GalleryOperation operation)
        {
            FrameScheduler = frameScheduler;
            Runner = runner;
            Coordinator = coordinator;
            Operation = operation;
        }

        public Task RunAsync(CancellationToken ct)
        {
            return Runner.RunAsync(
                new List<GalleryOperation> { Operation },
                Coordinator,
                ct);
        }
    }
}
