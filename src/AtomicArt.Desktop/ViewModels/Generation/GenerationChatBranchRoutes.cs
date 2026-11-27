using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

internal static class GenerationChatBranchRoutes
{
    internal const int MessagesPerTurn = 2;

    internal static Dictionary<Guid, Guid[]> Build(
        IReadOnlyList<GenerationItemViewModel> cards, IReadOnlyDictionary<Guid, GenerationChatHistory> histories)
    {
        return cards.ToDictionary(card => card.Id, card =>
        {
            (GenerationChatHistory? history, int end) = GetContext(card, histories);

            return GetRoute(card, history, end);
        });
    }

    internal static Guid[] GetRoute(GenerationItemViewModel card, GenerationChatHistory? history, int end)
    {
        Guid[] declared = GetDeclaredRoute(card);
        int missing = Math.Max(0, (end / MessagesPerTurn) - (declared.Length - 1));
        List<Guid> ancestors = [];

        for (int turn = 0; history is not null && turn < missing; turn++)
        {
            int index = turn * MessagesPerTurn;

            if (IsTurn(history, index) && history.Messages[index + 1].SourceCardId is Guid id
                && id != Guid.Empty && !declared.Contains(id))
            {
                ancestors.Add(id);
            }
        }

        return ancestors.Concat(declared).Distinct().ToArray();
    }

    internal static Guid[] GetDeclaredRoute(GenerationItemViewModel card)
    {
        return (card.ChatGroup is { } group
            ? group.AncestorCardIds.Append(group.SourceCardId)
            : Enumerable.Empty<Guid>()).Where(id => id != card.Id && id != Guid.Empty)
            .Distinct().Append(card.Id).ToArray();
    }

    internal static (GenerationChatHistory? History, int End) GetContext(
        GenerationItemViewModel card, IReadOnlyDictionary<Guid, GenerationChatHistory> histories)
    {
        GenerationChatHistory? history = card.ChatHistory
            ?? (card.ChatId is Guid chatId ? histories.GetValueOrDefault(chatId) : null);

        if (history is not null)
        {
            int end = history.Messages.Count - (history.IncludesCardTurn ? MessagesPerTurn : 0);

            return (history, Math.Max(0, end));
        }

        history = card.ChatGroup is { } group ? group.Context ?? histories.GetValueOrDefault(group.ContextId) : null;

        return (history, Math.Max(0, (history?.Messages.Count ?? 1) - 1));
    }

    internal static Dictionary<Guid, int> GetMessageIndexes(GenerationItemViewModel card, GenerationChatHistory history, int end)
    {
        Dictionary<Guid, int> indexes = [];

        for (int index = 0; index + 1 < end; index += MessagesPerTurn)
        {
            if (IsTurn(history, index) && history.Messages[index + 1].SourceCardId is Guid id && id != Guid.Empty)
            {
                indexes.TryAdd(id, index);
            }
        }

        Guid[] declared = GetDeclaredRoute(card);

        for (int turn = 0; turn < declared.Length - 1; turn++)
        {
            int index = end - ((declared.Length - 1 - turn) * MessagesPerTurn);

            if (IsTurn(history, index) && (history.Messages[index + 1].SourceCardId is null
                || history.Messages[index + 1].SourceCardId == declared[turn]))
            {
                indexes.TryAdd(declared[turn], index);
            }
        }

        return indexes;
    }

    private static bool IsTurn(GenerationChatHistory history, int index)
    {
        return index >= 0 && index + 1 < history.Messages.Count
            && !history.Messages[index].IsAssistant && history.Messages[index + 1].IsAssistant;
    }
}
