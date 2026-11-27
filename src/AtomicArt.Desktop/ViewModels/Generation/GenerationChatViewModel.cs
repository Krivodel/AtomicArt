using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatViewModel : AsyncOperationViewModel, IDisposable
{
    public ReadOnlyObservableCollection<GenerationChatMessageViewModel> Messages => _active?.Messages ?? EmptyMessages;
    public bool HasChat => _active?.HasChat == true;
    public bool IsBusy => IsLoading || _active?.IsBusy == true;
    public bool CanSubmit => !IsLoading && BranchMap.IsGenerationPanelVisible && _active?.CanSubmit == true;
    public bool CanAddVariants => _active?.CanAddVariants == true;
    public string? RepeatPrompt => _active?.RepeatPrompt;
    public long ConversationVersion => _conversationVersion;
    public double TextSize => _textSizes.CurrentTextSize;
    public GenerationChatBranchMapViewModel BranchMap { get; }
    public bool CanReturnToSource => _active?.Group is { } group
        && _gallery?.Items.Any(card => card.Id == group.SourceCardId) == true;

    private static readonly ReadOnlyObservableCollection<GenerationChatMessageViewModel> EmptyMessages =
        new(new ObservableCollection<GenerationChatMessageViewModel>());

    private readonly Func<GenerationChatSessionViewModel> _createSession;
    private readonly IChatTextSizeController _textSizes;
    private readonly NanoBanana2PanelTextFormatter _formatter;
    private readonly GenerationChatDateFormatter _dates;
    private readonly IAnimatedGalleryOperations _galleryOperations;
    private readonly IGenerationChatStore _chats;
    private readonly IGenerationReferenceStore _references;
    private readonly GenerationChatMediaService _media;
    private readonly Dictionary<Guid, GenerationChatSessionViewModel> _sessions = [];
    private readonly Dictionary<Guid, GenerationChatSessionViewModel> _groups = [];
    [ObservableProperty]
    private bool _isExpanded;
    private GenerationChatSessionViewModel? _active;
    private GalleryViewModel? _gallery;
    private IGenerationPanelPresetTarget? _panel;
    private long _conversationVersion;
    private long _openVersion;
    private bool _isDisposed;

    public GenerationChatViewModel(
        GenerationChatMediaService media,
        IGenerationReferenceStore references,
        NanoBanana2PanelTextFormatter formatter,
        ITextClipboardService clipboard,
        IViewModelErrorHandler errorHandler,
        IDialogService dialogs,
        IChatTextSizeController textSizeController,
        IGenerationChatStore chats,
        GenerationChatDateFormatter dates,
        IAnimatedGalleryOperations galleryOperations)
        : base(errorHandler, dialogs)
    {
        _media = media ?? throw new ArgumentNullException(nameof(media));
        _references = references ?? throw new ArgumentNullException(nameof(references));
        ArgumentNullException.ThrowIfNull(formatter);
        _formatter = formatter;
        ArgumentNullException.ThrowIfNull(clipboard);
        _chats = chats ?? throw new ArgumentNullException(nameof(chats));
        ArgumentNullException.ThrowIfNull(dates);
        _dates = dates;
        _galleryOperations = galleryOperations ?? throw new ArgumentNullException(nameof(galleryOperations));
        _dates.Changed += OnGroupLabelsChanged;
        BranchMap = new GenerationChatBranchMapViewModel(OpenCommand, SelectBranchCommand, OpenBranchImageCommand);
        BranchMap.PropertyChanged += OnBranchMapChanged;
        _textSizes = textSizeController ?? throw new ArgumentNullException(nameof(textSizeController));
        _textSizes.TextSizeChanged += OnTextSizeChanged;
        _createSession = () => new GenerationChatSessionViewModel(
            _media, references, formatter, clipboard, errorHandler, dialogs, chats, dates);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        BranchMap.PropertyChanged -= OnBranchMapChanged;
        BranchMap.Dispose();
        _textSizes.TextSizeChanged -= OnTextSizeChanged;
        _dates.Changed -= OnGroupLabelsChanged;

        if (_gallery is not null)
        {
            ((INotifyCollectionChanged)_gallery.Items).CollectionChanged -= OnGalleryItemsChanged;
        }

        Detach();

        foreach (GenerationChatSessionViewModel session in _sessions.Values.Concat(_groups.Values))
        {
            session.PropertyChanged -= OnGroupChanged;
            session.Dispose();
        }

        _sessions.Clear();
        _groups.Clear();
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
        return _active?.IsForModel(modelId) == true;
    }

    [RelayCommand]
    internal void Collapse()
    {
        BranchMap.Close();
        IsExpanded = false;
    }

    internal void Detach()
    {
        BranchMap.Close();
        _openVersion++;
        Activate(null);
        IsExpanded = false;
    }

    internal async Task SubmitAsync(
        NanoBanana2GenerationParameters parameters,
        Func<NanoBanana2GenerationParameters, CancellationToken, Task> submit,
        CancellationToken ct)
    {
        if (_active is not { } session || _gallery is null || _panel is null)
        {
            return;
        }

        GenerationChatSessionViewModel? original = null;

        if (!session.IsGroup && session.Source is { } source)
        {
            original = session;
            session = _createSession();
            session.Connect(_gallery, _panel);

            try
            {
                await session.OpenSessionAsync(source, null, ct);

                if (_isDisposed || !session.HasChat)
                {
                    session.Dispose();
                    return;
                }

                session.BeginGroup();
                session.PropertyChanged += OnGroupChanged;
                _groups.Add(session.GroupId.GetValueOrDefault(), session);

                if (object.ReferenceEquals(_active, original))
                {
                    Activate(session);
                }
            }
            catch (Exception)
            {
                session.Dispose();
                throw;
            }
        }

        try
        {
            await session.SubmitAsync(parameters, submit, ct);
        }
        catch (Exception)
        {
            if (session.IsGroup && session.Group is null)
            {
                _groups.Remove(session.GroupId.GetValueOrDefault());

                if (object.ReferenceEquals(_active, session))
                {
                    Activate(original?.HasChat == true ? original : null);
                }

                session.PropertyChanged -= OnGroupChanged;
                session.Dispose();
            }

            throw;
        }

        RefreshGroupLinks();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsLoading))
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanSubmit));
            OpenCommand.NotifyCanExecuteChanged();
            SelectBranchCommand.NotifyCanExecuteChanged();
            ChooseVariantCommand.NotifyCanExecuteChanged();
            ReturnToSourceCommand.NotifyCanExecuteChanged();
            OpenGroupCommand.NotifyCanExecuteChanged();
            NavigateInBackgroundCommand.NotifyCanExecuteChanged();
            ShowInGalleryCommand.NotifyCanExecuteChanged();
            BranchMap.RefreshCommands();
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenBranchImageAsync(GenerationItemViewModel? card, CancellationToken ct)
    {
        if (card is not { ShowsGeneratedImage: true })
        {
            return;
        }

        if (_gallery?.Items.Contains(card) == true && _gallery.OpenViewerCommand.CanExecute(card))
        {
            await _gallery.OpenViewerCommand.ExecuteAsync(card);
        }
        else if (card.ChatResult?.Image is { } image)
        {
            await ExecuteAsync(token => _media.OpenAsync(new GenerationReference[] { image }, 0, token),
                nameof(OpenBranchImageAsync), ct);
        }
    }

    private bool CanOpen(GenerationItemViewModel? item)
    {
        return !_isDisposed && !IsLoading && item is { SupportsChat: true };
    }

    private bool CanShowInGallery()
    {
        return !_isDisposed && !IsLoading && _active?.Source is { } source && _gallery?.Items.Contains(source) == true;
    }

    [RelayCommand(CanExecute = nameof(CanShowInGallery))]
    private async Task ShowInGalleryAsync(CancellationToken ct)
    {
        if (_active?.Source is not { } source || _gallery?.Items.Contains(source) != true)
        {
            return;
        }

        Collapse();
        await ExecuteAsync(token => _galleryOperations.RevealAsync(source.Id, token), nameof(ShowInGalleryAsync), ct);
    }

    [RelayCommand]
    private Task AdjustTextSizeAsync(TextSizeAdjustment adjustment, CancellationToken ct)
    {
        return ExecuteAsync(token => _textSizes.AdjustAsync(adjustment, token), nameof(AdjustTextSizeAsync), ct);
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private Task OpenAsync(GenerationItemViewModel? item, CancellationToken ct)
    {
        return OpenWithPresetAsync(item, GenerationChatNavigationMode.ShowChat, ct);
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private Task SelectBranchAsync(GenerationItemViewModel? item, CancellationToken ct)
    {
        return OpenWithPresetAsync(item, GenerationChatNavigationMode.ShowBranchComposer, ct);
    }

    private async Task OpenWithPresetAsync(GenerationItemViewModel? item,
        GenerationChatNavigationMode navigation, CancellationToken ct)
    {
        if (item is not { SupportsChat: true } || _panel is null)
        {
            return;
        }

        GenerationPanelPreset preset = new(item.ModelId, null, item.AspectRatio, item.Resolution,
            item.RequestSettings, References: null);

        if (!_panel.CanApplyPreset(preset))
        {
            await Dialogs.ShowLocalizedErrorAsync(SettingsLocalizationKeys.LocalCodex.Unavailable, ct);
            return;
        }

        await ExecuteAsync(token => OpenSessionAsync(item, preset, navigation, token), nameof(OpenWithPresetAsync), ct);
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private Task NavigateInBackgroundAsync(GenerationItemViewModel? item, CancellationToken ct)
    {
        return item is null ? Task.CompletedTask : ExecuteAsync(
            token => OpenSessionAsync(item, null, GenerationChatNavigationMode.KeepPresentation, token),
            nameof(NavigateInBackgroundAsync), ct);
    }

    [RelayCommand(CanExecute = nameof(CanChooseVariant))]
    private async Task ChooseVariantAsync(GenerationChatImageViewModel? image, CancellationToken ct)
    {
        if (image is not null && _active?.GetVariantCard(image) is { } card)
        {
            await OpenAsync(card, ct);
        }
    }

    private bool CanChooseVariant(GenerationChatImageViewModel? image)
    {
        return !IsLoading && image is { CanChoose: true } && _active?.GetVariantCard(image) is not null;
    }

    private async Task OpenSessionAsync(GenerationItemViewModel item, GenerationPanelPreset? preset,
        GenerationChatNavigationMode navigation, CancellationToken ct)
    {
        long version = ++_openVersion;

        if (_gallery is null || _panel is null || !_gallery.Items.Contains(item))
        {
            return;
        }

        if (!_sessions.TryGetValue(item.Id, out GenerationChatSessionViewModel? session))
        {
            session = _createSession();
            session.Connect(_gallery, _panel);

            try
            {
                await session.OpenSessionAsync(item, preset, ct);

                if (_isDisposed || !session.HasChat)
                {
                    session.Dispose();
                    return;
                }

                _sessions.Add(item.Id, session);
            }
            catch (Exception)
            {
                session.Dispose();
                throw;
            }
        }
        else if (preset is not null && !await _panel.TryApplyPresetAsync(preset, ct))
        {
            return;
        }

        ct.ThrowIfCancellationRequested();

        if (_isDisposed || version != _openVersion || !_gallery.Items.Contains(item)
            || (navigation == GenerationChatNavigationMode.ShowBranchComposer && !BranchMap.IsOpen))
        {
            return;
        }

        if (navigation == GenerationChatNavigationMode.ShowBranchComposer && BranchMap.IsOpeningChat(item))
        {
            navigation = GenerationChatNavigationMode.ShowChat;
        }

        Activate(session);

        if (navigation == GenerationChatNavigationMode.ShowChat)
        {
            BranchMap.Close();
            IsExpanded = true;
        }
        else if (navigation == GenerationChatNavigationMode.ShowBranchComposer)
        {
            BranchMap.ShowComposer(item);
            IsExpanded = true;
        }
    }

    private void Activate(GenerationChatSessionViewModel? session)
    {
        if (object.ReferenceEquals(session, _active))
        {
            return;
        }

        OnPropertyChanging(nameof(ConversationVersion));

        if (_active is { } previous)
        {
            previous.PropertyChanged -= OnSessionChanged;

            foreach (GenerationChatMessageViewModel message in previous.Messages)
            {
                message.CancelCommand.Execute(null);
            }
        }

        _active = session;

        if (session is not null)
        {
            session.PropertyChanged += OnSessionChanged;
        }

        if (BranchMap.IsOpen && session?.Source is { } source)
        {
            BranchMap.UpdateCurrentChat(source);
            _ = RestoreBranchPreviewsAsync(CancellationToken.None);
        }

        _conversationVersion++;
        OnPropertyChanged(nameof(Messages));
        OnPropertyChanged(nameof(HasChat));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(CanAddVariants));
        OnPropertyChanged(nameof(RepeatPrompt));
        OnPropertyChanged(nameof(ConversationVersion));
        OnPropertyChanged(nameof(CanReturnToSource));
        ReturnToSourceCommand.NotifyCanExecuteChanged();
        RefreshGroupLinks();
        OpenBranchMapCommand.NotifyCanExecuteChanged();
        ShowInGalleryCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Toggle()
    {
        if (HasChat)
        {
            if (BranchMap.IsOpen)
            {
                Expand();
            }
            else
            {
                IsExpanded = !IsExpanded;
            }
        }
    }

    [RelayCommand]
    private void Expand()
    {
        if (HasChat)
        {
            BranchMap.Close();
            IsExpanded = true;
        }
    }

    [RelayCommand(CanExecute = nameof(HasChat))]
    private async Task OpenBranchMapAsync(CancellationToken ct)
    {
        if (_gallery is not null && _active?.Source is { } source)
        {
            BranchMap.Open(_gallery.Items, source.Id, source.Id);
            await RestoreBranchPreviewsAsync(ct);
        }
    }

    private Task RestoreBranchPreviewsAsync(CancellationToken ct)
    {
        return ViewModelAsyncOperation.ExecuteAsync(ErrorHandler, value => ErrorMessage = value, _ => { },
            token => BranchMap.RestoreDeletedPreviewsAsync(_chats, _references, token), nameof(OpenBranchMapAsync), ct);
    }

    private void OnBranchMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationChatBranchMapViewModel.IsGenerationPanelVisible))
        {
            OnPropertyChanged(nameof(CanSubmit));
        }
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationChatSessionViewModel.HasGroupVariants))
        {
            RefreshGroupLinks();

            if (!IsLoading && _active is { IsGroup: true, HasGroupVariants: false })
            {
                if (_gallery?.Items.FirstOrDefault(card => card.Id == _active.Group?.SourceCardId) is { } source)
                {
                    NavigateInBackgroundCommand.Execute(source);
                }
                else
                {
                    Detach();
                }
            }
        }

        if (e.PropertyName is nameof(GenerationChatSessionViewModel.IsBusy) or nameof(GenerationChatSessionViewModel.CanSubmit))
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanSubmit));
            ChooseVariantCommand.NotifyCanExecuteChanged();
        }

        if (e.PropertyName is nameof(GenerationChatSessionViewModel.CanAddVariants) or nameof(GenerationChatSessionViewModel.RepeatPrompt))
        {
            OnPropertyChanged(nameof(CanAddVariants));
            OnPropertyChanged(nameof(RepeatPrompt));
        }

        OnPropertyChanged(nameof(CanReturnToSource));
        ReturnToSourceCommand.NotifyCanExecuteChanged();

        if (!IsLoading && _active?.AutoContinueImage is { } image && _active.GetVariantCard(image) is { } card
            && NavigateInBackgroundCommand.CanExecute(card))
        {
            NavigateInBackgroundCommand.Execute(card);
        }
    }

    private void OnTextSizeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TextSize));
    }

    private void OnGalleryItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        KeyValuePair<Guid, GenerationChatSessionViewModel>[] removed = _sessions
            .Where(pair => pair.Value.Source is not { } source || _gallery?.Items.Contains(source) != true).ToArray();

        foreach (KeyValuePair<Guid, GenerationChatSessionViewModel> entry in removed)
        {
            GenerationChatSessionViewModel session = entry.Value;

            if (object.ReferenceEquals(session, _active))
            {
                Detach();
            }

            _sessions.Remove(entry.Key);
            session.Dispose();
        }

        RefreshGroupLinks();
        OnPropertyChanged(nameof(CanReturnToSource));
        ReturnToSourceCommand.NotifyCanExecuteChanged();
        ShowInGalleryCommand.NotifyCanExecuteChanged();

        if (BranchMap.IsOpen && _gallery is not null)
        {
            BranchMap.Refresh(_gallery.Items);
            _ = RestoreBranchPreviewsAsync(CancellationToken.None);
        }
    }
}
