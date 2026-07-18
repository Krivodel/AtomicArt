using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed record FrontGenerationCycleResult(
    bool ShouldRetarget,
    List<GalleryOperation> NextOperations);
