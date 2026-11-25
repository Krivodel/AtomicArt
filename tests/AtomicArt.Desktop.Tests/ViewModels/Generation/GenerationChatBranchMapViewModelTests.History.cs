using System.Text.Json;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

public sealed partial class GenerationChatBranchMapViewModelTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RestoreDeletedPreviewsAsync_AfterRestart_RestoresEachAncestorFromItsOwnTurn(
        bool includesCardTurn, bool groupContext)
    {
        GenerationReference[] images =
        [
            new("root", "root.png", GenerationImageContentTypes.Png),
            new("first", "first.png", GenerationImageContentTypes.Png),
            new("second", "second.png", GenerationImageContentTypes.Png)
        ];
        DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        GenerationChatMessage[] ancestors = images.SelectMany((image, index) => new GenerationChatMessage[]
        {
            new(false, $"Request {index}", timestamp, Array.Empty<GenerationReference>()),
            new(true, $"Reply {index}", timestamp, new GenerationReference[] { image })
        }).ToArray();
        GenerationChatMessage request = new(false, "Current request", timestamp, Array.Empty<GenerationReference>());
        GenerationChatMessage reply = new(true, "Current reply", timestamp, Array.Empty<GenerationReference>());
        GenerationChatHistory saved = new(groupContext ? ancestors.Append(request).ToArray()
            : includesCardTurn ? ancestors.Concat(new GenerationChatMessage[] { request, reply }).ToArray() : ancestors,
            includesCardTurn);
        GenerationChatHistory restored = JsonSerializer.Deserialize<GenerationChatHistory>(JsonSerializer.Serialize(saved))
            ?? throw new InvalidOperationException();
        GenerationChatGroup group = new(UnrelatedId, SecondId, saved.Id, saved.ReferenceHashes)
        {
            AncestorCardIds = new Guid[] { RootId, FirstId, SecondId }
        };
        GenerationItemViewModel current = CreateCard(LeafId, group: group, chatId: groupContext ? null : saved.Id);
        Mock<IGenerationChatStore> chats = new(MockBehavior.Strict);
        chats.Setup(store => store.LoadAsync(saved.Id, It.IsAny<CancellationToken>())).ReturnsAsync(restored);
        Mock<IGenerationReferenceStore> references = new(MockBehavior.Strict);
        references.Setup(store => store.GetExistingFilePathAsync(It.IsAny<GenerationReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GenerationReference image, CancellationToken _) => Path.Combine("History", image.FileName));
        using GenerationChatBranchMapViewModel map = new(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { current }, current.Id, current.Id);

        await map.RestoreDeletedPreviewsAsync(chats.Object, references.Object, CancellationToken.None);

        Guid[] route = [RootId, FirstId, SecondId];

        for (int index = 0; index < route.Length; index++)
        {
            GenerationChatBranchNodeViewModel node = map.Nodes.Single(candidate => candidate.Id == route[index]);
            node.IsDeleted.Should().BeTrue();
            GenerationItemViewModel preview = node.PreviewCard ?? throw new InvalidOperationException();
            preview.Prompt.Should().Be($"Request {index}");
            preview.ImagePath.Should().Be(Path.Combine("History", images[index].FileName));
            preview.ChatResult?.Image.Should().Be(images[index]);
            map.OpenNodeCommand.CanExecute(node).Should().BeFalse();
        }

        chats.Verify(store => store.LoadAsync(saved.Id, It.IsAny<CancellationToken>()), Times.Once);
        map.Nodes.Single(node => node.Id == current.Id).PreviewCard.Should().BeSameAs(current);
        GenerationItemViewModel?[] previews = map.Nodes.Select(node => node.PreviewCard).ToArray();
        map.Refresh(new GenerationItemViewModel[] { current });
        await map.RestoreDeletedPreviewsAsync(chats.Object, references.Object, CancellationToken.None);
        map.Nodes.Select(node => node.PreviewCard).Should().Equal(previews);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RestoreDeletedPreviewsAsync_WithEditedSiblingHistories_PrefersTheOpenChat(bool firstOpen)
    {
        DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        GenerationReference image = new("first", "first.png", GenerationImageContentTypes.Png);
        GenerationItemViewModel first = CreateCard(FirstId, RootId);
        first.ChatHistory = new GenerationChatHistory(new GenerationChatMessage[]
        {
            new(false, "Root in the first branch", timestamp, Array.Empty<GenerationReference>()),
            new(true, "First image", timestamp, new GenerationReference[] { image })
        });
        GenerationItemViewModel second = CreateCard(SecondId, RootId);
        second.ChatHistory = new GenerationChatHistory(new GenerationChatMessage[]
        {
            new(false, "Edited root in the second branch", timestamp, Array.Empty<GenerationReference>()),
            new(true, "Second text reply", timestamp, Array.Empty<GenerationReference>())
        });
        Mock<IGenerationReferenceStore> references = new(MockBehavior.Strict);
        references.Setup(store => store.GetExistingFilePathAsync(image, It.IsAny<CancellationToken>()))
            .ReturnsAsync("History/first.png");
        using GenerationChatBranchMapViewModel map = new(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        GenerationItemViewModel current = firstOpen ? first : second;
        map.Open(new GenerationItemViewModel[] { first, second }, current.Id, current.Id);

        await map.RestoreDeletedPreviewsAsync(Mock.Of<IGenerationChatStore>(), references.Object, CancellationToken.None);

        GenerationItemViewModel preview = map.Nodes.Single(node => node.Id == RootId).PreviewCard
            ?? throw new InvalidOperationException();
        preview.Prompt.Should().Be(current.ChatHistory?.Messages[0].Text);
        preview.ChatResult?.Text.Should().Be(current.ChatHistory?.Messages[1].Text);
        preview.ChatResult?.IsTextReply.Should().Be(!firstOpen);
    }

    [Fact]
    public async Task RestoreDeletedPreviewsAsync_WhenClosedDuringReading_DiscardsTheLateHistory()
    {
        GenerationItemViewModel current = CreateCard(FirstId, RootId, chatId: UnrelatedId);
        TaskCompletionSource<GenerationChatHistory> reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IGenerationChatStore> chats = new();
        chats.Setup(store => store.LoadAsync(UnrelatedId, It.IsAny<CancellationToken>())).Returns(reading.Task);
        Mock<IGenerationReferenceStore> references = new(MockBehavior.Strict);
        using GenerationChatBranchMapViewModel map = new(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { current }, current.Id, current.Id);
        Task restoring = map.RestoreDeletedPreviewsAsync(chats.Object, references.Object, CancellationToken.None);
        map.Close();
        DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

        reading.SetResult(new GenerationChatHistory(new GenerationChatMessage[]
        {
            new(false, "Root request", timestamp, Array.Empty<GenerationReference>()),
            new(true, "Root reply", timestamp, new GenerationReference[]
            {
                new("root", "root.png", GenerationImageContentTypes.Png)
            })
        }));
        await restoring;

        map.IsOpen.Should().BeFalse();
        map.Nodes.Single(node => node.Id == RootId).HasPreview.Should().BeFalse();
        references.VerifyNoOtherCalls();
    }
}
