namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed record GenerationMetadataRow(
    GenerationMetadataField First,
    GenerationMetadataField? Second,
    GenerationMetadataField? Third);
