using System.Text.Json;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

public sealed partial class GenerationChatBranchMapViewModelTests
{
    private static readonly Guid RootId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FirstId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SecondId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LeafId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid UnrelatedId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void UpdateCurrentChat_WithAnotherCard_PreservesAllNodeInstances()
    {
        GenerationItemViewModel root = CreateCard(RootId);
        GenerationItemViewModel target = CreateCard(FirstId, RootId);
        using GenerationChatBranchMapViewModel map = CreateMap();
        map.Open(new GenerationItemViewModel[] { root, target }, RootId, RootId);
        GenerationChatBranchNodeViewModel original = map.Nodes.Single(node => node.Card == root);
        GenerationChatBranchNodeViewModel selected = map.Nodes.Single(node => node.Card == target);

        map.UpdateCurrentChat(target);

        map.Nodes.Single(node => node.Card == root).Should().BeSameAs(original);
        map.Nodes.Single(node => node.Card == target).Should().BeSameAs(selected);
        original.IsCurrent.Should().BeFalse();
        selected.IsCurrent.Should().BeTrue();
        selected.HasStatus.Should().BeTrue();
    }

    [Fact]
    public void Refresh_WhenAddingABranch_PreservesExistingNodes()
    {
        GenerationItemViewModel root = CreateCard(RootId);
        GenerationItemViewModel first = CreateCard(FirstId, RootId);
        using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { root, first }, FirstId, FirstId);
        GenerationChatBranchNodeViewModel[] existing = map.Nodes.ToArray();

        map.Refresh(new GenerationItemViewModel[] { root, first, CreateCard(SecondId, RootId) });

        foreach (GenerationChatBranchNodeViewModel node in existing)
        {
            map.Nodes.Single(current => current.Id == node.Id).Should().BeSameAs(node);
        }

        map.SelectedNode.Should().BeSameAs(existing.Single(node => node.Id == FirstId));
        map.Nodes.Should().HaveCount(3);
    }

    [Fact]
    public void Refresh_WhenPendingCardReceivesItsResultId_PreservesAllNodes()
    {
        GenerationItemViewModel pending = CreateCard(FirstId, RootId);
        using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { CreateCard(RootId), pending }, FirstId, FirstId);
        GenerationChatBranchNodeViewModel root = map.Nodes.Single(node => node.Id == RootId);
        GenerationChatBranchNodeViewModel original = map.Nodes.Single(node => node.Id == FirstId);

        pending.Id = LeafId;

        map.Nodes.Single(node => node.Id == RootId).Should().BeSameAs(root);
        map.Nodes.Single(node => node.Id == LeafId).Should().BeSameAs(original);
        map.SelectedNode?.Id.Should().Be(LeafId);
        map.Nodes.Single(node => node.IsCurrent).Id.Should().Be(LeafId);
    }

    [Fact]
    public void Open_WithParallelVariants_MarksOnlyTheOpenChat()
    {
        GenerationChatGroup group = CreateGroup(RootId);
        GenerationItemViewModel root = CreateCard(RootId);
        GenerationItemViewModel first = CreateCard(FirstId, group: group);
        GenerationItemViewModel second = CreateCard(SecondId, group: group);
        first.StatusKind = GenerationItemStatus.Generating;
        second.StatusKind = GenerationItemStatus.Generating;
        using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));

        map.Open(new GenerationItemViewModel[] { root, first, second }, first.Id, first.Id);

        map.Nodes.Single(node => node.IsCurrent).Id.Should().Be(FirstId);
    }

    [Fact]
    public void Refresh_WhenPendingCardReceivesItsResultId_KeepsItsSelectionAndFamily()
    {
        GenerationItemViewModel card = CreateCard(FirstId, RootId);
        using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { CreateCard(RootId), card }, FirstId, FirstId);

        card.Id = LeafId;

        map.IsOpen.Should().BeTrue();
        map.Nodes.Single(node => node.IsCurrent).Id.Should().Be(LeafId);
        map.SelectedNode?.Card.Should().BeSameAs(card);
        map.SelectedNode?.Id.Should().Be(LeafId);
    }

    [Fact]
    public void Open_FromDescendant_ShowsOnlyItsFamilyWithParentsAndSiblings()
    {
        GenerationItemViewModel[] cards =
        [
            CreateCard(RootId), CreateCard(FirstId, RootId), CreateCard(SecondId, RootId),
            CreateCard(LeafId, FirstId), CreateCard(UnrelatedId)
        ];
        using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));

        map.Open(cards, LeafId, LeafId);

        map.Nodes.Select(node => node.Id).Should().BeEquivalentTo(new Guid[] { RootId, FirstId, SecondId, LeafId });
        map.Nodes.Single(node => node.Id == LeafId).ParentId.Should().Be(FirstId);
        map.Nodes.Single(node => node.IsCurrent).Id.Should().Be(LeafId);
        map.SelectedNode?.Id.Should().Be(LeafId);
    }

    [Fact]
    public void Open_WithDeletedAncestor_KeepsTheSurvivingFamilyConnected()
    {
        GenerationChatGroup group = CreateGroup(FirstId) with { AncestorCardIds = new Guid[] { RootId, FirstId } };
        GenerationItemViewModel[] cards = [CreateCard(RootId), CreateCard(SecondId, RootId), CreateCard(LeafId, group: group)];
        using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));

        map.Open(cards, LeafId, LeafId);

        map.Nodes.Should().HaveCount(4);
        map.Nodes.Single(node => node.Id == FirstId).IsDeleted.Should().BeTrue();
        map.Nodes.Single(node => node.Id == FirstId).ParentId.Should().Be(RootId);
        map.Nodes.Single(node => node.Id == LeafId).ParentId.Should().Be(FirstId);
        map.Nodes.Single(node => node.Id == SecondId).ParentId.Should().Be(RootId);
    }

    [Fact]
    public async Task OpenNodeCommand_WithPendingCard_UsesTheNormalChatCommand()
    {
        GenerationItemViewModel pending = CreateCard(FirstId, RootId);
        pending.StatusKind = GenerationItemStatus.Generating;
        GenerationItemViewModel? opened = null;
        using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap(new AsyncRelayCommand<GenerationItemViewModel?>(card =>
        {
            opened = card;
            return Task.CompletedTask;
        }));
        map.Open(new GenerationItemViewModel[] { CreateCard(RootId), pending }, RootId, RootId);
        map.SelectCommand.Execute(map.Nodes.Single(node => node.Id == FirstId));

        await map.OpenNodeCommand.ExecuteAsync(map.SelectedNode);

        map.OpenNodeCommand.CanExecute(map.SelectedNode).Should().BeTrue();
        opened.Should().BeSameAs(pending);
    }

    [Fact]
    public void Refresh_AfterDeletingSelectedCard_SelectsAnExistingCard()
    {
        GenerationItemViewModel root = CreateCard(RootId);
        using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(new GenerationItemViewModel[] { root, CreateCard(FirstId, RootId) }, RootId, RootId);
        map.SelectCommand.Execute(map.Nodes.Single(node => node.Id == FirstId));

        map.Refresh(new GenerationItemViewModel[] { root });

        map.SelectedNode?.Card.Should().BeSameAs(root);
        map.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Open_WithCycle_ProducesAnAcyclicTree()
    {
        using GenerationChatBranchMapViewModel map = GenerationChatBranchMapViewModelTests.CreateMap(new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        GenerationItemViewModel[] cards = [CreateCard(RootId, FirstId), CreateCard(FirstId, RootId)];

        map.Open(cards, RootId, RootId);

        map.Nodes.Should().HaveCount(2);
        map.Nodes.Where(node => node.ParentId is null).Should().ContainSingle();
        map.Nodes.Where(node => node.ParentId is not null).Should().ContainSingle();
    }

    [Fact]
    public void Serialize_WithAncestors_PreservesTheFamilyAfterRestart()
    {
        GenerationChatGroup group = CreateGroup(FirstId) with { AncestorCardIds = new Guid[] { RootId, FirstId } };

        GenerationChatGroup? restored = JsonSerializer.Deserialize<GenerationChatGroup>(JsonSerializer.Serialize(group));

        restored?.AncestorCardIds.Should().Equal(RootId, FirstId);
    }

    [Fact]
    public async Task SelectCommand_WithRapidClicks_UsesOnlyTheLastQueuedBranch()
    {
        GenerationItemViewModel[] cards = [CreateCard(RootId), CreateCard(FirstId, RootId), CreateCard(SecondId, RootId)];
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<GenerationItemViewModel?> selected = [];
        using GenerationChatBranchMapViewModel map = new(
            new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask),
            new AsyncRelayCommand<GenerationItemViewModel?>(async card =>
            {
                selected.Add(card);
                await ready.Task;
            }), new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));
        map.Open(cards, RootId, RootId);

        Task first = map.SelectCommand.ExecuteAsync(map.Nodes.Single(node => node.Id == RootId));
        Task second = map.SelectCommand.ExecuteAsync(map.Nodes.Single(node => node.Id == FirstId));
        Task last = map.SelectCommand.ExecuteAsync(map.Nodes.Single(node => node.Id == SecondId));
        map.Refresh(cards);
        ready.SetResult();
        await Task.WhenAll(first, second, last);

        selected.Should().Equal(cards[0], cards[2]);
        map.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task SelectCommand_WithDeletedNode_HidesTheComposerWithoutNavigating()
    {
        GenerationItemViewModel card = CreateCard(FirstId, RootId);
        using GenerationChatBranchMapViewModel map = CreateMap();
        map.Open(new GenerationItemViewModel[] { card }, FirstId, FirstId);
        await map.SelectCommand.ExecuteAsync(map.Nodes.Single(node => node.Id == FirstId));
        map.IsComposerOpen.Should().BeTrue();

        await map.SelectCommand.ExecuteAsync(map.Nodes.Single(node => node.IsDeleted));

        map.IsOpen.Should().BeTrue();
        map.IsComposerOpen.Should().BeFalse();
        map.IsGenerationPanelVisible.Should().BeFalse();
        map.Nodes.Single(node => node.IsCurrent).Card.Should().BeSameAs(card);
    }

    internal static GenerationChatBranchMapViewModel CreateMap(
        IAsyncRelayCommand<GenerationItemViewModel?>? openChat = null,
        IAsyncRelayCommand<GenerationItemViewModel?>? openImage = null)
    {
        GenerationChatBranchMapViewModel? map = null;
        IAsyncRelayCommand<GenerationItemViewModel?> selectChat = new AsyncRelayCommand<GenerationItemViewModel?>(card =>
        {
            if (card is not null && map is not null)
            {
                map.UpdateCurrentChat(card);
                map.ShowComposer(card);
            }

            return Task.CompletedTask;
        });
        map = new GenerationChatBranchMapViewModel(
            openChat ?? new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask), selectChat,
            openImage ?? new AsyncRelayCommand<GenerationItemViewModel?>(_ => Task.CompletedTask));

        return map;
    }

    internal static GenerationItemViewModel CreateCard(
        Guid id, Guid? parent = null, GenerationChatGroup? group = null, Guid? chatId = null)
    {
        GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated(id: id, imagePath: null,
            prompt: $"Prompt {id}", modelId: LocalCodexImageModel.Id,
            chatGroup: group ?? (parent is Guid sourceId ? CreateGroup(sourceId) : null), chatId: chatId);

        return GenerationItemViewModel.Restore(state, null, null,
            GenerationItemStatusDescriptorRegistryTestFactory.Create(), TestLocalizationTextProvider.Default);
    }

    private static GenerationChatGroup CreateGroup(Guid sourceId)
    {
        return new GenerationChatGroup(LeafId, sourceId, UnrelatedId, Array.Empty<string>());
    }
}
