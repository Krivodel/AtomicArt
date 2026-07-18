using System.Text.Json;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal static class GoogleInteractionsJsonElementReader
{
    public static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement propertyElement)
    {
        propertyElement = default;

        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out propertyElement);
    }
}
