using System.ComponentModel;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Tests.ViewModels.Generation;
using AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SubmitAsync_WithTwoAcceptedClicks_QueuesBothInTheOriginalGroup(int firstCount)
    {
        using ChatContext context = new();
        await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
        ImageModelOption model = LocalCodexImageModel.Option;
        NanoBanana2GenerationParameters parameters = new(model, model.DisplayName, "A queued prompt", "auto", "auto",
            0d, firstCount, Array.Empty<AttachedImageDto>());
        TaskCompletionSource admitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<GenerationChatBatch> batches = [];
        Task first = context.Chat.SubmitAsync(parameters, (sent, _) =>
        {
            batches.Add(sent.ChatBatch ?? throw new InvalidOperationException());
            return admitted.Task;
        }, CancellationToken.None);
        Task second = context.Chat.SubmitAsync(parameters with { GenerationCount = 2 }, (sent, _) =>
        {
            batches.Add(sent.ChatBatch ?? throw new InvalidOperationException());
            return Task.CompletedTask;
        }, CancellationToken.None);

        second.IsCompleted.Should().BeFalse();

        foreach (GenerationChatRun run in batches[0].Runs)
        {
            await GenerationChatViewModelTests.WaitForBusyChangeAsync(context.Chat, () => run.Complete(null));
        }

        admitted.SetResult();
        await Task.WhenAll(first, second);

        batches.Should().HaveCount(2);
        batches[1].History.Should().BeSameAs(batches[0].History);
        context.Chat.Messages[^1].Images.Should().HaveCount(firstCount + 2);

        foreach (GenerationChatRun run in batches[1].Runs)
        {
            run.Complete(null);
        }
    }

    [Fact]
    public async Task SubmitAsync_WithRepeatedReference_UsesOriginalBytesAndPrompt()
    {
        using ChatContext context = new();
        await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
        ImageModelOption model = LocalCodexImageModel.Option;
        AttachedImageDto image = new("original.png", "image/png", GalleryThumbnailTestImages.CreatePngBytes(16, 16));
        NanoBanana2GenerationParameters parameters = new(model, model.DisplayName, "Original prompt", "auto", "auto",
            0d, 2, new[] { image });
        GenerationChatBatch first = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat, parameters);
        context.References.Setup(store => store.LoadAsync(first.UserMessage.Images[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(image);
        NanoBanana2GenerationParameters? repeated = null;

        GenerationChatBatch next = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat,
            parameters with { Prompt = "Unrelated draft", AttachedImages = Array.Empty<AttachedImageDto>() },
            submitted => repeated = submitted);

        NanoBanana2GenerationParameters sent = repeated ?? throw new InvalidOperationException();
        sent.Prompt.Should().Be("Original prompt");
        sent.AttachedImages.Should().ContainSingle().Which.Content.Should().Equal(image.Content);
        next.UserMessage.Images.Should().Equal(first.UserMessage.Images);
        next.History.Should().BeSameAs(first.History);
        context.References.Verify(store => store.SaveAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<AttachedImageDto>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        foreach (GenerationChatRun run in first.Runs.Concat(next.Runs))
        {
            run.Complete(null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmitAsync_WithExistingVariants_AppendsToTheSameReplyAndContext(bool completed)
    {
        using ChatContext context = new();
        await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
        GenerationChatBatch first = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        GenerationChatMessageViewModel reply = context.Chat.Messages[^1];
        GenerationChatImageViewModel[] originals = reply.Images.ToArray();
        int messageCount = context.Chat.Messages.Count;

        if (completed)
        {
            foreach (GenerationChatRun run in first.Runs)
            {
                await GenerationChatViewModelTests.WaitForBusyChangeAsync(context.Chat, () => run.Complete(null));
            }
        }

        GenerationChatBatch next = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);

        context.Chat.Messages.Should().HaveCount(messageCount);
        context.Chat.Messages[^1].Should().BeSameAs(reply);
        reply.Images.Should().HaveCount(4);
        reply.Images.Take(2).Should().Equal(originals);
        next.History.Should().BeSameAs(first.History);
        next.UserMessage.Should().BeSameAs(first.UserMessage);
        reply.Images.Skip(2).Should().OnlyContain(image => image.IsPending);
        context.Chat.CanAddVariants.Should().BeTrue();
        context.Chat.CanSubmit.Should().BeTrue();

        foreach (GenerationChatRun run in first.Runs.Concat(next.Runs))
        {
            run.Complete(null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenCommand_AfterAnotherChat_RestoresVariantsAndTheirLateResults(bool completeBeforeLeaving)
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        GenerationChatBatch first = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        GenerationChatMessageViewModel originalReply = context.Chat.Messages[^1];
        GenerationChatResult result = context.AddResultCard(first, "The first chat result");

        if (completeBeforeLeaving)
        {
            await GenerationChatViewModelTests.CompleteVariantAsync(originalReply.Images[0], () => first.Runs[0].Complete(result));
        }

        GenerationItemViewModel other = context.AddSourceCard("Another chat");
        await context.Chat.OpenCommand.ExecuteAsync(other);
        GenerationChatBatch second = await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        GenerationChatMessageViewModel otherReply = context.Chat.Messages[^1];

        if (!completeBeforeLeaving)
        {
            await GenerationChatViewModelTests.CompleteVariantAsync(originalReply.Images[0], () => first.Runs[0].Complete(result));
        }

        otherReply.Images.Should().OnlyContain(image => image.IsPending);
        await context.Chat.OpenCommand.ExecuteAsync(source);

        context.Chat.Messages[^1].Should().NotBeSameAs(originalReply);
        await context.Chat.OpenGroupCommand.ExecuteAsync(context.Chat.Messages[^1].Groups.Single());
        context.Chat.Messages[^1].Should().BeSameAs(originalReply);
        originalReply.Images.Should().HaveCount(2);
        originalReply.Images[0].Reference.Should().Be(result.Image);
        originalReply.Images[0].CanChoose.Should().BeTrue();
        originalReply.Images[1].IsPending.Should().BeTrue();
        await context.Chat.OpenCommand.ExecuteAsync(other);
        await context.Chat.OpenGroupCommand.ExecuteAsync(context.Chat.Messages[^1].Groups.Single());
        context.Chat.Messages[^1].Should().BeSameAs(otherReply);

        foreach (GenerationChatRun run in first.Runs.Concat(second.Runs))
        {
            run.Complete(null);
        }
    }

    [Fact]
    public async Task GenerateCommand_WithUnselectedVariants_LocksPromptAndAddsRequestsUntilSelection()
    {
        using ChatContext context = new();
        await context.Chat.OpenCommand.ExecuteAsync(await context.RestoreSourceAsync());
        List<GenerationRunRequest> requests = [];
        Mock<IGenerationRunDispatcher> dispatcher = new();
        dispatcher.Setup(service => service.EnqueueAsync(It.IsAny<GenerationRunRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GenerationRunRequest, CancellationToken>((request, _) => requests.Add(request))
            .Returns(Task.CompletedTask);
        LocalCodexSettings settings = new();
        using UniversalNanoBananaPanelViewModel panel = UniversalNanoBananaPanelViewModelTests.CreateViewModel(
            imageModelOptionCatalog: new ImageModelOptionCatalog(settings), dispatcher: dispatcher.Object, localCodexSettings: settings);
        panel.ConnectChat(context.Chat);
        panel.Prompt = "The fixed prompt";
        panel.GenerationCount = 2;

        await panel.GenerateCommand.ExecuteAsync(null);

        panel.IsChatPromptLocked.Should().BeTrue();
        panel.Prompt.Should().Be("The fixed prompt");
        panel.Prompt = "A different prompt";
        panel.Prompt.Should().Be("The fixed prompt");
        panel.ReplacePromptCommand.CanExecute("A different prompt").Should().BeFalse();
        panel.GenerateCommand.CanExecute(null).Should().BeTrue();
        await panel.GenerateCommand.ExecuteAsync(null);
        requests.Should().HaveCount(4).And.AllSatisfy(request => request.Request.Prompt.Should().Be("The fixed prompt"));
        context.Chat.Messages[^1].Images.Should().HaveCount(4);
        GenerationChatHistory history = requests[0].StartSnapshot.ChatHistory ?? throw new InvalidOperationException();
        GenerationChatRun firstRun = requests[0].ChatRun ?? throw new InvalidOperationException();
        GenerationChatBatch batch = new(history, new GenerationChatMessage(false, "The fixed prompt", new DateTime(2026, 11, 24),
            Array.Empty<GenerationReference>()), new[] { firstRun });
        GenerationChatResult result = context.AddResultCard(batch, "A chosen result");
        await GenerationChatViewModelTests.WaitForBusyChangeAsync(context.Chat, () => firstRun.Complete(result));
        GenerationChatImageViewModel chosen = context.Chat.Messages[^1].Images[0];

        await context.Chat.ChooseVariantCommand.ExecuteAsync(chosen);

        panel.IsChatPromptLocked.Should().BeFalse();
        panel.Prompt = "The next change";
        panel.Prompt.Should().Be("The next change");

        foreach (GenerationRunRequest request in requests)
        {
            request.ChatRun?.Complete(null);
        }
    }

    private static async Task CompleteVariantAsync(GenerationChatImageViewModel image, Action complete)
    {
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (image.CanChoose)
            {
                ready.TrySetResult();
            }
        }

        image.PropertyChanged += OnChanged;

        try
        {
            complete();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            image.PropertyChanged -= OnChanged;
        }
    }
}
