using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Tests;

internal sealed class NullImageViewerService : IImageViewerService
{
    public void ConfigureAttachments(
        IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> command,
        IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? inputCommand = null)
    {
        ArgumentNullException.ThrowIfNull(command);
    }

    public Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
