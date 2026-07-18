using Avalonia;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed record GallerySpawnTarget(
    object Item,
    Guid Id,
    Rect Rect);
