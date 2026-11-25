using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Rendering.Tests.ViewModels.Generation;

public sealed partial class GenerationChatViewModelTests
{
    [Fact]
    public async Task SaveMessage_WithTheFirstRequest_UpdatesGalleryTreeAndSavedState()
    {
        Mock<IGalleryStateService> state = new();
        IReadOnlyList<GalleryItemState> saved = Array.Empty<GalleryItemState>();
        state.Setup(store => store.SaveLatestAsync(It.IsAny<Func<CancellationToken, Task<IReadOnlyList<GalleryItemState>>>>(),
            It.IsAny<CancellationToken>())).Returns(async (Func<CancellationToken, Task<IReadOnlyList<GalleryItemState>>> snapshot,
                CancellationToken ct) => saved = await snapshot(ct));
        using ChatContext context = new(state.Object);
        GenerationItemViewModel source = await context.RestoreSourceAsync();
        await context.Chat.OpenCommand.ExecuteAsync(source);
        GenerationChatMessageViewModel message = context.Chat.Messages[0];
        message.EditCommand.Execute(null);
        message.EditText = "Edited first request";

        await message.SaveCommand.ExecuteAsync(null);
        context.Chat.OpenBranchMapCommand.Execute(null);

        source.Prompt.Should().Be("Edited first request");
        saved.Single().Prompt.Should().Be(source.Prompt);
        context.Chat.BranchMap.Nodes.Single().Card?.Prompt.Should().Be(source.Prompt);
    }

    [Theory]
    [InlineData(0, "Current request")]
    [InlineData(2, "Edited request")]
    public async Task SaveMessage_WithAnEditedUserTurn_UpdatesOnlyTheMatchingCardPrompt(int index, string expected)
    {
        using ChatContext context = new();
        GenerationChatHistory history = CreatePromptHistory();
        GenerationItemViewModel source = await context.RestoreSavedChatAsync(history);
        source.Prompt = "Current request";
        await context.Chat.OpenCommand.ExecuteAsync(source);
        GenerationChatMessageViewModel message = context.Chat.Messages[index];
        message.EditCommand.Execute(null);
        message.EditText = "Edited request";

        await message.SaveCommand.ExecuteAsync(null);
        context.Chat.OpenBranchMapCommand.Execute(null);

        source.Prompt.Should().Be(expected);
        source.ChatHistory?.Messages[index].Text.Should().Be("Edited request");
        context.Chat.BranchMap.Nodes.Single().Card?.Prompt.Should().Be(expected);
    }

    [Fact]
    public async Task SaveMessage_WhenStorageFails_RestoresTheHistoryAndCardPrompt()
    {
        Mock<IGalleryStateService> state = new();
        state.Setup(store => store.SaveLatestAsync(It.IsAny<Func<CancellationToken, Task<IReadOnlyList<GalleryItemState>>>>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Simulated storage failure."));
        using ChatContext context = new(state.Object);
        GenerationItemViewModel source = await context.RestoreSavedChatAsync(CreatePromptHistory());
        source.Prompt = "Current request";
        await context.Chat.OpenCommand.ExecuteAsync(source);
        GenerationChatHistory? previous = source.ChatHistory;
        GenerationChatMessageViewModel message = context.Chat.Messages[2];
        message.EditCommand.Execute(null);
        message.EditText = "Edited request";

        await message.SaveCommand.ExecuteAsync(null);

        source.ChatHistory.Should().BeSameAs(previous);
        source.Prompt.Should().Be("Current request");
        message.Text.Should().Be("Current request");
        message.IsEditing.Should().BeTrue();
    }

    private static GenerationChatHistory CreatePromptHistory()
    {
        DateTime date = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        GenerationChatMessage[] messages =
        [
            new(false, "First request", date, Array.Empty<GenerationReference>()),
            new(true, "First reply", date, Array.Empty<GenerationReference>()),
            new(false, "Current request", date, Array.Empty<GenerationReference>()),
            new(true, "Current reply", date, Array.Empty<GenerationReference>())
        ];

        return new GenerationChatHistory(messages, includesCardTurn: true);
    }
}
