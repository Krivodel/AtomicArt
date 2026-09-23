using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public interface IImageViewerService
{
    void ConfigureAttachments(IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> command);

    Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct);
}
