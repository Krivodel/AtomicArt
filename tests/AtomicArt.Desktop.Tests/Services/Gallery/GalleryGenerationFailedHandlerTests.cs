using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryGenerationFailedHandlerTests
{
    private static readonly Guid CorrelationId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task HandleAsync_WithAssistantReply_PersistsItAndCompletesTheChatAfterSaving()
    {
        GalleryLifecycleViewStateController view = GalleryLifecycleTestFactory.CreateViewStateController(
            GenerationItemStatusDescriptorRegistryTestFactory.Create());
        await view.ApplyStartedAsync(GalleryLifecycleTestFactory.CreateStartedEvent(CorrelationId, 1), CancellationToken.None);
        GenerationChatResult reply = new("The image generator rejected the request.", null);
        GenerationChatRun run = new();
        TaskCompletionSource saving = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource saved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IGalleryStateService> state = new();
        state.Setup(store => store.SaveLatestAsync(It.IsAny<Func<CancellationToken, Task<IReadOnlyList<GalleryItemState>>>>(),
            It.IsAny<CancellationToken>())).Returns(async (Func<CancellationToken, Task<IReadOnlyList<GalleryItemState>>> snapshot, CancellationToken ct) =>
            {
                IReadOnlyList<GalleryItemState> items = await snapshot(ct);
                items.Should().ContainSingle().Which.ChatResult.Should().Be(reply);
                items.Single().Status.Should().Be(GenerationItemStatus.Failed);
                saving.SetResult();
                await saved.Task;
            });
        GalleryGenerationFailedHandler handler = new(view, state.Object);
        GenerationLifecycleEvent failed = GalleryLifecycleTestFactory.CreateFailedEvent(CorrelationId,
            GenerationClientFailureCodes.CodexNoImage) with { ChatResult = reply, ChatRun = run };

        Task handling = handler.HandleAsync(failed, CancellationToken.None);
        await saving.Task;

        run.Completion.IsCompleted.Should().BeFalse();
        saved.SetResult();
        await handling;
        (await run.Completion).Should().BeSameAs(reply);
        GalleryItemState persisted = (await view.CreateStateSnapshotAsync(CancellationToken.None)).Single();
        GenerationItemViewModel restored = GenerationItemViewModel.Restore(persisted, null, null,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(), TestLocalizationTextProvider.Default);
        restored.ChatResult.Should().Be(reply);
        restored.IsFailed.Should().BeTrue();
        restored.FailureCode.Should().Be(GenerationClientFailureCodes.CodexNoImage);
    }

    [Fact]
    public async Task HandleAsync_WhenSavingFails_CompletesTheChatWithItsReply()
    {
        Mock<IGalleryLifecycleViewState> view = new();
        view.Setup(value => value.ApplyFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<GenerationChatResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Mock<IGalleryStateService> state = new();
        state.Setup(store => store.SaveLatestAsync(It.IsAny<Func<CancellationToken, Task<IReadOnlyList<GalleryItemState>>>>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Simulated gallery write failure."));
        GenerationChatRun run = new();
        GenerationChatResult reply = new("Generation failed.", null);
        GenerationLifecycleEvent failed = GalleryLifecycleTestFactory.CreateFailedEvent(CorrelationId) with
        {
            ChatRun = run,
            ChatResult = reply
        };
        GalleryGenerationFailedHandler handler = new(view.Object, state.Object);

        Func<Task> handle = () => handler.HandleAsync(failed, CancellationToken.None);

        await handle.Should().ThrowAsync<IOException>();
        (await run.Completion).Should().BeSameAs(reply);
    }
}
