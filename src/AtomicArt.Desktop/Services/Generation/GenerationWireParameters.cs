using System.Text.Json;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

internal static class GenerationWireParameters
{
    public static IReadOnlyDictionary<string, JsonElement> Create(ImageGenerationRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, JsonElement> parameters = new(StringComparer.Ordinal)
        {
            [GenerationParameterNames.AspectRatio] = JsonSerializer.SerializeToElement(request.AspectRatio),
            [GenerationParameterNames.Resolution] = JsonSerializer.SerializeToElement(request.Resolution)
        };

        if (request.IncludeTemperature)
        {
            parameters.Add(
                GenerationParameterNames.Temperature,
                JsonSerializer.SerializeToElement(request.Temperature));
        }

        AddOptionalString(parameters, GenerationParameterNames.Quality, request.Quality);
        AddOptionalString(parameters, GenerationParameterNames.Complexity, request.Complexity);
        AddOptionalString(parameters, GenerationParameterNames.ThinkingLevel, request.ThinkingLevel);

        return parameters;
    }

    private static void AddOptionalString(
        Dictionary<string, JsonElement> parameters,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add(name, JsonSerializer.SerializeToElement(value));
        }
    }
}
