namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed record AttachedImageReorderRequest(
    AttachedImageViewModel AttachedImage,
    int TargetIndex);
