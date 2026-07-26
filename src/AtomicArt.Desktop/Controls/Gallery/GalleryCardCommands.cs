using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed record GalleryCardCommands(
    IRelayCommand? OpenViewer,
    IRelayCommand? RevealInFolder,
    IRelayCommand? OpenMetadata,
    IRelayCommand? DeleteOrCancel);
