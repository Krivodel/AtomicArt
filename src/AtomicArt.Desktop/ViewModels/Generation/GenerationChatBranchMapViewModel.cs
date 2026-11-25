using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Helpers;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatBranchMapViewModel : ObservableObject, IDisposable
{
    public bool CanOpenSelected => CanOpenNode(SelectedNode);
    public GenerationChatBranchMapViewModel? Presentation => IsOpen ? this : null;
    public ReadOnlyObservableCollection<GenerationChatBranchNodeViewModel> Nodes { get; }

    private readonly IAsyncRelayCommand<GenerationItemViewModel?> _openChat;
    private readonly HashSet<GenerationItemViewModel> _observedCards = [];
    private readonly ObservableCollection<GenerationChatBranchNodeViewModel> _nodes = [];
    [ObservableProperty]
    private GenerationChatBranchNodeViewModel? _selectedNode;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Presentation))]
    private bool _isOpen;
    private Guid _anchorId;
    private Guid? _currentId;
    private IReadOnlyList<GenerationItemViewModel> _cards = Array.Empty<GenerationItemViewModel>();
    private GenerationItemViewModel? _anchorCard;
    private GenerationItemViewModel? _currentCard;

    internal GenerationChatBranchMapViewModel(IAsyncRelayCommand<GenerationItemViewModel?> openChat)
    {
        _openChat = openChat ?? throw new ArgumentNullException(nameof(openChat));
        Nodes = new ReadOnlyObservableCollection<GenerationChatBranchNodeViewModel>(_nodes);
        _openChat.CanExecuteChanged += OnOpenChatCanExecuteChanged;
    }

    public void Dispose()
    {
        _openChat.CanExecuteChanged -= OnOpenChatCanExecuteChanged;
        Close();
    }

    internal void Open(IReadOnlyList<GenerationItemViewModel> cards, Guid anchorId, Guid? currentId)
    {
        ArgumentNullException.ThrowIfNull(cards);
        _anchorId = anchorId;
        _currentId = currentId;
        _anchorCard = cards.FirstOrDefault(card => card.Id == anchorId);
        _currentCard = cards.FirstOrDefault(card => card.Id == currentId);
        SelectedNode = null;
        Refresh(cards);
        IsOpen = Nodes.Count > 0;
        ObserveCards();
    }

    internal void Refresh(IReadOnlyList<GenerationItemViewModel> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        _cards = cards;
        _anchorId = _anchorCard?.Id ?? _anchorId;
        _currentId = _currentCard?.Id ?? _currentId;
        Guid selectedId = SelectedNode?.Card?.Id ?? SelectedNode?.Id ?? _anchorId;
        UpdateNodes(BuildNodes(cards, _anchorId, _currentId));
        SelectedNode = Nodes.FirstOrDefault(node => node.Id == selectedId)
            ?? Nodes.FirstOrDefault(node => node.Id == _anchorId)
            ?? Nodes.FirstOrDefault(node => !node.IsDeleted);

        if (Nodes.All(node => node.IsDeleted))
        {
            Close();
        }

        ObserveCards();
    }

    internal void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanOpenSelected));
        OpenSelectedCommand.NotifyCanExecuteChanged();
        OpenNodeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    internal void Close()
    {
        IsOpen = false;
        ObserveCards();
        _cards = Array.Empty<GenerationItemViewModel>();
    }

    private static IReadOnlyList<GenerationChatBranchNodeViewModel> BuildNodes(
        IReadOnlyList<GenerationItemViewModel> cards, Guid anchorId, Guid? currentId)
    {
        Dictionary<Guid, GenerationItemViewModel> byId = cards.Where(card => card.SupportsChat).ToDictionary(card => card.Id);
        Dictionary<Guid, List<Guid>> adjacent = [];
        Dictionary<Guid, Guid> parents = [];

        foreach (GenerationItemViewModel card in byId.Values.OrderBy(card => card.CreatedAtUtc)
            .ThenBy(card => card.ChatGroup?.VariantOrdinal).ThenBy(card => card.Id))
        {
            adjacent.TryAdd(card.Id, new List<Guid>());

            if (card.ChatGroup is not { } group)
            {
                continue;
            }

            Guid[] route = group.AncestorCardIds.Append(group.SourceCardId).Where(id => id != card.Id && id != Guid.Empty)
                .Distinct().Append(card.Id).ToArray();

            for (int index = 1; index < route.Length; index++)
            {
                Guid parent = route[index - 1];
                Guid child = route[index];
                parents.TryAdd(child, parent);
            }
        }

        foreach (KeyValuePair<Guid, Guid> edge in parents)
        {
            adjacent.TryAdd(edge.Key, new List<Guid>());
            adjacent.TryAdd(edge.Value, new List<Guid>());
            adjacent[edge.Value].Add(edge.Key);
            adjacent[edge.Key].Add(edge.Value);
        }

        if (!adjacent.ContainsKey(anchorId))
        {
            return Array.Empty<GenerationChatBranchNodeViewModel>();
        }

        HashSet<Guid> component = [];
        Queue<Guid> pending = [];
        pending.Enqueue(anchorId);

        while (pending.TryDequeue(out Guid id))
        {
            if (component.Add(id))
            {
                foreach (Guid neighbor in adjacent[id])
                {
                    pending.Enqueue(neighbor);
                }
            }
        }

        List<GenerationChatBranchNodeViewModel> result = [];
        HashSet<Guid> visited = [];
        Dictionary<Guid, Guid[]> descendants = parents.GroupBy(pair => pair.Value)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key)
                .OrderBy(id => byId.GetValueOrDefault(id)?.CreatedAtUtc)
                .ThenBy(id => byId.GetValueOrDefault(id)?.ChatGroup?.VariantOrdinal).ThenBy(id => id).ToArray());

        foreach (Guid root in component.Where(id => !parents.ContainsKey(id)).OrderBy(id => id).Concat(component.OrderBy(id => id)))
        {
            if (visited.Contains(root))
            {
                continue;
            }

            Queue<(Guid Id, Guid? Parent)> branch = [];
            branch.Enqueue((root, null));

            while (branch.TryDequeue(out (Guid Id, Guid? Parent) entry))
            {
                if (!visited.Add(entry.Id))
                {
                    continue;
                }

                GenerationItemViewModel? card = byId.GetValueOrDefault(entry.Id);
                bool isCurrent = entry.Id == currentId;
                result.Add(new GenerationChatBranchNodeViewModel(entry.Id, entry.Parent, card, isCurrent));

                foreach (Guid child in descendants.GetValueOrDefault(entry.Id) ?? Array.Empty<Guid>())
                {
                    branch.Enqueue((child, entry.Id));
                }
            }
        }

        return result.AsReadOnly();
    }

    [RelayCommand]
    private void Select(GenerationChatBranchNodeViewModel? node)
    {
        if (node is not null && Nodes.Contains(node))
        {
            SelectedNode = node;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    private Task OpenSelectedAsync()
    {
        return OpenNodeAsync(SelectedNode);
    }

    [RelayCommand(CanExecute = nameof(CanOpenNode))]
    private Task OpenNodeAsync(GenerationChatBranchNodeViewModel? node)
    {
        return _openChat.ExecuteAsync(node?.Card);
    }

    private bool CanOpenNode(GenerationChatBranchNodeViewModel? node)
    {
        return node?.Card is { } card && Nodes.Contains(node) && _openChat.CanExecute(card);
    }

    private void UpdateNodes(IReadOnlyList<GenerationChatBranchNodeViewModel> desired)
    {
        Dictionary<Guid, GenerationChatBranchNodeViewModel> existing = _nodes.ToDictionary(node => node.Id);
        GenerationChatBranchNodeViewModel[] nodes = desired.Select(node =>
            existing.TryGetValue(node.Id, out GenerationChatBranchNodeViewModel? previous)
                && previous.ParentId == node.ParentId
                && previous.IsCurrent == node.IsCurrent
                && object.ReferenceEquals(previous.Card, node.Card)
                    ? previous
                    : node).ToArray();
        ObservableCollectionSynchronizer.Synchronize(_nodes, nodes);
    }

    private void ObserveCards()
    {
        foreach (GenerationItemViewModel card in _observedCards)
        {
            card.PropertyChanged -= OnCardChanged;
        }

        _observedCards.Clear();

        if (IsOpen)
        {
            foreach (GenerationItemViewModel card in Nodes.Select(node => node.Card).OfType<GenerationItemViewModel>())
            {
                _observedCards.Add(card);
                card.PropertyChanged += OnCardChanged;
            }
        }
    }

    partial void OnSelectedNodeChanged(GenerationChatBranchNodeViewModel? oldValue, GenerationChatBranchNodeViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }

        RefreshCommands();
    }

    private void OnOpenChatCanExecuteChanged(object? sender, EventArgs e)
    {
        RefreshCommands();
    }

    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationItemViewModel.Id))
        {
            Refresh(_cards);
        }
    }
}
