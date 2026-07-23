using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryOperationCoordinatorTests
{
    [Fact]
    public async Task QueuedOperations_OnNextFrame_AreGroupedThroughRegisteredRunners()
    {
        TestUiFrameScheduler frameScheduler = new();
        RecordingRunner appendRunner = new(typeof(AppendBatchGalleryOperation));
        RecordingRunner frontRunner = new(typeof(GenerateFrontGalleryOperation));
        RecordingRunner removeRunner = new(typeof(RemoveGalleryOperation));
        RecordingRunner mixedRunner = new(typeof(MixedMutationGalleryOperation));
        GalleryOperationRunnerRegistry registry = new(
            [appendRunner, frontRunner, removeRunner, mixedRunner]);
        GalleryOperationCoordinator coordinator = CreateCoordinator(frameScheduler, registry);

        List<Task> tasks = QueueMixedOperations(coordinator);

        frameScheduler.RequestedFrameCount.Should().Be(1);
        tasks.Should().OnlyContain(task => !task.IsCompleted);

        frameScheduler.RunNextFrame(TimeSpan.Zero);
        await Task.WhenAll(tasks);

        AssertMixedOperationBatches(appendRunner, frontRunner, removeRunner, mixedRunner);
    }

    [Fact]
    public async Task RemoveOnlyMutationBatch_WhenQueued_UsesRemoveRunner()
    {
        TestUiFrameScheduler frameScheduler = new();
        RecordingRunner removeRunner = new(typeof(RemoveGalleryOperation));
        RecordingRunner mixedRunner = new(typeof(MixedMutationGalleryOperation));
        GalleryOperationRunnerRegistry registry = new(
            new List<IGalleryOperationRunner> { removeRunner, mixedRunner });
        GalleryOperationCoordinator coordinator = CreateCoordinator(frameScheduler, registry);

        Task firstTask = coordinator.RemoveAsync(Guid.NewGuid(), CancellationToken.None);
        Task secondTask = coordinator.RemoveAsync(Guid.NewGuid(), CancellationToken.None);

        frameScheduler.RunNextFrame(TimeSpan.Zero);
        await Task.WhenAll(firstTask, secondTask);

        removeRunner.Batches.Should().ContainSingle();
        removeRunner.Batches[0].Should().HaveCount(2);
        mixedRunner.Batches.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_WhenPreviousDeleteOverlayIsActive_ProcessesNextRemoveAndCleansOverlaysIndependently()
    {
        TestUiFrameScheduler frameScheduler = new();
        GalleryLayoutService layout = new();
        GalleryOperationCoordinator coordinator = CreateAnimatedRemoveCoordinator(frameScheduler, layout, out List<object> items);
        Guid firstId = (Guid)items[0];
        Guid secondId = (Guid)items[1];
        layout.RenderCards(coordinator);

        Task firstTask = coordinator.RemoveAsync(firstId, CancellationToken.None);
        frameScheduler.RunNextFrame(TimeSpan.Zero);
        await firstTask;

        coordinator.OverlayCanvas.Children.Should().ContainSingle();
        items.Should().NotContain(firstId);

        Task secondTask = coordinator.RemoveAsync(secondId, CancellationToken.None);
        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(1d));
        await secondTask;

        coordinator.OverlayCanvas.Children.Should().HaveCount(2);
        items.Should().NotContain(secondId);

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(520d));
        coordinator.OverlayCanvas.Children.Should().ContainSingle();

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(1040d));
        coordinator.OverlayCanvas.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task QueuedOperation_WhenCancelledBeforeFrame_CompletesAsCanceled()
    {
        TestUiFrameScheduler frameScheduler = new();
        RecordingRunner appendRunner = new(typeof(AppendBatchGalleryOperation));
        GalleryOperationRunnerRegistry registry = new(
            new List<IGalleryOperationRunner> { appendRunner });
        GalleryOperationCoordinator coordinator = CreateCoordinator(frameScheduler, registry);
        CancellationTokenSource cancellationTokenSource = new();

        Task appendTask = coordinator.AppendBatchAsync(new List<object> { Guid.NewGuid() }, cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();

        frameScheduler.RunNextFrame(TimeSpan.Zero);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await appendTask);
        appendRunner.Batches.Should().BeEmpty();
    }

    [Fact]
    public async Task QueuedOperation_WhenRunnerThrows_CompletesAsFaulted()
    {
        TestUiFrameScheduler frameScheduler = new();
        ThrowingRunner appendRunner = new(typeof(AppendBatchGalleryOperation));
        GalleryOperationRunnerRegistry registry = new(
            new List<IGalleryOperationRunner> { appendRunner });
        GalleryOperationCoordinator coordinator = CreateCoordinator(frameScheduler, registry);

        Task appendTask = coordinator.AppendBatchAsync(new List<object> { Guid.NewGuid() }, CancellationToken.None);

        frameScheduler.RunNextFrame(TimeSpan.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await appendTask);
    }

    private static GalleryOperationCoordinator CreateCoordinator(
        TestUiFrameScheduler frameScheduler,
        IGalleryOperationRunnerRegistry registry)
    {
        GalleryOperationCoordinator coordinator = GalleryOperationCoordinatorTestFactory.Create(frameScheduler, registry);
        coordinator.AttachScene(
            new ScrollViewer(),
            new Canvas(),
            new Canvas(),
            new List<object>(),
            item => (Guid)item,
            _ => new Border(),
            () => Task.CompletedTask);

        return coordinator;
    }

    private static GalleryOperationCoordinator CreateAnimatedRemoveCoordinator(
        TestUiFrameScheduler frameScheduler,
        GalleryLayoutService layout,
        out List<object> items)
    {
        UiAnimationScheduler animationScheduler = new(frameScheduler);
        GalleryMotionAnimator animator = GalleryMotionAnimatorTestFactory.Create(
            animationScheduler,
            new GalleryOverlayEffects(animationScheduler),
            layout);
        GalleryRemoveRunner removeRunner = new(animationScheduler, animator, layout, NullLogger<GalleryRemoveRunner>.Instance);
        GalleryOperationRunnerRegistry registry = new(
            new List<IGalleryOperationRunner> { removeRunner });
        GalleryOperationCoordinator coordinator = GalleryOperationCoordinatorTestFactory.Create(frameScheduler, registry);
        items = CreateRemoveItems();
        coordinator.AttachScene(
            CreateArrangedScrollViewer(),
            new Canvas(),
            new Canvas(),
            items,
            item => (Guid)item,
            _ => new Border(),
            () => Task.CompletedTask);

        return coordinator;
    }

    private static List<Task> QueueMixedOperations(GalleryOperationCoordinator coordinator)
    {
        List<Task> tasks =
        [
            coordinator.AppendBatchAsync(new List<object> { Guid.NewGuid() }, CancellationToken.None),
            coordinator.GenerateFrontAsync(new List<object> { Guid.NewGuid() }, CancellationToken.None),
            coordinator.RemoveAsync(Guid.NewGuid(), CancellationToken.None),
            coordinator.ApplyMixedMutationAsync(new List<object> { Guid.NewGuid() }, CancellationToken.None)
        ];

        return tasks;
    }

    private static void AssertMixedOperationBatches(
        RecordingRunner appendRunner,
        RecordingRunner frontRunner,
        RecordingRunner removeRunner,
        RecordingRunner mixedRunner)
    {
        appendRunner.Batches.Should().ContainSingle();
        appendRunner.Batches[0].Should().HaveCount(1);
        frontRunner.Batches.Should().ContainSingle();
        frontRunner.Batches[0].Should().HaveCount(1);
        removeRunner.Batches.Should().BeEmpty();
        mixedRunner.Batches.Should().ContainSingle();
        mixedRunner.Batches[0].Select(operation => operation.GetType())
            .Should()
            .Equal(typeof(RemoveGalleryOperation), typeof(MixedMutationGalleryOperation));
    }

    private static List<object> CreateRemoveItems()
    {
        List<object> items =
        [
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        ];

        return items;
    }

    private static ScrollViewer CreateArrangedScrollViewer()
    {
        ScrollViewer scrollViewer = new();
        scrollViewer.Arrange(new Avalonia.Rect(0d, 0d, 560d, 640d));

        return scrollViewer;
    }

    private sealed class RecordingRunner : GalleryOperationRunnerTestDouble
    {
        public List<List<GalleryOperation>> Batches { get; } = [];

        public RecordingRunner(Type operationType)
            : base(operationType)
        {
        }

        protected override Task RunCoreAsync(
            IReadOnlyList<GalleryOperation> operations,
            GalleryOperationCoordinator context,
            CancellationToken ct)
        {
            Batches.Add(operations.ToList());

            foreach (GalleryOperation operation in operations)
            {
                operation.Completion.TrySetResult();
            }

            return Task.CompletedTask;
        }

        protected override bool CanRunCore(IReadOnlyList<GalleryOperation> operations)
        {
            if (OperationType == typeof(RemoveGalleryOperation))
            {
                return operations.All(operation => operation.GetType() == OperationType);
            }

            return base.CanRunCore(operations);
        }

        protected override IReadOnlyList<GalleryOperation> SelectOperationsCore(
            IReadOnlyList<GalleryOperation> operations)
        {
            if ((OperationType == typeof(RemoveGalleryOperation))
                || (OperationType == typeof(MixedMutationGalleryOperation)))
            {
                return operations;
            }

            return base.SelectOperationsCore(operations);
        }
    }

    private sealed class ThrowingRunner : GalleryOperationRunnerTestDouble
    {
        public ThrowingRunner(Type operationType)
            : base(operationType)
        {
        }

        protected override Task RunCoreAsync(
            IReadOnlyList<GalleryOperation> operations,
            GalleryOperationCoordinator context,
            CancellationToken ct)
        {
            throw new InvalidOperationException("Runner failed.");
        }
    }
}
