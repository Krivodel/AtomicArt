using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed class NanoBanana2AttachmentsViewModel : ObservableObject, IGenerationModelViewModel
{
    private readonly INanoBanana2AttachmentValidator _attachmentValidator;
    private readonly IAttachedImagePreparationService _attachmentPreparationService;
    private readonly IPanelAttachmentStore _attachmentStore;
    private readonly ObservableCollection<AttachedImageViewModel> _attachedImages = [];
    private readonly Dictionary<Guid, CancellationTokenSource> _preparationCancellations = [];

    public ReadOnlyObservableCollection<AttachedImageViewModel> AttachedImages { get; }
    public bool HasPendingAttachments => _preparationCancellations.Count > 0;

    public event EventHandler<AttachmentStateChangedEventArgs>? AttachmentStateChanged;

    public NanoBanana2AttachmentsViewModel(
        INanoBanana2AttachmentValidator attachmentValidator,
        IAttachedImagePreparationService attachmentPreparationService,
        IPanelAttachmentStore attachmentStore)
    {
        ArgumentNullException.ThrowIfNull(attachmentValidator);
        ArgumentNullException.ThrowIfNull(attachmentPreparationService);
        ArgumentNullException.ThrowIfNull(attachmentStore);

        _attachmentValidator = attachmentValidator;
        _attachmentPreparationService = attachmentPreparationService;
        _attachmentStore = attachmentStore;
        AttachedImages = new ReadOnlyObservableCollection<AttachedImageViewModel>(_attachedImages);
    }

    public async Task AttachInputsAsync(
        string panelId,
        ImageModelOption selectedModel,
        IReadOnlyList<ImageAttachmentInput>? inputs,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(selectedModel);

        if (inputs is null || inputs.Count == 0)
        {
            return;
        }

        ct.ThrowIfCancellationRequested();
        List<Task> preparationTasks = [];

        foreach (ImageAttachmentInput input in inputs)
        {
            if (input is null)
            {
                NotifyStateChanged(AttachmentStateChangeKind.Failed);
                continue;
            }

            if (_attachedImages.Count >= selectedModel.MaxAttachedImages)
            {
                input.Dispose();
                NotifyStateChanged(AttachmentStateChangeKind.Failed);
                continue;
            }

            AttachedImageViewModel pendingImage = AttachedImageViewModel.CreateLoading(input.FileName);
            CancellationTokenSource cancellation = new();
            _preparationCancellations.Add(pendingImage.Id, cancellation);
            _attachedImages.Add(pendingImage);
            NotifyStateChanged(AttachmentStateChangeKind.PendingAdded);
            preparationTasks.Add(ProcessAttachmentAsync(
                panelId,
                pendingImage,
                input,
                selectedModel,
                cancellation));
        }

        foreach (Task preparationTask in preparationTasks)
        {
            await preparationTask;
        }
    }

    public async Task RestoreAsync(
        string panelId,
        ImageModelOption selectedModel,
        IReadOnlyList<PanelAttachmentState> attachments,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(selectedModel);
        ArgumentNullException.ThrowIfNull(attachments);

        List<AttachedImageDto> loadedImages = [];
        List<PanelAttachmentState> loadedStates = [];

        foreach (PanelAttachmentState attachment in attachments)
        {
            AttachedImageDto? image = await _attachmentStore
                .LoadAsync(panelId, attachment, ct);

            if (image is null)
            {
                continue;
            }

            loadedImages.Add(image);
            loadedStates.Add(attachment);
        }

        IReadOnlyList<AttachedImageDto>? validatedImages = _attachmentValidator.CreateValidatedAttachments(
            selectedModel,
            [],
            loadedImages);

        if (validatedImages is null)
        {
            _attachedImages.Clear();
            NotifyStateChanged(AttachmentStateChangeKind.Failed);
            return;
        }

        List<AttachedImageViewModel> restoredImages = [];

        for (int index = 0; index < loadedImages.Count; index++)
        {
            restoredImages.Add(new AttachedImageViewModel(loadedImages[index], loadedStates[index]));
        }

        ReplaceAttachedImages(restoredImages);
    }

    public async Task RemoveAttachmentAsync(
        string panelId,
        AttachedImageViewModel attachedImage,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        ArgumentNullException.ThrowIfNull(attachedImage);

        bool wasRemoved = _attachedImages.Remove(attachedImage);
        if (!wasRemoved)
        {
            return;
        }

        if (_preparationCancellations.Remove(
                attachedImage.Id,
                out CancellationTokenSource? cancellation))
        {
            cancellation.Cancel();
            NotifyStateChanged(AttachmentStateChangeKind.Canceled);
            return;
        }

        PanelAttachmentState state = attachedImage.State
            ?? throw new InvalidOperationException("Ready attachment has no persisted state.");
        await _attachmentStore
            .DeleteAsync(panelId, state, ct);
        NotifyStateChanged(AttachmentStateChangeKind.Removed);
    }

    public void MoveAttachment(AttachedImageViewModel attachedImage, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(attachedImage);

        int sourceIndex = _attachedImages.IndexOf(attachedImage);
        if (sourceIndex < 0)
        {
            return;
        }

        int clampedTargetIndex = Math.Clamp(targetIndex, 0, _attachedImages.Count - 1);
        if (sourceIndex == clampedTargetIndex)
        {
            return;
        }

        _attachedImages.Move(sourceIndex, clampedTargetIndex);
    }

    public IReadOnlyList<AttachedImageDto> GetAttachedImageDtos()
    {
        return GetReadyAttachedImages()
            .Select(attachedImage => attachedImage.ToDto())
            .ToList();
    }

    public IReadOnlyList<PanelAttachmentState> GetAttachmentStates()
    {
        return GetReadyAttachedImages()
            .Select(attachedImage => attachedImage.State
                ?? throw new InvalidOperationException("Ready attachment has no persisted state."))
            .ToList();
    }

    public IReadOnlyList<AttachedImageViewModel> GetReadyAttachedImages()
    {
        return _attachedImages
            .Where(attachedImage => attachedImage.IsReady)
            .ToList();
    }

    private void ReplaceAttachedImages(IReadOnlyList<AttachedImageViewModel> images)
    {
        _attachedImages.Clear();

        foreach (AttachedImageViewModel image in images)
        {
            _attachedImages.Add(image);
        }
    }

    private async Task ProcessAttachmentAsync(
        string panelId,
        AttachedImageViewModel pendingImage,
        ImageAttachmentInput input,
        ImageModelOption selectedModel,
        CancellationTokenSource cancellation)
    {
        PanelAttachmentState? savedState = null;

        try
        {
            AttachedImageDto? image = await input.ReadAsync(cancellation.Token);

            if (image is null)
            {
                FailPreparation(pendingImage, cancellation, null);
                return;
            }

            AttachedImageDto? preparedImage = await _attachmentPreparationService.PrepareAsync(
                image,
                selectedModel,
                cancellation.Token);

            if (preparedImage is null)
            {
                FailPreparation(pendingImage, cancellation, null);
                return;
            }

            if (cancellation.IsCancellationRequested
                || !_attachedImages.Contains(pendingImage))
            {
                CancelPreparation(pendingImage, cancellation);
                return;
            }

            IReadOnlyList<AttachedImageDto>? validatedImages =
                _attachmentValidator.CreateValidatedAttachments(
                    selectedModel,
                    GetAttachedImageDtos(),
                    [preparedImage]);

            if (validatedImages is null)
            {
                FailPreparation(pendingImage, cancellation, null);
                return;
            }

            savedState = _attachmentStore.CreateState(preparedImage);
            await _attachmentStore.SaveAsync(
                panelId,
                savedState,
                preparedImage,
                cancellation.Token);

            if (cancellation.IsCancellationRequested
                || !_attachedImages.Contains(pendingImage))
            {
                await _attachmentStore.DeleteAsync(
                    panelId,
                    savedState,
                    CancellationToken.None);
                CancelPreparation(pendingImage, cancellation);
                return;
            }

            AttachedImageDto managedImage = new(
                savedState.FileName,
                savedState.ContentType,
                preparedImage.Content);
            pendingImage.Complete(managedImage, savedState);
            CompletePreparation(pendingImage, cancellation);
            NotifyStateChanged(AttachmentStateChangeKind.Completed);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await CompleteCanceledPreparationAsync(
                panelId,
                pendingImage,
                cancellation,
                savedState);
        }
        catch (Exception ex)
        {
            await CompleteFailedPreparationAsync(
                panelId,
                pendingImage,
                cancellation,
                savedState,
                ex);
        }
        finally
        {
            input.Dispose();
        }
    }

    private async Task CompleteCanceledPreparationAsync(
        string panelId,
        AttachedImageViewModel pendingImage,
        CancellationTokenSource cancellation,
        PanelAttachmentState? savedState)
    {
        try
        {
            await DeleteSavedStateAsync(panelId, savedState);
        }
        catch (Exception ex)
        {
            FailPreparation(pendingImage, cancellation, ex);
            return;
        }

        CancelPreparation(pendingImage, cancellation);
    }

    private async Task CompleteFailedPreparationAsync(
        string panelId,
        AttachedImageViewModel pendingImage,
        CancellationTokenSource cancellation,
        PanelAttachmentState? savedState,
        Exception exception)
    {
        Exception reportedException = exception;

        try
        {
            await DeleteSavedStateAsync(panelId, savedState);
        }
        catch (Exception cleanupException)
        {
            reportedException = new AggregateException(exception, cleanupException);
        }

        FailPreparation(pendingImage, cancellation, reportedException);
    }

    private async Task DeleteSavedStateAsync(
        string panelId,
        PanelAttachmentState? savedState)
    {
        if (savedState is null)
        {
            return;
        }

        await _attachmentStore.DeleteAsync(
            panelId,
            savedState,
            CancellationToken.None);
    }

    private void CancelPreparation(
        AttachedImageViewModel pendingImage,
        CancellationTokenSource cancellation)
    {
        bool imageWasRemoved = _attachedImages.Remove(pendingImage);
        bool preparationWasActive = CompletePreparation(pendingImage, cancellation);

        if (imageWasRemoved || preparationWasActive)
        {
            NotifyStateChanged(AttachmentStateChangeKind.Canceled);
        }
    }

    private void FailPreparation(
        AttachedImageViewModel pendingImage,
        CancellationTokenSource cancellation,
        Exception? exception)
    {
        bool imageWasRemoved = _attachedImages.Remove(pendingImage);
        bool preparationWasActive = CompletePreparation(pendingImage, cancellation);

        if (imageWasRemoved || preparationWasActive || exception is not null)
        {
            NotifyStateChanged(AttachmentStateChangeKind.Failed, exception);
        }
    }

    private bool CompletePreparation(
        AttachedImageViewModel pendingImage,
        CancellationTokenSource cancellation)
    {
        bool preparationWasActive = _preparationCancellations.Remove(pendingImage.Id);
        cancellation.Dispose();

        return preparationWasActive;
    }

    private void NotifyStateChanged(
        AttachmentStateChangeKind kind,
        Exception? exception = null)
    {
        OnPropertyChanged(nameof(HasPendingAttachments));
        AttachmentStateChanged?.Invoke(
            this,
            new AttachmentStateChangedEventArgs(kind, exception));
    }
}
