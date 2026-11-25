using System.ComponentModel;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

internal sealed partial class GenerationChatSessionViewModel
{
    internal Guid? GroupId { get; private set; }
    internal GenerationChatGroup? Group => _group;
    internal bool IsGroup => GroupId is not null;
    internal GenerationChatImageViewModel? AutoContinueImage => IsGroup && _autoContinueSingle && !_isSubmitting
        && _variantReply?.Images.Count == 1 && _variantReply.Images[0].CanChoose ? _variantReply.Images[0] : null;
    internal bool HasGroupVariants => _variantReply?.Images.Count > 0;

    private readonly Dictionary<Guid, GenerationItemViewModel> _groupCards = [];
    private GenerationChatGroup? _group;
    private bool _autoContinueSingle;
    private bool _restoredGroup;
    private int _nextVariantOrdinal;

    internal void BeginGroup()
    {
        GroupId = Guid.NewGuid();
        _autoContinueSingle = true;
    }

    internal async Task OpenGroupAsync(GenerationChatGroup group, GenerationItemViewModel representative, CancellationToken ct)
    {
        GenerationChatHistory context = group.Context ?? await _chats.LoadAsync(group.ContextId, ct);

        if (context.Messages.Count == 0 || _gallery is null)
        {
            throw new InvalidDataException("The chat variant group has no saved request.");
        }

        ResetConversation();
        GroupId = group.Id;
        _group = group with { Context = context };
        _restoredGroup = true;
        _source = _gallery.Items.FirstOrDefault(card => card.Id == group.SourceCardId) ?? representative;
        GenerationChatHistory history = new(context.Messages.SkipLast(1).ToArray());

        foreach (GenerationChatMessage message in history.Messages)
        {
            _messages.Add(CreateMessage(message));
        }

        _sourceMessageCount = _messages.Count;
        GenerationChatRun[] runs = _gallery.Items.Where(card => card.ChatGroup?.Id == group.Id)
            .OrderBy(card => card.ChatGroup?.VariantOrdinal)
            .Select(card => new GenerationChatRun(card.CorrelationId ?? card.Id)
            {
                Ordinal = card.ChatGroup?.VariantOrdinal ?? 0
            }).ToArray();
        _nextVariantOrdinal = runs.Length == 0 ? 0 : runs.Max(run => run.Ordinal) + 1;
        AppendTurn(new GenerationChatBatch(history, context.Messages[^1], runs, _group), _sessionVersion);
        OnPropertyChanged(nameof(HasChat));
        RefreshVariantChoices();
    }

    private GenerationChatGroup? CreateGroup(GenerationChatHistory history, GenerationChatMessage user)
    {
        if (_group is null && GroupId is Guid id && _source is { } source)
        {
            GenerationChatHistory context = new(history.Messages.Append(user).ToArray());
            _group = new GenerationChatGroup(id, source.Id, context.Id, context.ReferenceHashes)
            {
                Context = context,
                CreatedAtUtc = user.CreatedAtUtc,
                AncestorCardIds = (source.ChatGroup is { } sourceGroup
                    ? sourceGroup.AncestorCardIds.Append(sourceGroup.SourceCardId)
                    : Enumerable.Empty<Guid>()).Append(source.Id).Distinct().ToArray()
            };
        }

        return _group;
    }

    private void DetachGroupCards()
    {
        foreach (GenerationItemViewModel card in _groupCards.Values)
        {
            card.PropertyChanged -= OnGroupCardChanged;
        }

        _groupCards.Clear();
    }

    private void LockGroupMessages()
    {
        if (IsGroup)
        {
            foreach (GenerationChatMessageViewModel message in _messages)
            {
                message.IsVariantChoice = true;
            }
        }
    }

    private void RefreshGroupCards()
    {
        if (_group is null || _gallery is null || _variantReply is null)
        {
            return;
        }

        GenerationChatRun[] runs = _conversationBatches.SelectMany(batch => batch.Generation.Runs).ToArray();

        foreach (GenerationChatRun run in runs)
        {
            GenerationItemViewModel? card = _gallery.Items.FirstOrDefault(item => item.Id == run.Id || item.CorrelationId == run.Id);

            if (card is not null)
            {
                if (_groupCards.TryAdd(run.Id, card))
                {
                    card.PropertyChanged += OnGroupCardChanged;
                }

                if (_restoredGroup && !card.IsGenerating)
                {
                    run.Complete(card.ChatResult);
                }
            }
            else if (_groupCards.Remove(run.Id, out GenerationItemViewModel? removed))
            {
                removed.PropertyChanged -= OnGroupCardChanged;
                _variantReply.RemoveVariant(run.Id);
                run.Complete(null);
            }
        }

        OnPropertyChanged(nameof(HasGroupVariants));
    }

    private void OnGroupCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationItemViewModel.IsGenerating))
        {
            RefreshGroupCards();
            RefreshVariantChoices();
        }
        else if (e.PropertyName == nameof(GenerationItemViewModel.TextReplyPreview)
            && sender is GenerationItemViewModel { TextReplyPreview: { } text } card
            && _variantReply?.Images.FirstOrDefault(image => image.Id == card.Id) is { TextReply: not null } variant)
        {
            variant.TextReply = text;
        }
    }
}
