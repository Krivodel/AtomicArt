using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Services.Gallery;

public interface IImageViewerService
{
    void ConfigureAttachments(
        IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> command,
        IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? inputCommand = null);

    Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct);
}
