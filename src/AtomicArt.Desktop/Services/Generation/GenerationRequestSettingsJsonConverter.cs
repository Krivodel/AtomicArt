using System.Text.Json;
using System.Text.Json.Serialization;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GenerationRequestSettingsJsonConverter : JsonConverter<GenerationRequestSettings>
{
    private const string ParametersPropertyName = "parameters";
    private const string LegacyThinkingKey = "thinkingLevelLocalizationKey";

    private static readonly string[] LegacyParameterNames =
    [
        GenerationParameterNames.AspectRatio,
        GenerationParameterNames.Resolution,
        GenerationParameterNames.Temperature,
        GenerationParameterNames.Quality,
        GenerationParameterNames.Complexity,
        GenerationParameterNames.ThinkingLevel
    ];

    public override GenerationRequestSettings? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement snapshot = document.RootElement;

        if (snapshot.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (snapshot.TryGetProperty(ParametersPropertyName, out JsonElement parameters))
        {
            return parameters.ValueKind == JsonValueKind.Object
                ? ReadParameters(parameters, options)
                : null;
        }

        return ReadLegacySnapshot(snapshot);
    }

    private static GenerationRequestSettings ReadParameters(
        JsonElement parameters,
        JsonSerializerOptions options)
    {
        Dictionary<string, GenerationSavedParameter> values = new(StringComparer.Ordinal);

        foreach (JsonProperty property in parameters.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            try
            {
                GenerationSavedParameter? parameter = property.Value
                    .Deserialize<GenerationSavedParameter>(options);
                if (parameter is not null)
                {
                    values[property.Name] = parameter;
                }
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return new GenerationRequestSettings(values);
    }

    public override void Write(
        Utf8JsonWriter writer,
        GenerationRequestSettings value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(ParametersPropertyName);
        JsonSerializer.Serialize(writer, value.Parameters, options);
        writer.WriteEndObject();
    }

    private static GenerationRequestSettings ReadLegacySnapshot(JsonElement snapshot)
    {
        Dictionary<string, GenerationSavedParameter> parameters = new(StringComparer.Ordinal);

        foreach (string name in LegacyParameterNames)
        {
            if (!snapshot.TryGetProperty(name, out JsonElement value)
                || value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            string? legacyLocalizationKey = name == GenerationParameterNames.ThinkingLevel
                && snapshot.TryGetProperty(LegacyThinkingKey, out JsonElement thinkingKey)
                && thinkingKey.ValueKind == JsonValueKind.String
                ? thinkingKey.GetString()
                : null;

            parameters.Add(
                name,
                GenerationParameterText.Describe(
                    name,
                    value.Clone(),
                    legacyValueLocalizationKey: legacyLocalizationKey));
        }

        return new GenerationRequestSettings(parameters);
    }
}
