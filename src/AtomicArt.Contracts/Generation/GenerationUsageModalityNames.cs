namespace AtomicArt.Contracts.Generation;

public static class GenerationUsageModalityNames
{
    public const string Image = "image";
    public const string Text = "text";

    public static IReadOnlyList<string> KnownImageGenerationInputModalities { get; } =
    [
        Image,
        Text
    ];

    public static IReadOnlyList<string> KnownImageGenerationOutputModalities { get; } =
    [
        Image,
        Text
    ];
}
