namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed record GalleryImageDuplicate(Guid ItemId, string ModelId, string OriginalPath, string SharedPath);
