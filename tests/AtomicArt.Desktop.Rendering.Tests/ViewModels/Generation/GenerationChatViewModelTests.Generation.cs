using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Tests.ViewModels.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Fact]
    public async Task ChooseVariant_WithTextAndImageReplies_ContinuesTheSelectedTextAndPreservesTheOriginalImageCall()
    {
        using ChatContext context = new();
        GenerationChatImageCall call = new("first", "Original generator prompt", false, "g1");
        GenerationItemViewModel source = await context.RestoreSourceAsync(context.CreateResult("Image") with { ImageGeneration = call });
        await context.Chat.OpenCommand.ExecuteAsync(source);
        GenerationChatBatch batch = await SubmitParallelAsync(context.Chat);
        GenerationChatMessageViewModel variants = context.Chat.Messages[^1];
        GenerationChatResult image = context.AddResultCard(batch, "Image variant");
        GenerationChatResult text = context.AddResultCard(batch, "Text variant", index: 1, textOnly: true);
        await CompleteVariantAsync(variants.Images[0], () => batch.Runs[0].Complete(image));
        await CompleteVariantAsync(variants.Images[1], () => batch.Runs[1].Complete(text));

        variants.Images[1].TextReply.Should().Be(text.Text);
        await context.Chat.ChooseVariantCommand.ExecuteAsync(variants.Images[1]);

        GenerationChatMessageViewModel reply = context.Chat.Messages[^1];
        reply.Images.Should().BeEmpty();
        reply.Text.Should().Be(text.Text);
        reply.EditCommand.Execute(null);
        reply.EditText = "Edited text variant";
        await reply.SaveCommand.ExecuteAsync(null);
        variants.Images[1].TextReply.Should().Be(reply.Text);
        context.Chat.OpenBranchMapCommand.Execute(null);
        GenerationItemViewModel selected = Assert.IsType<GenerationItemViewModel>(context.Chat.BranchMap.Nodes.Single(node => node.IsCurrent).Card);
        selected.TextReplyPreview.Should().Be(reply.Text);
        context.Chat.BranchMap.CloseCommand.Execute(null);
        GenerationChatBatch next = await SubmitParallelAsync(context.Chat);
        next.History.Messages[^1].Text.Should().Be(reply.Text);
        next.History.Messages[^1].Images.Should().BeEmpty();
        next.History.Messages.Should().ContainSingle(message => message.ImageGeneration == call);

        foreach (GenerationChatRun run in next.Runs)
        {
            run.Complete(null);
        }
    }

    [Theory]
    [InlineData(true, "Generated")]
    [InlineData(false, "Generated")]
    [InlineData(true, "")]
    [InlineData(false, "")]
    public async Task CompleteImage_WithMixedParallelResults_KeepsTheSuccessfulReply(bool refusalFirst, string successfulText)
    {
        using ChatContext context = new();
        GenerationChatHistory history = new(new GenerationChatMessage[]
        {
            new GenerationChatMessage(true, string.Empty, new DateTime(2026, 11, 24), Array.Empty<GenerationReference>())
        }, true);
        GenerationItemViewModel source = await context.RestoreSavedChatAsync(history);
        await context.Chat.OpenCommand.ExecuteAsync(source);
        GenerationChatMessageViewModel reply = context.Chat.Messages.Single();
        Guid failedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid successfulId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        reply.AddPendingImage(failedId);
        reply.AddPendingImage(successfulId);
        GenerationChatResult failure = new("The image generator rejected the request.", null);
        GenerationChatResult success = context.CreateResult(successfulText);

        if (refusalFirst)
        {
            reply.CompleteImage(failedId, failure);
            reply.Text.Should().Be(failure.Text);
            reply.CompleteImage(successfulId, success);
        }
        else
        {
            reply.CompleteImage(successfulId, success);
            reply.CompleteImage(failedId, failure);
        }

        reply.Text.Should().Be(successfulText);
        reply.Images.Should().ContainSingle(image => image.Reference == success.Image);
        reply.Images.Should().ContainSingle(image => image.IsFailed);
        reply.HasPendingResults.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task GenerateCommand_WithChat_ContinuesOnlyExpandedHistory(bool expanded, bool pending)
    {
        using ChatContext context = new();
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);

        if (pending)
        {
            await GenerationChatViewModelTests.SubmitParallelAsync(context.Chat);
        }

        if (!expanded)
        {
            context.Chat.Collapse();
        }

        Mock<IGenerationRunDispatcher> dispatcher = new();
        GenerationRunRequest? captured = null;
        dispatcher.Setup(service => service.EnqueueAsync(It.IsAny<GenerationRunRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GenerationRunRequest, CancellationToken>((request, _) => captured = request)
            .Returns(Task.CompletedTask);
        LocalCodexSettings settings = new();
        using UniversalNanoBananaPanelViewModel panel = UniversalNanoBananaPanelViewModelTests.CreateViewModel(
            imageModelOptionCatalog: new ImageModelOptionCatalog(settings), dispatcher: dispatcher.Object,
            localCodexSettings: settings);
        panel.ConnectChat(context.Chat);
        panel.Prompt = "A new image";

        panel.GenerateCommand.CanExecute(null).Should().BeTrue();
        await panel.GenerateCommand.ExecuteAsync(null);

        GenerationRunRequest request = captured ?? throw new InvalidOperationException(panel.ErrorMessage);
        (request.ChatRun is not null).Should().Be(expanded);
        (request.StartSnapshot.ChatHistory is not null).Should().Be(expanded);
        context.Chat.HasChat.Should().Be(expanded);
        panel.Prompt.Should().Be("A new image");
    }

    [Fact]
    public async Task OpenCommand_WithMessagesAcrossDates_ShowsOneSeparatorPerLocalDay()
    {
        using ChatContext context = new();
        GenerationChatMessage[] messages =
        [
            new GenerationChatMessage(false, "Old", new DateTime(2025, 11, 24), Array.Empty<GenerationReference>()),
            new GenerationChatMessage(true, "Same day", new DateTime(2025, 11, 24, 15, 0, 0), Array.Empty<GenerationReference>()),
            new GenerationChatMessage(false, "Today", new DateTime(2026, 11, 24), Array.Empty<GenerationReference>())
        ];
        GenerationItemViewModel source = await context.RestoreSavedChatAsync(new GenerationChatHistory(messages, includesCardTurn: true));

        await context.Chat.OpenCommand.ExecuteAsync(source);

        context.Chat.Messages.Select(message => message.DateLabel).Should().Equal("November 24, 2025", null, "November 24");
    }
}
