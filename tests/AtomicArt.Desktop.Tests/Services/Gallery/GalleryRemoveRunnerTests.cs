using Microsoft.Extensions.Logging.Abstractions;

using Avalonia.Controls;
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
        TestUiFrameScheduler frameScheduler = new();
        GalleryAnimationScheduler animationScheduler = new(frameScheduler);
        GalleryLayoutService layout = new();
        GalleryMotionAnimator animator = GalleryMotionAnimatorTestFactory.Create(
            animationScheduler,
            new GalleryOverlayEffects(animationScheduler),
            layout);
        GalleryRemoveRunner runner = new(animationScheduler, animator, layout, NullLogger<GalleryRemoveRunner>.Instance);
        Guid itemId = Guid.NewGuid();
        List<object> items = [itemId];
        GalleryOperationCoordinator context = CreateContext(frameScheduler, items);
        GalleryOperation operation = new RemoveGalleryOperation(itemId);
        layout.RenderCards(context);

        await runner.RunAsync(new List<GalleryOperation> { operation }, context, CancellationToken.None);

        context.OverlayCanvas.Children.Should().ContainSingle();
        operation.Completion.Task.IsCompletedSuccessfully.Should().BeTrue();

        frameScheduler.RunNextFrame(TimeSpan.Zero);
        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(519d));

        context.OverlayCanvas.Children.Should().ContainSingle();

        frameScheduler.RunNextFrame(TimeSpan.FromMilliseconds(520d));

        context.OverlayCanvas.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenCancelledBeforeRemoval_CancelsOperation()
    {
        TestUiFrameScheduler frameScheduler = new();
        GalleryAnimationScheduler animationScheduler = new(frameScheduler);
        GalleryLayoutService layout = new();
        GalleryMotionAnimator animator = GalleryMotionAnimatorTestFactory.Create(
            animationScheduler,
            new GalleryOverlayEffects(animationScheduler),
            layout);
        GalleryRemoveRunner runner = new(animationScheduler, animator, layout, NullLogger<GalleryRemoveRunner>.Instance);
        Guid itemId = Guid.NewGuid();
        List<object> items = [itemId];
        GalleryOperationCoordinator context = CreateContext(frameScheduler, items);
        GalleryOperation operation = new RemoveGalleryOperation(itemId);
        CancellationTokenSource cancellationTokenSource = new();
        layout.RenderCards(context);
        cancellationTokenSource.Cancel();

        await runner.RunAsync(new List<GalleryOperation> { operation }, context, cancellationTokenSource.Token);

        context.OverlayCanvas.Children.Should().BeEmpty();
        operation.Completion.Task.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenRemovalAnimationFails_ClearsOverlayCanvasAndFailsOperation()
    {
        TestUiFrameScheduler frameScheduler = new();
        GalleryAnimationScheduler animationScheduler = new(
            frameScheduler,
            (_, _) =>
            {
                throw new InvalidOperationException("Frame application failed.");
            });
        GalleryLayoutService layout = new();
        GalleryMotionAnimator animator = GalleryMotionAnimatorTestFactory.Create(
            animationScheduler,
            new GalleryOverlayEffects(animationScheduler),
            layout);
        GalleryRemoveRunner runner = new(animationScheduler, animator, layout, NullLogger<GalleryRemoveRunner>.Instance);
        Guid itemId = Guid.NewGuid();
        List<object> items = [itemId];
        GalleryOperationCoordinator context = CreateContext(frameScheduler, items);
        GalleryOperation operation = new RemoveGalleryOperation(itemId);
        layout.RenderCards(context);

        await runner.RunAsync(new List<GalleryOperation> { operation }, context, CancellationToken.None);

        context.OverlayCanvas.Children.Should().BeEmpty();
        operation.Completion.Task.IsFaulted.Should().BeTrue();
    }

    private static GalleryOperationCoordinator CreateContext(
        TestUiFrameScheduler frameScheduler,
        IList<object> items)
    {
        GalleryOperationCoordinator context = GalleryOperationCoordinatorTestFactory.Create(
            frameScheduler,
            new GalleryOperationRunnerRegistry(new List<IGalleryOperationRunner>()));
        context.AttachScene(
            new ScrollViewer(),
            new Canvas(),
            new Canvas(),
            items,
            item => (Guid)item,
            _ => new Border(),
            () => Task.CompletedTask);

        return context;
    }
}
