using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryImageViewerRequest(
    GalleryImageViewerItemsSource ItemsSource,
    Guid SelectedItemId,
    IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? AttachImagesCommand,
    IAsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?>? AttachImageInputsCommand = null);
