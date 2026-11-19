using System.Text.Json;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal static class GenerationRequestSettingsTestFactory
{
    public static GenerationRequestSettings Create(params (string Name, object Value)[] fields)
    {
        Dictionary<string, GenerationSavedParameter> parameters = fields.ToDictionary(
            field => field.Name,
            field => GenerationParameterText.Describe(field.Name, JsonSerializer.SerializeToElement(field.Value)),
            StringComparer.Ordinal);

        return new GenerationRequestSettings(parameters);
    }
}
