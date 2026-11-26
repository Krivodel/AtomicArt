using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

internal sealed partial class GenerationChatSessionViewModel : AsyncOperationViewModel, IDisposable
{
    public ReadOnlyObservableCollection<GenerationChatMessageViewModel> Messages { get; }
    public bool HasChat => _source is not null;
    public bool IsBusy => _isSubmitting
        || _messages.Any(message => message.HasPendingResults)
        || _variantReply?.Images.Any(image => image.CanChoose) == true;

    internal GenerationItemViewModel? Source => _source;
    internal bool CanSubmit => !_isSubmitting && (_pendingSourceImage is null);
    internal bool CanAddVariants => IsGroup || RepeatBatch is not null;
    internal string? RepeatPrompt => RepeatBatch?.Generation.UserMessage.Text;

    private ConversationBatch? RepeatBatch => _repeatInFlight ?? (_conversationBatches.LastOrDefault() is { } batch
        && (IsGroup || object.ReferenceEquals(batch.Reply, _variantReply) || batch.Reply.HasPendingResults) ? batch : null);

    private readonly GenerationChatMediaService _media;
    private readonly IGenerationReferenceStore _references;
    private readonly NanoBanana2PanelTextFormatter _formatter;
    private readonly ITextClipboardService _clipboard;
    private readonly IGenerationChatStore _chats;
    private readonly GenerationChatDateFormatter _dates;
    private readonly ObservableCollection<GenerationChatMessageViewModel> _messages = [];
    private readonly List<ConversationBatch> _conversationBatches = [];
    private readonly SemaphoreSlim _submissionGate = new(1, 1);
    private ConversationBatch? _repeatInFlight;
    private GalleryViewModel? _gallery;
    private IGenerationPanelPresetTarget? _panel;
    private GenerationItemViewModel? _source;
    private GenerationChatMessageViewModel? _variantReply;
    private Guid? _pendingSourceImage;
    private int _sourceMessageCount;
    private bool _isSubmitting;
    private long _sessionVersion;
    private int _pendingSubmissions;
    private int _disposeRequested;
    private int _gateDisposed;

    public GenerationChatSessionViewModel(
        GenerationChatMediaService media,
        IGenerationReferenceStore references,
        NanoBanana2PanelTextFormatter formatter,
        ITextClipboardService clipboard,
        IViewModelErrorHandler errorHandler,
        IDialogService dialogs,
        IGenerationChatStore chats,
        GenerationChatDateFormatter dates)
        : base(errorHandler, dialogs)
    {
        _media = media ?? throw new ArgumentNullException(nameof(media));
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _chats = chats ?? throw new ArgumentNullException(nameof(chats));
        _dates = dates ?? throw new ArgumentNullException(nameof(dates));
        _dates.Changed += OnDatesChanged;
        Messages = new ReadOnlyObservableCollection<GenerationChatMessageViewModel>(_messages);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
        {
            return;
        }

        _dates.Changed -= OnDatesChanged;

        if (_gallery is not null)
        {
            ((INotifyCollectionChanged)_gallery.Items).CollectionChanged -= OnGalleryItemsChanged;
        }

        Detach();
        TryDisposeSubmissionGate();
    }

    internal void Connect(GalleryViewModel gallery, IGenerationPanelPresetTarget panel)
    {
        ArgumentNullException.ThrowIfNull(gallery);
        ArgumentNullException.ThrowIfNull(panel);

        if (_gallery is not null)
        {
            ((INotifyCollectionChanged)_gallery.Items).CollectionChanged -= OnGalleryItemsChanged;
        }

        _gallery = gallery;
        _panel = panel;
        ((INotifyCollectionChanged)gallery.Items).CollectionChanged += OnGalleryItemsChanged;
    }

    internal bool IsForModel(string? modelId)
    {
        return _source is not null && string.Equals(modelId, _source.ModelId, StringComparison.Ordinal);
    }

    internal void Detach()
    {
        ResetConversation();
        OnPropertyChanged(nameof(HasChat));
        OnPropertyChanged(nameof(IsBusy));
    }

    internal async Task SubmitAsync(
        NanoBanana2GenerationParameters parameters,
        Func<NanoBanana2GenerationParameters, CancellationToken, Task> submit,
        CancellationToken ct)
    {
        if (Volatile.Read(ref _disposeRequested) != 0 || _pendingSourceImage is not null)
        {
            return;
        }

        bool queued = Interlocked.Increment(ref _pendingSubmissions) > 1;
        bool entered = false;
        long version = _sessionVersion;

        try
        {
            if (Volatile.Read(ref _disposeRequested) != 0)
            {
                return;
            }

            await _submissionGate.WaitAsync(ct);
            entered = true;

            if (Volatile.Read(ref _disposeRequested) == 0 && version == _sessionVersion)
            {
                await SubmitCoreAsync(parameters, submit, queued, ct);
            }
        }
        finally
        {
            if (entered)
            {
                _submissionGate.Release();
            }

            Interlocked.Decrement(ref _pendingSubmissions);
            TryDisposeSubmissionGate();
        }
    }

    internal Task OpenSessionAsync(GenerationItemViewModel item, GenerationPanelPreset? preset, CancellationToken ct)
    {
        return OpenConversationAsync(item, preset, ct);
    }

    internal GenerationItemViewModel? GetVariantCard(GenerationChatImageViewModel image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return image.CanChoose && _variantReply?.Images.Contains(image) == true ? FindVariantCard(image) : null;
    }

    private async Task SubmitCoreAsync(NanoBanana2GenerationParameters parameters,
        Func<NanoBanana2GenerationParameters, CancellationToken, Task> submit, bool queued, CancellationToken ct)
    {
        ConversationBatch? previous = RepeatBatch ?? (queued ? _conversationBatches.LastOrDefault() : null);
        _repeatInFlight = previous;
        _isSubmitting = true;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanSubmit));
        Guid storageLease = Guid.NewGuid();
        long version = _sessionVersion;
        GenerationChatHistory history = previous?.Generation.History
            ?? new GenerationChatHistory(_messages.Select(message => message.Snapshot()).ToArray());

        try
        {
            await _references.RetainAsync(storageLease, history.Messages.SelectMany(message => message.Images), ct);
            GenerationChatMessage user;

            if (previous is not null)
            {
                user = previous.Generation.UserMessage;
                await _references.RetainAsync(storageLease, user.Images, ct);
                List<AttachedImageDto> images = [];

                foreach (GenerationReference reference in user.Images)
                {
                    AttachedImageDto image = await _references.LoadAsync(reference, ct)
                        ?? throw new InvalidDataException("The reference for the repeated chat request is unavailable.");
                    images.Add(image);
                }

                parameters = parameters with { Prompt = user.Text, AttachedImages = images };
            }
            else
            {
                IReadOnlyList<GenerationReference> references = await _references.SaveAsync(storageLease, parameters.AttachedImages, ct);
                user = new GenerationChatMessage(false, parameters.Prompt, DateTime.UtcNow, references);
            }

            GenerationChatRun[] runs = Enumerable.Range(0, parameters.GenerationCount)
                .Select(_ => new GenerationChatRun { Ordinal = _nextVariantOrdinal++ }).ToArray();
            GenerationChatBatch batch = new(history, user, runs, CreateGroup(history, user));

            if (version == _sessionVersion)
            {
                if (previous is null)
                {
                    AppendTurn(batch, version);
                }
                else
                {
                    AppendVariants(previous, batch, version);
                }
            }

            await submit(parameters with { ChatBatch = batch }, ct);
        }
        finally
        {
            _references.Release(storageLease);
            _repeatInFlight = null;
            _isSubmitting = false;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanSubmit));
            NotifyRepeatState();
        }
    }

    private void TryDisposeSubmissionGate()
    {
        if (Volatile.Read(ref _disposeRequested) != 0 && Volatile.Read(ref _pendingSubmissions) == 0
            && Interlocked.Exchange(ref _gateDisposed, 1) == 0)
        {
            _submissionGate.Dispose();
        }
    }

    private async Task OpenConversationAsync(
        GenerationItemViewModel item, GenerationPanelPreset? preset, CancellationToken ct)
    {
        long version = _sessionVersion;
        bool isPending = item.IsGenerating;
        GenerationChatHistory history = await ReadHistoryAsync(item, ct);
        GenerationChatMessageViewModel[] messages = history.Messages.Select(CreateMessage).ToArray();

        if (_gallery?.Items.Contains(item) != true || version != _sessionVersion
            || (preset is not null && (_panel is null || !await _panel.TryApplyPresetAsync(preset, ct))))
        {
            return;
        }

        ct.ThrowIfCancellationRequested();

        if (!_gallery.Items.Contains(item))
        {
            return;
        }

        ResetConversation();
        _source = item;
        _sourceMessageCount = messages.Length;

        if (isPending && messages.Length > 0)
        {
            _pendingSourceImage = item.CorrelationId ?? item.Id;
            messages[^1].AddPendingImage(_pendingSourceImage.GetValueOrDefault());

            foreach (GenerationChatMessageViewModel message in messages)
            {
                message.IsTurnPending = true;
            }

            item.PropertyChanged += OnSourceChanged;
        }

        foreach (GenerationChatMessageViewModel message in messages)
        {
            _messages.Add(message);
        }

        UpdateDateLabels();

        OnPropertyChanged(nameof(HasChat));
        OnPropertyChanged(nameof(IsBusy));
        CompleteSource(item);
    }

    private void ResetConversation()
    {
        _sessionVersion++;
        DetachGroupCards();

        if (_source is not null)
        {
            _source.PropertyChanged -= OnSourceChanged;
        }

        _pendingSourceImage = null;

        foreach (GenerationChatMessageViewModel message in _messages)
        {
            message.Dispose();
        }

        _source = null;
        _variantReply = null;
        _sourceMessageCount = 0;
        _messages.Clear();
        _conversationBatches.Clear();
    }

    private void AppendTurn(GenerationChatBatch batch, long version)
    {
        _autoContinueSingle &= batch.Runs.Count == 1;
        GenerationChatMessageViewModel user = CreateMessage(batch.UserMessage);
        user.IsTurnPending = true;
        user.AnimateInsertion = true;
        GenerationChatMessageViewModel reply = CreateMessage(
            new GenerationChatMessage(true, string.Empty, batch.UserMessage.CreatedAtUtc, Array.Empty<GenerationReference>()));
        reply.AnimateInsertion = true;

        foreach (GenerationChatRun run in batch.Runs)
        {
            reply.AddPendingImage(run.Id);
        }

        _conversationBatches.Add(new ConversationBatch(batch, user, reply));

        if (IsGroup || batch.Runs.Count > 1)
        {
            _variantReply = reply;
            reply.IsVariantChoice = true;
            user.IsVariantChoice = true;
        }

        _messages.Add(user);
        _messages.Add(reply);
        LockGroupMessages();
        RefreshGroupCards();
        UpdateDateLabels();
        NotifyRepeatState();

        foreach (GenerationChatRun run in batch.Runs)
        {
            _ = WatchResultAsync(user, reply, run, version);
        }
    }

    private void AppendVariants(ConversationBatch previous, GenerationChatBatch batch, long version)
    {
        _autoContinueSingle = false;
        int index = _conversationBatches.IndexOf(previous);
        GenerationChatBatch combined = previous.Generation with
        {
            Runs = previous.Generation.Runs.Concat(batch.Runs).ToArray()
        };
        _conversationBatches[index] = previous with { Generation = combined };
        _variantReply = previous.Reply;
        previous.User.IsTurnPending = true;
        previous.User.IsVariantChoice = true;
        previous.Reply.IsVariantChoice = true;

        foreach (GenerationChatRun run in batch.Runs)
        {
            previous.Reply.AddPendingImage(run.Id);
            _ = WatchResultAsync(previous.User, previous.Reply, run, version);
        }

        NotifyRepeatState();
    }

    private void NotifyRepeatState()
    {
        OnPropertyChanged(nameof(CanAddVariants));
        OnPropertyChanged(nameof(RepeatPrompt));
    }

    private async Task WatchResultAsync(
        GenerationChatMessageViewModel user, GenerationChatMessageViewModel reply, GenerationChatRun run, long version)
    {
        GenerationChatResult? result = await run.Completion;

        if (version != _sessionVersion)
        {
            return;
        }

        if (!reply.Images.Any(image => image.Id == run.Id))
        {
            return;
        }

        reply.CompleteImage(run.Id, result);
        user.IsTurnPending = reply.HasPendingResults;
        RefreshVariantChoices();
    }

    private GenerationItemViewModel? FindVariantCard(GenerationChatImageViewModel image)
    {
        return _gallery?.Items.FirstOrDefault(card =>
            (card.Id == image.Id || card.CorrelationId == image.Id) && card.IsGenerated && card.ChatResult?.Image == image.Reference);
    }

    private void RefreshVariantChoices()
    {
        if (_variantReply is { } reply)
        {
            foreach (GenerationChatImageViewModel image in reply.Images)
            {
                image.CanChoose = image is { IsPending: false, IsFailed: false }
                    && FindVariantCard(image) is not null;
            }
        }

        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanSubmit));
        NotifyRepeatState();
        OnPropertyChanged(nameof(AutoContinueImage));
    }

    private void CompleteSource(GenerationItemViewModel item)
    {
        if (!object.ReferenceEquals(item, _source) || item.IsGenerating || _pendingSourceImage is not Guid id)
        {
            return;
        }

        item.PropertyChanged -= OnSourceChanged;
        _pendingSourceImage = null;
        _messages[_sourceMessageCount - 1].CompleteImage(id, item.ChatResult);

        foreach (GenerationChatMessageViewModel message in _messages.Take(_sourceMessageCount))
        {
            message.IsTurnPending = false;
        }

        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanSubmit));
    }

    private async Task<GenerationChatHistory> ReadHistoryAsync(GenerationItemViewModel item, CancellationToken ct)
    {
        if (item.ChatHistory is null && item.ChatId is Guid id)
        {
            item.ChatHistory = await _chats.LoadAsync(id, ct);
        }

        GenerationChatHistory? history = item.ChatHistory;

        if (history is { IncludesCardTurn: true })
        {
            return history;
        }

        GenerationReference? image = item.ChatResult?.Image;

        if (image is not null || item.ImagePath is not { Length: > 0 } path)
        {
            return item.BuildChatHistory(image);
        }

        Guid storageLease = Guid.NewGuid();

        try
        {
            AttachedImageDto? importedImage = await _media.ReadResultAsync(path, ct);

            if (importedImage is not null)
            {
                IReadOnlyList<GenerationReference> stored = await _references.SaveAsync(
                    storageLease, new AttachedImageDto[] { importedImage }, ct);
                image = stored.Single();
            }

            GenerationChatHistory imported = item.BuildChatHistory(image);

            if (image is not null && _gallery?.Items.Contains(item) == true)
            {
                await WriteHistoriesAsync(new (GenerationItemViewModel Card, GenerationChatHistory History)[] { (item, imported) }, ct);
            }

            return imported;
        }
        finally
        {
            _references.Release(storageLease);
        }
    }

    private GenerationChatMessageViewModel CreateMessage(GenerationChatMessage message)
    {
        return new GenerationChatMessageViewModel(message, _media, _references, _formatter, _clipboard, SaveMessageAsync, ErrorHandler, Dialogs);
    }

    private async Task SaveMessageAsync(
        GenerationChatMessageViewModel message, GenerationChatMessage snapshot, CancellationToken ct)
    {
        int index = _messages.IndexOf(message);

        if (index < 0 || _source is not { } source)
        {
            return;
        }

        if (index < _sourceMessageCount)
        {
            GenerationChatMessage[] messages = _messages.Take(_sourceMessageCount).Select(item => item.Snapshot()).ToArray();
            messages[index] = snapshot;
            await WriteHistoriesAsync(new (GenerationItemViewModel Card, GenerationChatHistory History)[] { (source, new GenerationChatHistory(messages, includesCardTurn: true)) }, ct);

            return;
        }

        ConversationBatch batch = _conversationBatches.Single(candidate =>
                    object.ReferenceEquals(candidate.User, message) || object.ReferenceEquals(candidate.Reply, message));

        if (_gallery is null)
        {
            return;
        }

        List<(GenerationItemViewModel, GenerationChatHistory)> updates = [];
        HashSet<GenerationReference> variants = batch.Generation.Runs
            .Where(run => run.Completion.IsCompletedSuccessfully)
            .Select(run => run.Completion.Result?.Image)
            .OfType<GenerationReference>()
            .ToHashSet();

        foreach (GenerationChatRun run in batch.Generation.Runs)
        {
            GenerationItemViewModel? item = _gallery.Items.FirstOrDefault(card => card.Id == run.Id || card.CorrelationId == run.Id);

            if (item is null || item.IsGenerating)
            {
                continue;
            }

            GenerationChatHistory current = item.BuildChatHistory(item.ChatResult?.Image);
            GenerationChatMessage[] messages = current.Messages.ToArray();
            messages[snapshot.IsAssistant ? ^1 : ^2] = snapshot.IsAssistant && batch.Generation.Runs.Count > 1
                ? snapshot with
                {
                    ImageGeneration = item.ChatResult?.ImageGeneration,
                    Images = snapshot.Images.Where(image => image == item.ChatResult?.Image
                    || !variants.Contains(image)).ToArray()
                }
                : snapshot;
            updates.Add((item, new GenerationChatHistory(messages, includesCardTurn: true)));
        }

        await WriteHistoriesAsync(updates, ct);
    }

    private async Task WriteHistoriesAsync(
        IReadOnlyList<(GenerationItemViewModel Card, GenerationChatHistory History)> updates, CancellationToken ct)
    {
        if (_gallery is null || updates.Count == 0)
        {
            return;
        }

        (GenerationChatHistory? History, string Prompt, string? Reply)[] previous = updates
            .Select(update => (update.Card.ChatHistory, update.Card.Prompt, update.Card.TextReplyPreview)).ToArray();

        foreach ((GenerationItemViewModel card, GenerationChatHistory history) in updates)
        {
            card.ChatHistory = history;
            card.Prompt = history.Messages.LastOrDefault(message => !message.IsAssistant)?.Text ?? card.Prompt;

            if (history.Messages.LastOrDefault() is { IsAssistant: true } reply)
            {
                card.UpdateTextReply(reply.Text);
            }
        }

        try
        {
            await _gallery.SaveCurrentStateAsync(ct);
        }
        catch (Exception)
        {
            for (int index = 0; index < updates.Count; index++)
            {
                if (object.ReferenceEquals(updates[index].Card.ChatHistory, updates[index].History))
                {
                    updates[index].Card.ChatHistory = previous[index].History;
                    updates[index].Card.Prompt = previous[index].Prompt;

                    if (previous[index].Reply is { } text)
                    {
                        updates[index].Card.UpdateTextReply(text);
                    }
                }
            }

            throw;
        }
    }

    private void UpdateDateLabels()
    {
        DateOnly? previous = null;

        foreach (GenerationChatMessageViewModel message in _messages)
        {
            DateOnly date = _dates.GetDate(message.CreatedAtUtc);
            message.UpdateDateLabel(previous == date ? null : _dates.Format(message.CreatedAtUtc));
            previous = date;
        }
    }

    private void OnDatesChanged(object? sender, EventArgs e)
    {
        UpdateDateLabels();
    }

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationItemViewModel.IsGenerating) && sender is GenerationItemViewModel item)
        {
            CompleteSource(item);
        }
    }

    private void OnGalleryItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsGroup)
        {
            RefreshGroupCards();
            RefreshVariantChoices();
        }
        else if (_source is not null && _gallery?.Items.Contains(_source) == false)
        {
            Detach();
        }
        else if (_variantReply is not null)
        {
            RefreshVariantChoices();
        }
    }

    private sealed record ConversationBatch(
        GenerationChatBatch Generation, GenerationChatMessageViewModel User, GenerationChatMessageViewModel Reply);
}
