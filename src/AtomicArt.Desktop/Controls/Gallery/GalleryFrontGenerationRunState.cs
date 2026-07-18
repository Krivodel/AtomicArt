using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryFrontGenerationRunState
{
    public List<GalleryOperation> UnmaterializedOperations { get; } = [];
    public List<GalleryOperation> AllOperations { get; } = [];
    public List<object> ActiveFrontItems { get; } = [];
    public HashSet<Guid> ActiveFrontIds { get; } = [];
    public Dictionary<Guid, Control> SpawnClones { get; } = [];
    public GalleryAnimationTracker RunningControls { get; } = [];
    public GalleryAnimationTracker OverlayControls { get; } = [];

    internal GalleryFrontGenerationRunState(IReadOnlyList<GalleryOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        UnmaterializedOperations.AddRange(operations);
    }
}
