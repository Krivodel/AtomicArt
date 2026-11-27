using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatBranchMapViewModel
{
    private CancellationTokenSource? _historyLoad;

    internal async Task RestoreDeletedPreviewsAsync(
        IGenerationChatStore chats, IGenerationReferenceStore references, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(chats);
        ArgumentNullException.ThrowIfNull(references);
        _historyLoad?.Cancel();
        using CancellationTokenSource load = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _historyLoad = load;

        try
        {
            HashSet<Guid> restored = [];
            Dictionary<string, string?> paths = new(StringComparer.Ordinal);
            List<GenerationItemViewModel> cards = Nodes.Select(node => node.Card).OfType<GenerationItemViewModel>()
                .OrderByDescending(card => object.ReferenceEquals(card, _currentCard))
                .ThenByDescending(card => object.ReferenceEquals(card, _anchorCard)).ToList();
            HashSet<GenerationItemViewModel> queued = cards.ToHashSet();

            for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
            {
                load.Token.ThrowIfCancellationRequested();
                GenerationItemViewModel card = cards[cardIndex];
                GenerationChatHistory? history = card.ChatHistory;

                if (history is null && card.ChatId is Guid chatId)
                {
                    history = await ReadHistoryAsync(chatId, _histories, chats, load.Token);
                }

                if (history is null && card.ChatGroup is { } group)
                {
                    history = group.Context ?? await ReadHistoryAsync(group.ContextId, _histories, chats, load.Token);
                }

                if (history is null)
                {
                    continue;
                }

                load.Token.ThrowIfCancellationRequested();
                RebuildNodes();
                ObserveCards();
                Dictionary<Guid, GenerationChatBranchNodeViewModel> deleted = Nodes.Where(node => node.IsDeleted && !restored.Contains(node.Id))
                    .ToDictionary(node => node.Id);
                Guid[] route = GetRoute(card);
                int ancestors = route.Length - 1;
                int end = GenerationChatBranchRoutes.GetContext(card, _histories).End;
                Dictionary<Guid, int> indexes = GenerationChatBranchRoutes.GetMessageIndexes(card, history, end);

                for (int index = 0; index < ancestors; index++)
                {
                    if (!deleted.TryGetValue(route[index], out GenerationChatBranchNodeViewModel? node)
                        || !indexes.TryGetValue(route[index], out int messageIndex))
                    {
                        continue;
                    }

                    GenerationChatMessage prompt = history.Messages[messageIndex];
                    GenerationChatMessage reply = history.Messages[messageIndex + 1];
                    GenerationReference? image = reply.Images.FirstOrDefault();
                    string? path = null;

                    if (image is not null && !paths.TryGetValue(image.Hash, out path))
                    {
                        path = await references.GetExistingFilePathAsync(image, load.Token);
                        paths.Add(image.Hash, path);
                    }

                    load.Token.ThrowIfCancellationRequested();
                    GenerationItemViewModel? preview = node.PreviewCard;

                    if (preview is null || preview.Prompt != prompt.Text || preview.ImagePath != path
                        || preview.ChatResult?.Text != reply.Text || preview.ChatResult?.Image != image
                        || preview.IsFailed != reply.Failed)
                    {
                        node.RestorePreview(card.CreateHistoryPreview(node.Id, prompt, reply, path));
                    }

                    deleted.Remove(node.Id);
                    restored.Add(node.Id);
                }

                if (deleted.Count == 0)
                {
                    break;
                }

                foreach (GenerationItemViewModel related in Nodes.Select(node => node.Card).OfType<GenerationItemViewModel>())
                {
                    if (queued.Add(related))
                    {
                        cards.Add(related);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested)
        {
        }
        finally
        {
            if (object.ReferenceEquals(_historyLoad, load))
            {
                _historyLoad = null;
            }
        }
    }

    private static async Task<GenerationChatHistory> ReadHistoryAsync(
        Guid id, Dictionary<Guid, GenerationChatHistory> histories, IGenerationChatStore chats, CancellationToken ct)
    {
        if (!histories.TryGetValue(id, out GenerationChatHistory? history))
        {
            history = await chats.LoadAsync(id, ct);
            ct.ThrowIfCancellationRequested();
            histories.Add(id, history);
        }

        return history;
    }

    private Guid[] GetRoute(GenerationItemViewModel card)
    {
        return _routes.GetValueOrDefault(card.Id) ?? GenerationChatBranchRoutes.GetDeclaredRoute(card);
    }
}
