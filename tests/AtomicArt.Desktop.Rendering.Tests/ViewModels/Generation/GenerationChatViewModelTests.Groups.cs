using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Generation;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Fact]
    public async Task ChatView_WithLongGroupPrompts_KeepsBothCountersVisibleInsideTheButtons()
    {
        await DesktopControlTestBase.DispatchAsync(typeof(GenerationChatViewModelTests), async () =>
        {
            using ChatContext context = new();
            await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
            AtomicArt.Desktop.Services.ImageModelOption model = LocalCodexImageModel.Option;
            NanoBanana2GenerationParameters parameters = new(model, model.DisplayName, new string('x', 200), "auto", "auto", 0d, 2,
                Array.Empty<AtomicArt.Contracts.Generation.AttachedImageDto>());
            GenerationChatBatch first = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat, parameters);
            await context.Chat.ReturnToSourceCommand.ExecuteAsync(null);
            GenerationChatBatch second = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat, parameters);
            await context.Chat.ReturnToSourceCommand.ExecuteAsync(null);
            GenerationChatView view = new() { DataContext = context.Chat };
            Window window = DesktopControlTestBase.Show(view, 420d, 500d);

            try
            {
                await GenerationChatViewModelTests.WaitForFramesAsync(window, () => view.IsPresented);
                Button[] links = view.GetVisualDescendants().OfType<Button>()
                    .Where(button => object.ReferenceEquals(button.Command, context.Chat.OpenGroupCommand)).ToArray();
                links.Should().HaveCount(2);

                foreach (Button button in links)
                {
                    TextBlock counter = button.GetVisualDescendants().OfType<TextBlock>()
                        .Single(text => text.Text?.Contains("variants", StringComparison.Ordinal) == true);
                    Point right = counter.TranslatePoint(new Point(counter.Bounds.Width, 0d), button)
                        ?? throw new InvalidOperationException("The variant counter has no position.");
                    right.X.Should().BeLessThanOrEqualTo(button.Bounds.Width);
                    counter.Bounds.Width.Should().BeGreaterThan(0d);
                    button.Command?.CanExecute(button.CommandParameter).Should().BeTrue();
                }

                GenerationChatViewModelTests.SaveChatFrame(window, "chat-groups.png");
            }
            finally
            {
                window.Close();

                foreach (GenerationChatRun run in first.Runs.Concat(second.Runs))
                {
                    run.Complete(null);
                }
            }
        });
    }

    [Fact]
    public async Task SubmitAsync_WithReferenceStorageFailure_ReturnsToTheEditableOriginal()
    {
        using ChatContext context = new();
        await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
        GenerationChatMessageViewModel[] original = context.Chat.Messages.ToArray();
        context.References.Setup(store => store.SaveAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<AtomicArt.Contracts.Generation.AttachedImageDto>>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Simulated image storage failure."));

        Func<Task> submit = () => GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);

        await submit.Should().ThrowAsync<IOException>();
        context.Chat.Messages.Should().Equal(original);
        context.Chat.CanAddVariants.Should().BeFalse();
        context.Chat.Messages.Should().OnlyContain(message => message.CanEdit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmitAsync_WithSingleResult_ContinuesFromItsCardAndPreservesTheOriginal(bool textOnly)
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        AtomicArt.Desktop.Services.ImageModelOption model = LocalCodexImageModel.Option;
        NanoBanana2GenerationParameters parameters = new(model, model.DisplayName, "Single change", "auto", "auto", 0d, 1,
            Array.Empty<AtomicArt.Contracts.Generation.AttachedImageDto>());
        GenerationChatBatch batch = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat, parameters);
        GenerationChatResult result = context.AddResultCard(batch, "Single result", textOnly: textOnly);

        await GenerationChatViewModelTests.WaitForBusyChangeAsync(context.Chat, () => batch.Runs[0].Complete(result));
        await AtomicArt.Tests.Common.AsyncTestWaiter.WaitForConditionAsync(
            () => context.Chat.ChooseVariantCommand.ExecutionTask is not null, CancellationToken.None);

        if (context.Chat.ChooseVariantCommand.ExecutionTask is { } opening)
        {
            await opening;
        }

        context.Chat.CanAddVariants.Should().BeFalse();
        context.Chat.Messages[^1].Text.Should().Be(result.Text);
        context.Chat.Messages.Should().OnlyContain(message => message.CanEdit);
        GenerationChatBatch next = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        next.Group?.SourceCardId.Should().Be(batch.Runs[0].Id);
        next.Group?.AncestorCardIds.Should().Equal(source.Id, batch.Runs[0].Id);
        await context.Chat.OpenCommand.ExecuteAsync(source);
        context.Chat.Messages.Should().HaveCount(2);

        foreach (GenerationChatRun run in next.Runs)
        {
            run.Complete(null);
        }
    }

    [Fact]
    public async Task ReturnToSourceCommand_WithTwoGroups_PreservesEachRequestAndItsLateResults()
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        string[] originalText = context.Chat.Messages.Select(message => message.Text).ToArray();
        GenerationChatBatch first = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        GenerationChatMessageViewModel firstReply = context.Chat.Messages[^1];

        context.Chat.Messages.Should().OnlyContain(message => !message.CanEdit);
        context.Chat.CanReturnToSource.Should().BeTrue();
        await context.Chat.ReturnToSourceCommand.ExecuteAsync(null);
        context.Chat.Messages.Select(message => message.Text).Should().Equal(originalText);
        context.Chat.Messages.Should().OnlyContain(message => message.CanEdit);
        context.Chat.CanAddVariants.Should().BeFalse();
        context.Chat.Messages[^1].Groups.Should().ContainSingle();

        GenerationChatBatch second = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        GenerationChatResult result = context.AddResultCard(first, "A late result from the first group");
        await GenerationChatViewModelTests.CompleteVariantAsync(firstReply.Images[0], () => first.Runs[0].Complete(result));

        second.Group.Should().NotBeNull();
        second.Group?.Id.Should().NotBe(first.Group?.Id ?? Guid.Empty);
        second.History.Messages.Should().BeEquivalentTo(first.History.Messages);
        context.Chat.Messages[^1].Images.Should().OnlyContain(image => image.IsPending);
        await context.Chat.ReturnToSourceCommand.ExecuteAsync(null);
        context.Chat.Messages[^1].Groups.Should().HaveCount(2);
        context.Chat.Messages[^1].Groups.Select(link => link.Group.Id).Should()
            .Equal(first.Group?.Id ?? Guid.Empty, second.Group?.Id ?? Guid.Empty);
        context.AddResultCard(second, "A result from the second group");
        context.Chat.Messages[^1].Groups.Select(link => link.Group.Id).Should()
            .Equal(first.Group?.Id ?? Guid.Empty, second.Group?.Id ?? Guid.Empty);
        await context.Chat.OpenGroupCommand.ExecuteAsync(context.Chat.Messages[^1].Groups.Single(link => link.Group.Id == first.Group?.Id));

        context.Chat.Messages[^1].Should().BeSameAs(firstReply);
        firstReply.Images[0].Reference.Should().Be(result.Image);
        GenerationChatBatch repeated = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        repeated.Group.Should().BeSameAs(first.Group);
        repeated.UserMessage.Should().BeSameAs(first.UserMessage);
        repeated.History.Should().BeSameAs(first.History);
        firstReply.Images.Should().HaveCount(4);

        foreach (GenerationChatRun run in first.Runs.Concat(second.Runs).Concat(repeated.Runs))
        {
            run.Complete(null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteOrCancelCommand_WithVariants_UpdatesTheGroupAndReturnsAfterTheLastDeletion(bool groupOpen)
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        GenerationChatBatch batch = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        GenerationChatMessageViewModel reply = context.Chat.Messages[^1];
        GenerationChatResult first = context.AddResultCard(batch, "First");
        GenerationChatResult second = context.AddResultCard(batch, "Second", index: 1);
        await GenerationChatViewModelTests.CompleteVariantAsync(reply.Images[0], () => batch.Runs[0].Complete(first));
        await GenerationChatViewModelTests.CompleteVariantAsync(reply.Images[1], () => batch.Runs[1].Complete(second));

        if (!groupOpen)
        {
            await context.Chat.ReturnToSourceCommand.ExecuteAsync(null);
        }

        await context.Gallery.DeleteOrCancelCommand.ExecuteAsync(context.Gallery.Items.Single(card => card.Id == batch.Runs[0].Id));

        reply.Images.Should().ContainSingle().Which.Id.Should().Be(batch.Runs[1].Id);
        context.Chat.CanReturnToSource.Should().Be(groupOpen);

        if (!groupOpen)
        {
            context.Chat.Messages[^1].Groups.Should().ContainSingle().Which.Label.Should().Contain("1 variant");
            await context.Chat.OpenGroupCommand.ExecuteAsync(context.Chat.Messages[^1].Groups.Single());
        }

        await context.Gallery.DeleteOrCancelCommand.ExecuteAsync(context.Gallery.Items.Single(card => card.Id == batch.Runs[1].Id));

        context.Chat.CanReturnToSource.Should().BeFalse();
        context.Chat.Messages[^1].Groups.Should().BeEmpty();
        context.Chat.Messages.Should().OnlyContain(message => message.CanEdit);
    }

    [Fact]
    public async Task OpenGroupCommand_AfterRestart_UsesItsFrozenContextAndSurvivingCards()
    {
        using ChatContext context = new();
        DateTime time = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        Guid sourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        GenerationChatHistory snapshot = new(new GenerationChatMessage[]
        {
            new GenerationChatMessage(false, "Original context", time, Array.Empty<GenerationReference>()),
            new GenerationChatMessage(true, "Original answer", time, Array.Empty<GenerationReference>()),
            new GenerationChatMessage(false, "Frozen request", time, Array.Empty<GenerationReference>())
        });
        GenerationChatGroup group = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), sourceId,
            snapshot.Id, snapshot.ReferenceHashes);
        context.Chats.Setup(store => store.LoadAsync(snapshot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        GenerationChatResult result = context.CreateResult("Saved result");
        GalleryItemState source = GalleryItemStateTestFactory.CreateGenerated(id: sourceId, modelId: LocalCodexImageModel.Id,
            imagePath: null, prompt: "Edited context");
        GalleryItemState variant = GalleryItemStateTestFactory.CreateGenerated(modelId: LocalCodexImageModel.Id,
            imagePath: null, prompt: "Frozen request", chatGroup: group, chatResult: result);
        await context.Gallery.RestoreStateAsync(new GalleryItemState[] { source, variant }, CancellationToken.None);
        await context.Chat.OpenCommand.ExecuteAsync(context.Gallery.Items.Single(card => card.Id == sourceId));

        await context.Chat.OpenGroupCommand.ExecuteAsync(context.Chat.Messages[^1].Groups.Single());
        GenerationChatImageViewModel image = context.Chat.Messages[^1].Images.Single();
        await AtomicArt.Tests.Common.AsyncTestWaiter.WaitForConditionAsync(() => image.CanChoose, CancellationToken.None);

        context.Chat.Messages[0].Text.Should().Be("Original context");
        context.Chat.Messages.Should().OnlyContain(message => !message.CanEdit);
        context.Chat.RepeatPrompt.Should().Be("Frozen request");
        image.Reference.Should().Be(result.Image);
        context.Chat.CanReturnToSource.Should().BeTrue();
        GenerationChatBatch repeated = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        repeated.History.Messages.Select(message => message.Text).Should().Equal("Original context", "Original answer");
        repeated.UserMessage.Text.Should().Be("Frozen request");
        repeated.Group?.Id.Should().Be(group.Id);

        foreach (GenerationChatRun run in repeated.Runs)
        {
            run.Complete(null);
        }
    }
}
