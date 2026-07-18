using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed record GalleryImageViewerRequest(
    GalleryImageViewerItemsSource ItemsSource,
    Guid SelectedItemId,
    IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? AttachImagesCommand);
