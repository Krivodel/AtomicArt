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
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RestoreDeletedPreviewsAsync_WithSavedTurnIds_RestoresEveryHistoricalTurn(int historySource)
    {
        DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        Guid[] ancestorsIds = [RootId, FirstId, Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Guid.Parse("77777777-7777-7777-7777-777777777777"), SecondId];
        GenerationChatMessage[] ancestors = Enumerable.Range(0, 5).SelectMany(index => new GenerationChatMessage[]
        {
            new(false, $"Historical request {index}", timestamp, Array.Empty<GenerationReference>()),
            new(true, $"Historical reply {index}", timestamp, index == 2
                ? Array.Empty<GenerationReference>()
                : new GenerationReference[] { new($"image-{index}", $"image-{index}.png", GenerationImageContentTypes.Png) })
            { SourceCardId = ancestorsIds[index] }
        }).ToArray();
        GenerationChatMessage request = new(false, "Current request", timestamp.AddMinutes(5), Array.Empty<GenerationReference>());
        GenerationChatMessage reply = new(true, "Current reply", timestamp.AddMinutes(5).AddSeconds(20), Array.Empty<GenerationReference>());
        GenerationChatHistory saved = new(historySource == 3 ? ancestors.Append(request).ToArray()
            : historySource == 2 ? ancestors.Concat(new GenerationChatMessage[] { request, reply }).ToArray() : ancestors,
            includesCardTurn: historySource == 2);
        GenerationChatGroup group = new(UnrelatedId, SecondId, saved.Id, saved.ReferenceHashes);
        GenerationItemViewModel current = CreateCard(LeafId, group: group, chatId: historySource == 3 ? null : saved.Id);

        if (historySource == 0)
        {
            current.ChatHistory = saved;
        }

        Mock<IGenerationChatStore> chats = new(MockBehavior.Strict);
        GenerationChatHistory restoredHistory = JsonSerializer.Deserialize<GenerationChatHistory>(JsonSerializer.Serialize(saved))
            ?? throw new InvalidOperationException("The saved history is missing.");
        chats.Setup(store => store.LoadAsync(saved.Id, It.IsAny<CancellationToken>())).ReturnsAsync(restoredHistory);
        Mock<IGenerationReferenceStore> references = new(MockBehavior.Strict);
        references.Setup(store => store.GetExistingFilePathAsync(It.IsAny<GenerationReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GenerationReference image, CancellationToken _) => Path.Combine("History", image.FileName));
        using GenerationChatBranchMapViewModel map = new(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { current }, current.Id, current.Id);

        await map.RestoreDeletedPreviewsAsync(chats.Object, references.Object, CancellationToken.None);

        map.Nodes.Should().HaveCount(6);
        map.Nodes.Count(node => node.IsDeleted).Should().Be(5);
        GenerationChatBranchNodeViewModel tail = map.Nodes.Single(node => node.Id == current.Id);
        tail.IsCurrent.Should().BeTrue();
        tail.ParentId.Should().Be(SecondId);

        for (int index = 4; index >= 0; index--)
        {
            tail = map.Nodes.Single(node => node.Id == tail.ParentId);
            tail.Id.Should().Be(ancestorsIds[index]);
            tail.PreviewCard?.Prompt.Should().Be($"Historical request {index}");
            tail.PreviewCard?.ChatResult?.Text.Should().Be($"Historical reply {index}");
            tail.PreviewCard?.ChatResult?.IsTextReply.Should().Be(index == 2);
        }

        tail.ParentId.Should().BeNull();
        GenerationChatBranchNodeViewModel[] nodes = map.Nodes.ToArray();
        map.Refresh(new GenerationItemViewModel[] { current });
        await map.RestoreDeletedPreviewsAsync(chats.Object, references.Object, CancellationToken.None);
        map.Nodes.Should().Equal(nodes);
        map.SelectedNode?.Card.Should().BeSameAs(current);
        map.OpenNodeCommand.CanExecute(map.Nodes.Single(node => node.Id == SecondId)).Should().BeFalse();
        Guid[] restoredIds = map.Nodes.Select(node => node.Id).ToArray();
        map.Close();
        map.Open(new GenerationItemViewModel[] { current }, current.Id, current.Id);
        await map.RestoreDeletedPreviewsAsync(chats.Object, references.Object, CancellationToken.None);
        map.Nodes.Select(node => node.Id).Should().Equal(restoredIds);
    }

    [Fact]
    public async Task RestoreDeletedPreviewsAsync_WithSharedHistory_ReusesLiveAncestorsAndKeepsOneDeletedChain()
    {
        DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        GenerationChatMessage[] ancestors = Enumerable.Range(0, 3).SelectMany(index => new GenerationChatMessage[]
        {
            new(false, $"Request {index}", timestamp.AddMinutes(index), Array.Empty<GenerationReference>()),
            new(true, $"Reply {index}", timestamp, Array.Empty<GenerationReference>())
            { SourceCardId = index == 0 ? RootId : index == 1 ? FirstId : SecondId }
        }).ToArray();
        GenerationItemViewModel root = CreateCard(RootId);
        root.CreatedAtUtc = timestamp;
        root.CompletedAtUtc = timestamp.AddSeconds(20);
        GenerationItemViewModel first = CreateCard(FirstId, RootId);
        first.CreatedAtUtc = timestamp.AddMinutes(1);
        first.CompletedAtUtc = timestamp.AddMinutes(1).AddSeconds(20);
        GenerationItemViewModel current = CreateCard(LeafId, SecondId);
        current.ChatHistory = new GenerationChatHistory(ancestors);
        GenerationItemViewModel sibling = CreateCard(UnrelatedId, SecondId);
        sibling.ChatHistory = new GenerationChatHistory(ancestors.Select(message => message with { Text = "Edited sibling" }).ToArray());
        GenerationItemViewModel[] cards = [root, first, current, sibling];
        using GenerationChatBranchMapViewModel map = new(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(cards, current.Id, current.Id);

        await map.RestoreDeletedPreviewsAsync(Mock.Of<IGenerationChatStore>(), Mock.Of<IGenerationReferenceStore>(), CancellationToken.None);

        map.Nodes.Should().HaveCount(5);
        map.Nodes.Single(node => node.Id == RootId).Card.Should().BeSameAs(root);
        map.Nodes.Single(node => node.Id == FirstId).Card.Should().BeSameAs(first);
        map.Nodes.Single(node => node.Id == FirstId).ParentId.Should().Be(RootId);
        GenerationChatBranchNodeViewModel deleted = map.Nodes.Single(node => node.IsDeleted);
        deleted.Id.Should().Be(SecondId);
        deleted.ParentId.Should().Be(FirstId);
        deleted.PreviewCard?.Prompt.Should().Be("Request 2");
        map.Nodes.Single(node => node.Id == LeafId).ParentId.Should().Be(SecondId);
        map.Nodes.Single(node => node.Id == UnrelatedId).ParentId.Should().Be(SecondId);
    }

    [Fact]
    public async Task RestoreDeletedPreviewsAsync_WithEditedSharedAncestors_KeepsOneStableHistoricalChain()
    {
        DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        GenerationChatMessage[] ancestors = Enumerable.Range(0, 2).SelectMany(index => new GenerationChatMessage[]
        {
            new(false, $"Request {index}", timestamp.AddMinutes(index), Array.Empty<GenerationReference>()),
            new(true, $"Reply {index}", timestamp, Array.Empty<GenerationReference>())
            { SourceCardId = index == 0 ? UnrelatedId : RootId }
        }).ToArray();
        GenerationItemViewModel current = CreateCard(FirstId, RootId);
        current.ChatHistory = new GenerationChatHistory(ancestors);
        GenerationItemViewModel sibling = CreateCard(SecondId, RootId);
        sibling.ChatHistory = new GenerationChatHistory(ancestors.Select(message => message with
        {
            Text = "Edited sibling text",
            Images = new GenerationReference[] { new("edited", "edited.png", GenerationImageContentTypes.Png) }
        }).ToArray());
        using GenerationChatBranchMapViewModel map = new(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { current, sibling }, current.Id, current.Id);

        await map.RestoreDeletedPreviewsAsync(Mock.Of<IGenerationChatStore>(), Mock.Of<IGenerationReferenceStore>(), CancellationToken.None);

        map.Nodes.Should().HaveCount(4);
        GenerationChatBranchNodeViewModel root = map.Nodes.Single(node => node.ParentId is null);
        root.Id.Should().Be(UnrelatedId);
        root.IsDeleted.Should().BeTrue();
        root.PreviewCard?.Prompt.Should().Be("Request 0");
        map.Nodes.Single(node => node.Id == RootId).ParentId.Should().Be(root.Id);
        map.Nodes.Single(node => node.Id == FirstId).ParentId.Should().Be(RootId);
        map.Nodes.Single(node => node.Id == SecondId).ParentId.Should().Be(RootId);
        GenerationChatBranchNodeViewModel[] nodes = map.Nodes.ToArray();
        map.Refresh(new GenerationItemViewModel[] { sibling, current });
        map.Nodes.Should().Equal(nodes);
    }

    [Fact]
    public async Task RestoreDeletedPreviewsAsync_WithoutSavedTurnIds_DoesNotInventAncestors()
    {
        DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        GenerationItemViewModel current = CreateCard(FirstId, RootId);
        current.ChatHistory = new GenerationChatHistory(Enumerable.Range(0, 3).SelectMany(index => new GenerationChatMessage[]
        {
            new(false, $"Request {index}", timestamp, Array.Empty<GenerationReference>()),
            new(true, $"Reply {index}", timestamp, Array.Empty<GenerationReference>())
        }).ToArray());
        using GenerationChatBranchMapViewModel map = new(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { current }, current.Id, current.Id);

        await map.RestoreDeletedPreviewsAsync(Mock.Of<IGenerationChatStore>(), Mock.Of<IGenerationReferenceStore>(), CancellationToken.None);

        map.Nodes.Select(node => node.Id).Should().Equal(RootId, FirstId);
        map.Nodes.Single(node => node.Id == RootId).PreviewCard?.Prompt.Should().Be("Request 2");
        current.ChatHistory.Messages.Should().OnlyContain(message => message.SourceCardId == null);
    }

    [Fact]
    public async Task RestoreDeletedPreviewsAsync_WithPartialCurrentHistory_KeepsItsPreviewWhileFillingEarlierTurns()
    {
        DateTime timestamp = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        GenerationChatGroup group = CreateGroup(FirstId) with { AncestorCardIds = new Guid[] { RootId, FirstId } };
        GenerationItemViewModel current = CreateCard(LeafId, group: group);
        current.ChatHistory = new GenerationChatHistory(new GenerationChatMessage[]
        {
            new(false, "Current branch edit", timestamp.AddMinutes(1), Array.Empty<GenerationReference>()),
            new(true, "Current reply", timestamp.AddMinutes(1).AddSeconds(20), Array.Empty<GenerationReference>())
        });
        GenerationItemViewModel sibling = CreateCard(SecondId, group: group);
        sibling.ChatHistory = new GenerationChatHistory(new GenerationChatMessage[]
        {
            new(false, "Root", timestamp, Array.Empty<GenerationReference>()),
            new(true, "Root reply", timestamp.AddSeconds(20), Array.Empty<GenerationReference>()),
            new(false, "Old prompt", timestamp.AddMinutes(1), Array.Empty<GenerationReference>()),
            new(true, "Old reply", timestamp.AddMinutes(1).AddSeconds(20), Array.Empty<GenerationReference>())
        });
        using GenerationChatBranchMapViewModel map = new(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { current, sibling }, current.Id, current.Id);

        await map.RestoreDeletedPreviewsAsync(Mock.Of<IGenerationChatStore>(), Mock.Of<IGenerationReferenceStore>(), CancellationToken.None);

        map.Nodes.Single(node => node.Id == FirstId).PreviewCard?.Prompt.Should().Be("Current branch edit");
        map.Nodes.Single(node => node.Id == FirstId).PreviewCard?.ChatResult?.Text.Should().Be("Current reply");
        map.Nodes.Single(node => node.Id == RootId).PreviewCard?.Prompt.Should().Be("Root");
    }

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
            new(false, "Earlier request", timestamp.AddMinutes(-1), Array.Empty<GenerationReference>()),
            new(true, "Earlier reply", timestamp.AddSeconds(-20), Array.Empty<GenerationReference>()),
            new(false, "Root request", timestamp, Array.Empty<GenerationReference>()),
            new(true, "Root reply", timestamp, new GenerationReference[]
            {
                new("root", "root.png", GenerationImageContentTypes.Png)
            })
        }));
        await restoring;

        map.IsOpen.Should().BeFalse();
        map.Nodes.Should().HaveCount(2);
        map.Nodes.Single(node => node.Id == RootId).HasPreview.Should().BeFalse();
        references.VerifyNoOtherCalls();
    }
}
