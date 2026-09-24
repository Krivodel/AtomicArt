using Pica.Viewer.Services;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed record GalleryOpenWithApplicationRequest(
    GenerationItemViewModel Item,
    string FilePath,
    OpenWithApplication Application);
