using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Tests;

internal sealed class RecordingImageViewerService : IImageViewerService
{
    public GalleryImageViewerRequest? LastRequest { get; private set; }
    public IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? AttachImagesCommand { get; private set; }
    public IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? AttachImageInputsCommand { get; private set; }
    public int OpenCallCount { get; private set; }

    public void ConfigureAttachments(
        IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> command,
        IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? inputCommand = null)
    {
        AttachImagesCommand = command ?? throw new ArgumentNullException(nameof(command));
        AttachImageInputsCommand = inputCommand;
    }

    public Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct)
    {
        LastRequest = request;
        OpenCallCount++;

        return Task.CompletedTask;
    }
}
