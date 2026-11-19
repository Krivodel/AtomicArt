using System.Globalization;
using System.Text.Json;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationParameterText
{
    public const string AutomaticValue = "Auto";

    private static readonly IReadOnlyDictionary<string, (string? Label, string? OptionPrefix)> Defaults =
        new Dictionary<string, (string? Label, string? OptionPrefix)>(StringComparer.Ordinal)
        {
            [GenerationParameterNames.AspectRatio] =
                (GalleryLocalizationKeys.Metadata.RequestedAspectRatio, null),
            [GenerationParameterNames.Resolution] =
                (GalleryLocalizationKeys.Metadata.RequestedResolution, null),
            [GenerationParameterNames.Temperature] =
                (GenerationUiLocalizationKeys.Temperature.Label, null),
            [GenerationParameterNames.Quality] =
                (GenerationUiLocalizationKeys.Quality.Label, GenerationUiLocalizationKeys.Quality.OptionPrefix),
            [GenerationParameterNames.Complexity] =
                (GenerationUiLocalizationKeys.Complexity.Label, GenerationUiLocalizationKeys.Complexity.OptionPrefix),
            [GenerationParameterNames.ThinkingLevel] =
                (GenerationUiLocalizationKeys.Thinking.Label, null)
        };

    public static string FormatTemperature(double temperature, ILocalizationTextProvider textProvider)
    {
        return textProvider.Format(GenerationUiLocalizationKeys.Temperature.ValueFormat, temperature);
    }

    public static string? FormatValue(string name, JsonElement value, ILocalizationTextProvider textProvider)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when name == GenerationParameterNames.Temperature
                && value.TryGetDouble(out double temperature)
                && double.IsFinite(temperature) => FormatTemperature(temperature, textProvider),
            JsonValueKind.Number when value.TryGetDecimal(out decimal number) =>
                number.ToString(CultureInfo.CurrentCulture),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null
        };
    }

    public static string NormalizeOptionValue(string? optionValue)
    {
        if (string.IsNullOrWhiteSpace(optionValue))
        {
            return AutomaticValue;
        }

        string value = optionValue.Trim().ToLowerInvariant();
        return $"{char.ToUpperInvariant(value[0])}{value[1..]}";
    }

    public static string? GetOptionLocalizationKey(
        GenerationModelParameterMetadataDto parameter,
        string value)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        if (parameter.Presentation is { } presentation)
        {
            return FindModelValueLocalizationKey(presentation, value);
        }

        Defaults.TryGetValue(parameter.Name, out (string? Label, string? OptionPrefix) defaults);
        return ResolveDefaultValueLocalizationKey(parameter.Name, value, defaults.OptionPrefix, null);
    }

    public static GenerationSavedParameter Describe(
        string name,
        JsonElement value,
        ImageModelOption? model = null,
        string? legacyValueLocalizationKey = null)
    {
        GenerationParameterPresentationDto? presentation = model?.Parameters?
            .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
            ?.Presentation;
        Defaults.TryGetValue(name, out (string? Label, string? OptionPrefix) defaults);
        string? selectedValue = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        string? valueLocalizationKey = null;

        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            valueLocalizationKey = presentation is not null
                ? FindModelValueLocalizationKey(presentation, selectedValue)
                : legacyValueLocalizationKey ?? ResolveDefaultValueLocalizationKey(
                    name,
                    selectedValue,
                    defaults.OptionPrefix,
                    model);
        }

        return new GenerationSavedParameter(
            value,
            presentation?.LabelLocalizationKey ?? defaults.Label,
            valueLocalizationKey);
    }

    private static string? FindModelValueLocalizationKey(
        GenerationParameterPresentationDto? presentation,
        string value)
    {
        return presentation?.ValueLocalizationKeys?
            .FirstOrDefault(pair => string.Equals(pair.Key, value, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private static string? ResolveDefaultValueLocalizationKey(
        string name,
        string value,
        string? optionPrefix,
        ImageModelOption? model)
    {
        if (string.Equals(name, GenerationParameterNames.ThinkingLevel, StringComparison.Ordinal))
        {
            return model?.Thinking?.Levels
                .FirstOrDefault(level => string.Equals(level.Value, value, StringComparison.Ordinal))
                ?.LocalizationKey;
        }

        if (string.Equals(name, GenerationParameterNames.AspectRatio, StringComparison.Ordinal)
            && GenerationAspectRatios.IsAuto(value))
        {
            return GenerationLocalizationKeys.OptionsAuto;
        }

        if (optionPrefix is null)
        {
            return null;
        }

        string normalizedValue = NormalizeOptionValue(value);
        return string.Equals(normalizedValue, AutomaticValue, StringComparison.Ordinal)
            ? GenerationLocalizationKeys.OptionsAuto
            : optionPrefix + normalizedValue;
    }
}
