using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class GenerationChatMessageViewModel : AsyncOperationViewModel, IGenerationAttachmentStrip, IDisposable
{
    public bool IsAssistant { get; }
    public bool AnimateInsertion { get; internal set; }
    public DateTime CreatedAtUtc { get; }
    public string TimeText => CreatedAtUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
    public ReadOnlyObservableCollection<AttachedImageViewModel> AttachedImages { get; }
    public ReadOnlyObservableCollection<GenerationChatImageViewModel> Images { get; }
    public GenerationChatImageSet ImageSet { get; private set; } = new GenerationChatImageSet(Array.Empty<GenerationChatImageViewModel>());
    public bool HasReferenceThumbnails => !IsAssistant && _attachments.Count != 0;
    public bool ShowsReferenceStrip => IsEditing || HasReferenceThumbnails;
    public bool ShowsText => IsEditing || !string.IsNullOrWhiteSpace(Text);
    public bool ShowsAttachmentActions => IsEditing;
    public bool ShowsAttachmentStrip => !IsAssistant;
    public string AttachmentCounterText => _formatter.FormatAttachmentCounterText(
        _images.Count, LocalCodexImageModel.Option.MaxAttachedImages);
    public bool HasPendingResults => _images.Any(image => image.IsPending);
    public bool CanEdit => !IsTurnPending && !HasPendingResults && !IsVariantChoice;
    public int AttachmentInputByteLimit => _media.MaxInputImageBytes;
    public bool IsAttachmentLimitReached => !HasAvailableImageSlots;
    public bool HasDateLabel => !string.IsNullOrWhiteSpace(DateLabel);
    public string DisplayText
    {
        get => IsEditing ? EditText : Text;
        set
        {
            if (IsEditing)
            {
                EditText = value;
            }
        }
    }
    public ICommand? RemoveAttachment => IsEditing ? RemoveReferenceCommand : null;
    public ICommand? ReorderAttachment => IsEditing ? MoveReferenceCommand : null;

    private bool HasAvailableImageSlots => _images.Count < LocalCodexImageModel.Option.MaxAttachedImages;
    private bool CanPickImages => IsEditing && !IsLoading && HasAvailableImageSlots;
    private bool CanSave => IsEditing && !IsLoading;

    private readonly GenerationChatMediaService _media;
    private readonly IGenerationReferenceStore _references;
    private readonly NanoBanana2PanelTextFormatter _formatter;
    private readonly ITextClipboardService _clipboard;
    private readonly Func<GenerationChatMessageViewModel, GenerationChatMessage, CancellationToken, Task> _save;
    private readonly ObservableCollection<AttachedImageViewModel> _attachments = [];
    private readonly ObservableCollection<GenerationChatImageViewModel> _images = [];
    private readonly List<GenerationChatImageViewModel> _originalImages = [];
    private readonly Dictionary<AttachedImageViewModel, GenerationChatImageViewModel> _referenceSlots = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsText))]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string _text;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string _editText = string.Empty;
    [ObservableProperty]
    private bool _isCopied;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(ShowsText))]
    [NotifyPropertyChangedFor(nameof(ShowsReferenceStrip))]
    [NotifyPropertyChangedFor(nameof(ShowsAttachmentActions))]
    [NotifyPropertyChangedFor(nameof(RemoveAttachment))]
    [NotifyPropertyChangedFor(nameof(ReorderAttachment))]
    [NotifyCanExecuteChangedFor(nameof(PickImagesCommand))]
    [NotifyCanExecuteChangedFor(nameof(AttachImageInputsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isEditing;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    private bool _isTurnPending;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    private bool _isVariantChoice;
    private Guid _editLeaseId;
    private bool _isDisposed;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDateLabel))]
    private string? _dateLabel;

    public GenerationChatMessageViewModel(
        GenerationChatMessage message,
        GenerationChatMediaService media,
        IGenerationReferenceStore references,
        NanoBanana2PanelTextFormatter formatter,
        ITextClipboardService clipboard,
        Func<GenerationChatMessageViewModel, GenerationChatMessage, CancellationToken, Task> save,
        IViewModelErrorHandler errorHandler,
        IDialogService dialogs)
        : base(errorHandler, dialogs)
    {
        ArgumentNullException.ThrowIfNull(message);
        IsAssistant = message.IsAssistant;
        CreatedAtUtc = message.CreatedAtUtc;
        _text = message.Text;
        _media = media ?? throw new ArgumentNullException(nameof(media));
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        AttachedImages = new ReadOnlyObservableCollection<AttachedImageViewModel>(_attachments);
        Images = new ReadOnlyObservableCollection<GenerationChatImageViewModel>(_images);

        foreach (GenerationReference reference in message.Images)
        {
            AddImage(reference);
        }

        if (message.Failed)
        {
            AddImage(null);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        PickImagesCommand.Cancel();
        AttachImageInputsCommand.Cancel();
        CopyCommand.Cancel();
        SaveCommand.Cancel();

        if (IsEditing && !SaveCommand.IsRunning)
        {
            FinishEdit();
        }
    }

    string? IGenerationAttachmentStrip.PanelId => null;
    ICommand IGenerationAttachmentStrip.PickImage => PickImagesCommand;
    ICommand IGenerationAttachmentStrip.OpenAttachment => OpenReferenceCommand;

    internal void AddPendingImage(Guid id)
    {
        _images.Add(new GenerationChatImageViewModel(id, isPending: true));
        RefreshImages();
    }

    internal void CompleteImage(Guid id, GenerationChatResult? result)
    {
        GenerationChatImageViewModel slot = _images.Single(image => image.Id == id);
        slot.Complete(result?.Image);

        if (result is not null && string.IsNullOrWhiteSpace(Text))
        {
            Text = result.Text;
        }

        RefreshImages();
    }

    internal GenerationChatMessage Snapshot()
    {
        return CreateMessage(Text, IsEditing ? _originalImages : _images);
    }

    internal void UpdateDateLabel(string? label)
    {
        DateLabel = label;
        OnPropertyChanged(nameof(TimeText));
    }

    [RelayCommand]
    internal void Cancel()
    {
        if (!IsEditing || SaveCommand.IsRunning)
        {
            return;
        }

        PickImagesCommand.Cancel();
        AttachImageInputsCommand.Cancel();
        ReplaceImages(_originalImages);
        FinishEdit();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsLoading))
        {
            PickImagesCommand.NotifyCanExecuteChanged();
            AttachImageInputsCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private static void Synchronize<T>(ObservableCollection<T> collection, IReadOnlyList<T> desired)
    {
        for (int index = collection.Count - 1; index >= 0; index--)
        {
            if (!desired.Contains(collection[index]))
            {
                collection.RemoveAt(index);
            }
        }

        for (int index = 0; index < desired.Count; index++)
        {
            int previous = collection.IndexOf(desired[index]);

            if (previous < 0)
            {
                collection.Insert(index, desired[index]);
            }
            else if (previous != index)
            {
                collection.Move(previous, index);
            }
        }
    }

    private GenerationChatMessage CreateMessage(string text, IEnumerable<GenerationChatImageViewModel> images)
    {
        GenerationChatImageViewModel[] slots = images.ToArray();
        GenerationReference[] references = slots.Select(image => image.Reference).OfType<GenerationReference>().ToArray();
        bool failed = references.Length == 0 && slots.Any(image => image.IsFailed);

        return new GenerationChatMessage(IsAssistant, text, CreatedAtUtc, references, failed);
    }

    private void AddImage(GenerationReference? reference)
    {
        GenerationChatImageViewModel slot = new(Guid.NewGuid());
        slot.Complete(reference);
        AppendImage(slot);
        RefreshImages();
    }

    private void AppendImage(GenerationChatImageViewModel image)
    {
        _images.Add(image);

        if (!IsAssistant && image.Reference is { } reference)
        {
            AttachedImageViewModel attachment = new(reference);
            _referenceSlots.Add(attachment, image);
            _attachments.Add(attachment);
        }
    }

    private void RefreshImages()
    {
        if (!ImageSet.Images.SequenceEqual(_images))
        {
            ImageSet.IsCurrent = false;
            ImageSet = new GenerationChatImageSet(_images.ToArray());
            OnPropertyChanged(nameof(ImageSet));
        }

        OnPropertyChanged(nameof(HasReferenceThumbnails));
        OnPropertyChanged(nameof(ShowsReferenceStrip));
        OnPropertyChanged(nameof(AttachmentCounterText));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsAttachmentLimitReached));
        EditCommand.NotifyCanExecuteChanged();
        PickImagesCommand.NotifyCanExecuteChanged();
        AttachImageInputsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit()
    {
        if (IsEditing)
        {
            return;
        }

        EditText = Text;
        _originalImages.AddRange(_images);
        _editLeaseId = Guid.NewGuid();
        IsEditing = true;
    }

    [RelayCommand]
    private async Task CopyAsync(CancellationToken ct)
    {
        await ExecuteAsync(async token =>
        {
            await _clipboard.SetTextAsync(Text, token);
            IsCopied = true;
        }, nameof(CopyAsync), ct);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (!IsEditing || PickImagesCommand.IsRunning || AttachImageInputsCommand.IsRunning)
        {
            return;
        }

        GenerationChatMessage edited = CreateMessage(EditText.Trim(), _images);

        try
        {
            await ExecuteAsync(async token =>
            {
                await _save(this, edited, token);
                Text = edited.Text;
                FinishEdit();
            }, nameof(SaveAsync), ct);
        }
        finally
        {
            if (_isDisposed && IsEditing)
            {
                FinishEdit();
            }
        }
    }

    private void FinishEdit()
    {
        IsEditing = false;
        _references.Release(_editLeaseId);
        _originalImages.Clear();
    }

    private void ReplaceImages(IEnumerable<GenerationChatImageViewModel> images)
    {
        GenerationChatImageViewModel[] desired = images.ToArray();
        Dictionary<GenerationChatImageViewModel, AttachedImageViewModel> previous =
            _referenceSlots.ToDictionary(pair => pair.Value, pair => pair.Key);
        GenerationChatMessageViewModel.Synchronize(_images, desired);
        AttachedImageViewModel[] attachments = IsAssistant
            ? Array.Empty<AttachedImageViewModel>()
            : desired.Where(image => image.Reference is not null)
                .Select(image => previous.TryGetValue(image, out AttachedImageViewModel? attachment)
                    ? attachment
                    : new AttachedImageViewModel(image.Reference
                        ?? throw new InvalidOperationException("The restored attachment has no image reference.")))
                .ToArray();
        GenerationChatMessageViewModel.Synchronize(_attachments, attachments);
        _referenceSlots.Clear();
        int index = 0;

        foreach (GenerationChatImageViewModel image in desired.Where(image => !IsAssistant && image.Reference is not null))
        {
            _referenceSlots.Add(attachments[index++], image);
        }

        RefreshImages();
    }

    [RelayCommand(CanExecute = nameof(CanPickImages))]
    private async Task PickImagesAsync(CancellationToken ct)
    {
        if (!IsEditing)
        {
            return;
        }

        Guid lease = _editLeaseId;
        await ExecuteAsync(async token =>
        {
            ImageModelOption model = LocalCodexImageModel.Option;
            long usedBytes = await _media.GetTotalReferenceBytesAsync(
                _images.Select(image => image.Reference).OfType<GenerationReference>().ToArray(), token);
            IReadOnlyList<AttachedImageDto> images = await _media.PickAsync(
                model, model.MaxAttachedImages - _images.Count, model.MaxTotalAttachedImageBytes - usedBytes, token);
            await StoreImagesAsync(lease, images, token);
        }, nameof(PickImagesAsync), ct);
    }

    private bool CanAttachImageInputs(IReadOnlyList<ImageAttachmentInput>? inputs)
    {
        return !_isDisposed && CanPickImages;
    }

    [RelayCommand(CanExecute = nameof(CanAttachImageInputs))]
    private async Task AttachImageInputsAsync(IReadOnlyList<ImageAttachmentInput>? inputs, CancellationToken ct)
    {
        if (inputs is null)
        {
            return;
        }

        Guid lease = _editLeaseId;
        bool transferred = false;
        await ExecuteAsync(async token =>
        {
            try
            {
                ImageModelOption model = LocalCodexImageModel.Option;
                long used = await _media.GetTotalReferenceBytesAsync(
                    _images.Select(image => image.Reference).OfType<GenerationReference>().ToArray(), token);
                transferred = true;
                IReadOnlyList<AttachedImageDto> images = await _media.PrepareAsync(
                    model, inputs, model.MaxAttachedImages - _images.Count, model.MaxTotalAttachedImageBytes - used, token);
                await StoreImagesAsync(lease, images, token);
            }
            finally
            {
                if (!transferred)
                {
                    foreach (ImageAttachmentInput input in inputs)
                    {
                        input.Dispose();
                    }
                }
            }
        }, nameof(AttachImageInputsAsync), ct);
    }

    private async Task StoreImagesAsync(Guid lease, IReadOnlyList<AttachedImageDto> images, CancellationToken ct)
    {
        if (!IsEditing || lease != _editLeaseId)
        {
            return;
        }

        IReadOnlyList<GenerationReference> stored = await _references.SaveAsync(lease, images, ct);

        if (!IsEditing || lease != _editLeaseId)
        {
            _references.Release(lease);
            return;
        }

        foreach (GenerationReference reference in stored)
        {
            AddImage(reference);
        }
    }

    [RelayCommand]
    private void HandleAttachmentError(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        ErrorHandler.Log(exception, nameof(AttachImageInputsAsync));
        ErrorMessage = ErrorHandler.GetUserMessage(exception);
        Dialogs.ShowLocalizedError(GenerationUiLocalizationKeys.Attachments.Failed);
    }

    [RelayCommand]
    private void RemoveReference(AttachedImageViewModel? image)
    {
        if (IsEditing && image is not null && _referenceSlots.Remove(image, out GenerationChatImageViewModel? slot))
        {
            _attachments.Remove(image);
            _images.Remove(slot);
            RefreshImages();
        }
    }

    [RelayCommand]
    private void RemoveResult(GenerationChatImageViewModel? image)
    {
        if (IsEditing && image is not null)
        {
            _images.Remove(image);
            RefreshImages();
        }
    }

    [RelayCommand]
    private void MoveReference(AttachedImageReorderRequest? request)
    {
        if (!IsEditing || request is null)
        {
            return;
        }

        int fromIndex = _attachments.IndexOf(request.AttachedImage);

        if (fromIndex >= 0 && request.TargetIndex >= 0 && request.TargetIndex < _attachments.Count)
        {
            int fromSlot = _images.IndexOf(_referenceSlots[request.AttachedImage]);
            int targetSlot = _images.IndexOf(_referenceSlots[_attachments[request.TargetIndex]]);
            _attachments.Move(fromIndex, request.TargetIndex);
            _images.Move(fromSlot, targetSlot);
        }
    }

    [RelayCommand]
    private Task OpenReferenceAsync(AttachedImageViewModel? image, CancellationToken ct)
    {
        return image is not null && _referenceSlots.TryGetValue(image, out GenerationChatImageViewModel? slot)
            ? OpenResultAsync(slot, ct)
            : Task.CompletedTask;
    }

    [RelayCommand]
    private Task OpenResultAsync(GenerationChatImageViewModel? image, CancellationToken ct)
    {
        return image is not null ? OpenImageAsync(_images.IndexOf(image), ct) : Task.CompletedTask;
    }

    private Task OpenImageAsync(int imageIndex, CancellationToken ct)
    {
        if (imageIndex < 0 || _images[imageIndex].Reference is null)
        {
            return Task.CompletedTask;
        }

        GenerationReference[] references = _images.Select(image => image.Reference).OfType<GenerationReference>().ToArray();
        int selectedIndex = _images.Take(imageIndex).Count(image => image.Reference is not null);

        return ExecuteAsync(token => _media.OpenAsync(references, selectedIndex, token), nameof(OpenImageAsync), ct);
    }
}
