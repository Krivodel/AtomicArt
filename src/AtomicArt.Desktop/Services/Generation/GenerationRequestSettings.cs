using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtomicArt.Desktop.Services.Generation;

[JsonConverter(typeof(GenerationRequestSettingsJsonConverter))]
public sealed record GenerationRequestSettings(
    IReadOnlyDictionary<string, GenerationSavedParameter> Parameters,
    string? CodexTextModelId = null)
{
    public string? GetString(string parameterName)
    {
        return Parameters.TryGetValue(parameterName, out GenerationSavedParameter? parameter)
            && parameter?.Value.ValueKind == JsonValueKind.String
            ? parameter.Value.GetString()
            : null;
    }

    public double? GetNumber(string parameterName)
    {
        return Parameters.TryGetValue(parameterName, out GenerationSavedParameter? parameter)
            && parameter?.Value.ValueKind == JsonValueKind.Number
            && parameter.Value.TryGetDouble(out double value)
            && double.IsFinite(value)
            ? value
            : null;
    }
}
