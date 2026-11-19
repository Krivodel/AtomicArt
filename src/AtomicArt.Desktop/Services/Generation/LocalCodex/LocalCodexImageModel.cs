using System.Collections.ObjectModel;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

internal static class LocalCodexImageModel
{
    public const string Id = "local-codex";
    public const string Automatic = "auto";

    public static ImageModelOption Option { get; } = new(
        Id,
        "Codex",
        Id,
        Id,
        GenerationPanelIds.NanoBanana,
        ContextWindowTokens: 0,
        MaxOutputTokens: 0,
        new[]
        {
            new GenerationModelOptionMetadataDto(GenerationAspectRatios.Auto, GenerationLocalizationKeys.OptionsAuto),
            new GenerationModelOptionMetadataDto("1:1", null),
            new GenerationModelOptionMetadataDto("9:16", null),
            new GenerationModelOptionMetadataDto("16:9", null),
            new GenerationModelOptionMetadataDto("21:9", null)
        },
        new[] { Automatic },
        new[] { 1, 2, 3, 4 },
        new GenerationModelTemperatureMetadataDto(0d, 1d, 0d, 1d),
        MaxAttachedImages: 5,
        MaxAttachedImageBytes: 20 * 1024 * 1024,
        MaxTotalAttachedImageBytes: 50L * 1024 * 1024,
        new[] { GenerationImageContentTypes.Png, GenerationImageContentTypes.Jpeg, GenerationImageContentTypes.Webp },
        new GenerationModelPricingMetadataDto(
            string.Empty, 0m, 0m, 0m, 0m, 1m, 0, ReadOnlyDictionary<string, int>.Empty),
        SupportsTemperature: false);

    public static bool Matches(string? modelId)
    {
        return string.Equals(modelId, Id, StringComparison.Ordinal);
    }

    public static string FormatDisplayName(string? textModelName)
    {
        return string.IsNullOrWhiteSpace(textModelName)
            ? Option.DisplayName
            : $"{Option.DisplayName}, {textModelName}";
    }
}
