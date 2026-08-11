using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed record GalleryCardCommands(
    IRelayCommand? OpenViewer,
    IRelayCommand? ShowFailureDetails,
    IRelayCommand? RevealInFolder,
    IRelayCommand? RevealInNewFolderWindow,
    IRelayCommand? OpenMetadata,
    IRelayCommand? ToggleSelection,
    IRelayCommand? SelectRange);
