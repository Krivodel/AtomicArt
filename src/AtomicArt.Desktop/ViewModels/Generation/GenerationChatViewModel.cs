using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatViewModel : AsyncOperationViewModel, IDisposable
{
    public ReadOnlyObservableCollection<GenerationChatMessageViewModel> Messages => _active?.Messages ?? EmptyMessages;
    public bool HasChat => _active?.HasChat == true;
    public bool IsBusy => IsLoading || _active?.IsBusy == true;
    public bool CanSubmit => !IsLoading && _active?.CanSubmit == true;
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
        GenerationChatDateFormatter dates)
        : base(errorHandler, dialogs)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(formatter);
        _formatter = formatter;
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(chats);
        ArgumentNullException.ThrowIfNull(dates);
        _dates = dates;
        _dates.Changed += OnGroupLabelsChanged;
        BranchMap = new GenerationChatBranchMapViewModel(OpenCommand);
        _textSizes = textSizeController ?? throw new ArgumentNullException(nameof(textSizeController));
        _textSizes.TextSizeChanged += OnTextSizeChanged;
        _createSession = () => new GenerationChatSessionViewModel(
            media, references, formatter, clipboard, errorHandler, dialogs, chats, dates);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
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

    internal static GenerationChatHistory BuildHistory(GenerationItemViewModel item, GenerationReference? image)
    {
        return GenerationChatSessionViewModel.BuildHistory(item, image);
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
            ChooseVariantCommand.NotifyCanExecuteChanged();
            ReturnToSourceCommand.NotifyCanExecuteChanged();
            OpenGroupCommand.NotifyCanExecuteChanged();
            BranchMap.RefreshCommands();
        }
    }

    private bool CanOpen(GenerationItemViewModel? item)
    {
        return !_isDisposed && !IsLoading && item is { SupportsChat: true };
    }

    [RelayCommand]
    private Task AdjustTextSizeAsync(TextSizeAdjustment adjustment, CancellationToken ct)
    {
        return ExecuteAsync(token => _textSizes.AdjustAsync(adjustment, token), nameof(AdjustTextSizeAsync), ct);
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private async Task OpenAsync(GenerationItemViewModel? item, CancellationToken ct)
    {
        if (item is not { SupportsChat: true } || _panel is null)
        {
            return;
        }

        GenerationPanelPreset preset = new(item.ModelId, string.Empty, item.AspectRatio, item.Resolution,
            item.RequestSettings, References: null);

        if (!_panel.CanApplyPreset(preset))
        {
            await Dialogs.ShowLocalizedErrorAsync(SettingsLocalizationKeys.LocalCodex.Unavailable, ct);
            return;
        }

        await ExecuteAsync(token => OpenSessionAsync(item, preset, token), nameof(OpenAsync), ct);
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

    private async Task OpenSessionAsync(GenerationItemViewModel item, GenerationPanelPreset? preset, CancellationToken ct)
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

        if (_isDisposed || version != _openVersion || !_gallery.Items.Contains(item))
        {
            return;
        }

        Activate(session);
        BranchMap.Close();
        IsExpanded = true;
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
    }

    [RelayCommand]
    private void Toggle()
    {
        if (HasChat)
        {
            IsExpanded = !IsExpanded;
        }
    }

    [RelayCommand]
    private void Expand()
    {
        if (HasChat)
        {
            IsExpanded = true;
        }
    }

    [RelayCommand(CanExecute = nameof(HasChat))]
    private void OpenBranchMap()
    {
        if (_gallery is not null && _active?.Source is { } source)
        {
            BranchMap.Open(_gallery.Items, source.Id, source.Id);
        }
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GenerationChatSessionViewModel.HasGroupVariants))
        {
            RefreshGroupLinks();

            if (_active is { IsGroup: true, HasGroupVariants: false })
            {
                if (CanReturnToSource)
                {
                    ReturnToSourceCommand.Execute(null);
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

        if (!IsLoading && _active?.AutoContinueImage is { } image && ChooseVariantCommand.CanExecute(image))
        {
            ChooseVariantCommand.Execute(image);
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

        if (BranchMap.IsOpen && _gallery is not null)
        {
            BranchMap.Refresh(_gallery.Items);
        }
    }
}
