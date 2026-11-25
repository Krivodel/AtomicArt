using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatBranchMapViewModel
{
    private const int MessagesPerTurn = 2;

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
            Dictionary<Guid, GenerationChatBranchNodeViewModel> deleted = Nodes.Where(node => node.IsDeleted)
                .ToDictionary(node => node.Id);
            Dictionary<Guid, GenerationChatHistory> histories = [];
            Dictionary<string, string?> paths = new(StringComparer.Ordinal);
            GenerationItemViewModel[] cards = Nodes.Select(node => node.Card).OfType<GenerationItemViewModel>()
                .OrderByDescending(card => object.ReferenceEquals(card, _currentCard))
                .ThenByDescending(card => object.ReferenceEquals(card, _anchorCard)).ToArray();

            foreach (GenerationItemViewModel card in cards)
            {
                load.Token.ThrowIfCancellationRequested();

                if (deleted.Count == 0)
                {
                    break;
                }

                Guid[] route = GetRoute(card);
                int ancestors = route.Length - 1;

                if (!route.Take(ancestors).Any(deleted.ContainsKey))
                {
                    continue;
                }

                GenerationChatHistory? history = card.ChatHistory;

                if (history is null && card.ChatId is Guid chatId)
                {
                    history = await ReadHistoryAsync(chatId, histories, chats, load.Token);
                }

                int end = history is { IncludesCardTurn: true }
                    ? history.Messages.Count - MessagesPerTurn : history?.Messages.Count ?? 0;

                if (history is null && card.ChatGroup is { } group)
                {
                    history = group.Context ?? await ReadHistoryAsync(group.ContextId, histories, chats, load.Token);
                    end = history.Messages.Count - 1;
                }

                if (history is null)
                {
                    continue;
                }

                load.Token.ThrowIfCancellationRequested();

                for (int index = 0; index < ancestors; index++)
                {
                    int messageIndex = end - ((ancestors - index) * MessagesPerTurn);

                    if (!deleted.TryGetValue(route[index], out GenerationChatBranchNodeViewModel? node)
                        || messageIndex < 0 || messageIndex + 1 >= history.Messages.Count
                        || history.Messages[messageIndex].IsAssistant || !history.Messages[messageIndex + 1].IsAssistant)
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

    private static Guid[] GetRoute(GenerationItemViewModel card)
    {
        return (card.ChatGroup is { } group
            ? group.AncestorCardIds.Append(group.SourceCardId)
            : Enumerable.Empty<Guid>()).Where(id => id != card.Id && id != Guid.Empty)
            .Distinct().Append(card.Id).ToArray();
    }

    private static async Task<GenerationChatHistory> ReadHistoryAsync(
        Guid id, Dictionary<Guid, GenerationChatHistory> histories, IGenerationChatStore chats, CancellationToken ct)
    {
        if (!histories.TryGetValue(id, out GenerationChatHistory? history))
        {
            history = await chats.LoadAsync(id, ct);
            histories.Add(id, history);
        }

        return history;
    }
}
