using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed record GalleryCardCommands(
    IRelayCommand? CopyImage,
    IRelayCommand? OpenViewer,
    IRelayCommand? ShowFailureDetails,
    IRelayCommand? RevealInFolder,
    IRelayCommand? RevealInNewFolderWindow,
    IRelayCommand? OpenMetadata,
    IRelayCommand? DeleteOrCancel,
    IRelayCommand? ToggleFavorite,
    IRelayCommand? ToggleSelection,
    IRelayCommand? SelectRange,
    IRelayCommand? OpenDlss5 = null);
