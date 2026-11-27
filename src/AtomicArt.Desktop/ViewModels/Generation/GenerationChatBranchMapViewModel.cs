using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Helpers;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatBranchMapViewModel : ObservableObject, IDisposable
{
    public GenerationChatBranchMapViewModel? Presentation => IsOpen ? this : null;
    public ReadOnlyObservableCollection<GenerationChatBranchNodeViewModel> Nodes { get; }
    public IAsyncRelayCommand<GenerationItemViewModel?> OpenImageCommand { get; }
    public bool IsGenerationPanelVisible => !IsOpen || IsComposerOpen;

    private readonly IAsyncRelayCommand<GenerationItemViewModel?> _openChat;
    private readonly IAsyncRelayCommand<GenerationItemViewModel?> _selectChat;
    private readonly HashSet<GenerationItemViewModel> _observedCards = [];
    private readonly ObservableCollection<GenerationChatBranchNodeViewModel> _nodes = [];
    private readonly Dictionary<Guid, GenerationChatHistory> _histories = [];
    private Dictionary<Guid, Guid[]> _routes = [];
    [ObservableProperty]
    private GenerationChatBranchNodeViewModel? _selectedNode;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Presentation))]
    [NotifyPropertyChangedFor(nameof(IsGenerationPanelVisible))]
    private bool _isOpen;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGenerationPanelVisible))]
    private bool _isComposerOpen;
    private long _selectionVersion;
    private Guid _anchorId;
    private Guid? _currentId;
    private IReadOnlyList<GenerationItemViewModel> _cards = Array.Empty<GenerationItemViewModel>();
    private GenerationItemViewModel? _anchorCard;
    private GenerationItemViewModel? _currentCard;
    private GenerationItemViewModel? _selectingCard;
    private GenerationItemViewModel? _requestedChat;

    internal GenerationChatBranchMapViewModel(
        IAsyncRelayCommand<GenerationItemViewModel?> openChat,
        IAsyncRelayCommand<GenerationItemViewModel?> selectChat,
        IAsyncRelayCommand<GenerationItemViewModel?> openImage)
    {
        _openChat = openChat ?? throw new ArgumentNullException(nameof(openChat));
        _selectChat = selectChat ?? throw new ArgumentNullException(nameof(selectChat));
        OpenImageCommand = openImage ?? throw new ArgumentNullException(nameof(openImage));
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
        _selectionVersion++;
        _requestedChat = null;
        _selectingCard = null;
        _anchorId = anchorId;
        _currentId = currentId;
        _anchorCard = cards.FirstOrDefault(card => card.Id == anchorId);
        _currentCard = cards.FirstOrDefault(card => card.Id == currentId);
        _histories.Clear();
        IsComposerOpen = false;
        SelectedNode = null;
        Refresh(cards);
        IsOpen = Nodes.Count > 0;
        ObserveCards();
    }

    internal void Refresh(IReadOnlyList<GenerationItemViewModel> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        _historyLoad?.Cancel();
        _cards = cards;
        _anchorId = _anchorCard?.Id ?? _anchorId;
        _currentId = _currentCard?.Id ?? _currentId;
        RebuildNodes();

        if (Nodes.All(node => node.IsDeleted))
        {
            Close();
        }

        ObserveCards();
    }

    internal void UpdateCurrentChat(GenerationItemViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!IsOpen || object.ReferenceEquals(_currentCard, card))
        {
            return;
        }

        _currentCard = card;
        _currentId = card.Id;
        Refresh(_cards);
    }

    internal void RefreshCommands()
    {
        OpenNodeCommand.NotifyCanExecuteChanged();
    }

    internal void ShowComposer(GenerationItemViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (IsOpen && Nodes.FirstOrDefault(node => object.ReferenceEquals(node.Card, card)) is { } node)
        {
            SelectedNode = node;
            IsComposerOpen = true;
        }
    }

    internal bool IsOpeningChat(GenerationItemViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return IsOpen && object.ReferenceEquals(_requestedChat, card);
    }

    [RelayCommand]
    internal void Close()
    {
        _selectionVersion++;
        _selectChat.Cancel();
        _requestedChat = null;
        _selectingCard = null;
        _historyLoad?.Cancel();
        IsOpen = false;
        IsComposerOpen = false;
        ObserveCards();
        _cards = Array.Empty<GenerationItemViewModel>();
        _histories.Clear();
        _routes.Clear();
    }

    [RelayCommand]
    private void HideComposer()
    {
        _selectionVersion++;
        _selectChat.Cancel();
        _requestedChat = null;
        IsComposerOpen = false;
        SelectedNode = null;
    }

    private IReadOnlyList<GenerationChatBranchNodeViewModel> BuildNodes(
        IReadOnlyList<GenerationItemViewModel> cards, Guid anchorId, Guid? currentId)
    {
        Dictionary<Guid, GenerationItemViewModel> byId = cards.Where(card => card.SupportsChat).ToDictionary(card => card.Id);
        _routes = GenerationChatBranchRoutes.Build(byId.Values.ToArray(), _histories);
        Dictionary<Guid, List<Guid>> adjacent = [];
        Dictionary<Guid, Guid> parents = [];

        foreach (GenerationItemViewModel card in byId.Values.OrderBy(card => card.CreatedAtUtc)
            .ThenBy(card => card.ChatGroup?.VariantOrdinal).ThenBy(card => card.Id))
        {
            adjacent.TryAdd(card.Id, new List<Guid>());

            Guid[] route = GetRoute(card);

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

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SelectAsync(GenerationChatBranchNodeViewModel? node)
    {
        long version = ++_selectionVersion;

        if (!object.ReferenceEquals(node?.Card, _requestedChat))
        {
            _requestedChat = null;
        }

        if (_selectChat.ExecutionTask is { IsCompleted: false } pending)
        {
            await pending;
        }

        if (!IsOpen || version != _selectionVersion || node is null)
        {
            return;
        }

        node = Nodes.FirstOrDefault(current => node.Card is { } card
            ? object.ReferenceEquals(current.Card, card) : current.Id == node.Id);

        if (node is null)
        {
            return;
        }

        if (node.IsDeleted)
        {
            SelectedNode = node;
            IsComposerOpen = false;
        }
        else if (_selectChat.CanExecute(node.Card))
        {
            _selectingCard = node.Card;

            try
            {
                await _selectChat.ExecuteAsync(node.Card);
            }
            finally
            {
                _selectingCard = null;

                if (object.ReferenceEquals(_requestedChat, node.Card))
                {
                    _requestedChat = null;
                }
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenNode))]
    private async Task OpenNodeAsync(GenerationChatBranchNodeViewModel? node)
    {
        if (node?.Card is not { } target)
        {
            return;
        }

        _requestedChat = target;

        if (object.ReferenceEquals(_selectingCard, target))
        {
            return;
        }

        if (SelectCommand.ExecutionTask is { IsCompleted: false } selecting)
        {
            await selecting;
        }

        if (IsOpen && node?.Card is { } card
            && object.ReferenceEquals(_requestedChat, card)
            && Nodes.Any(current => object.ReferenceEquals(current.Card, card)) && _openChat.CanExecute(card))
        {
            await _openChat.ExecuteAsync(card);
        }
    }

    private bool CanOpenNode(GenerationChatBranchNodeViewModel? node)
    {
        return node?.Card is { } card && Nodes.Contains(node)
            && (_openChat.CanExecute(card) || SelectCommand.IsRunning);
    }

    private void RebuildNodes()
    {
        Guid selectedId = (IsComposerOpen ? _currentCard?.Id : SelectedNode?.Card?.Id ?? SelectedNode?.Id) ?? _anchorId;
        UpdateNodes(BuildNodes(_cards, _anchorId, _currentId));
        SelectedNode = Nodes.FirstOrDefault(node => node.Id == selectedId)
            ?? Nodes.FirstOrDefault(node => node.Id == _anchorId)
            ?? Nodes.FirstOrDefault(node => !node.IsDeleted);
    }

    private void UpdateNodes(IReadOnlyList<GenerationChatBranchNodeViewModel> desired)
    {
        Dictionary<Guid, GenerationChatBranchNodeViewModel> existing = _nodes.ToDictionary(node => node.Id);
        Dictionary<GenerationItemViewModel, GenerationChatBranchNodeViewModel> byCard = new(ReferenceEqualityComparer.Instance);

        foreach (GenerationChatBranchNodeViewModel node in _nodes)
        {
            if (node.Card is { } card)
            {
                byCard.Add(card, node);
            }
        }

        GenerationChatBranchNodeViewModel[] nodes = desired.Select(node =>
        {
            GenerationChatBranchNodeViewModel? previous = node.Card is { } card
                ? byCard.GetValueOrDefault(card) : existing.GetValueOrDefault(node.Id);

            if (previous is null)
            {
                return node;
            }

            if (object.ReferenceEquals(previous.Card, node.Card))
            {
                previous.Update(node.Id, node.ParentId, node.IsCurrent);
                return previous;
            }

            if (previous.PreviewCard is { } preview)
            {
                node.RestorePreview(preview);
            }

            return node;
        }).ToArray();
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
