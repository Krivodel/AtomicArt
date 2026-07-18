using System.Diagnostics.CodeAnalysis;
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

    public static bool TryGetProperty(
        JsonElement element,
        string firstName,
        string secondName,
        out JsonElement propertyElement)
    {
        if (TryGetProperty(element, firstName, out propertyElement))
        {
            return true;
        }

        return TryGetProperty(element, secondName, out propertyElement);
    }

    public static bool TryGetInt32Property(
        JsonElement element,
        string propertyName,
        out int value)
    {
        value = 0;

        return TryGetProperty(element, propertyName, out JsonElement propertyElement)
            && propertyElement.ValueKind == JsonValueKind.Number
            && propertyElement.TryGetInt32(out value);
    }

    public static bool TryGetInt32Property(
        JsonElement element,
        string firstName,
        string secondName,
        out int value)
    {
        value = 0;

        return TryGetProperty(element, firstName, secondName, out JsonElement propertyElement)
            && propertyElement.ValueKind == JsonValueKind.Number
            && propertyElement.TryGetInt32(out value);
    }

    public static bool TryGetStringProperty(
        JsonElement element,
        string propertyName,
        [NotNullWhen(true)] out string? value)
    {
        value = null;

        if (!TryGetProperty(element, propertyName, out JsonElement propertyElement)
            || propertyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = propertyElement.GetString();

        return value is not null;
    }

    public static bool TryGetStringProperty(
        JsonElement element,
        string firstName,
        string secondName,
        [NotNullWhen(true)] out string? value)
    {
        value = null;

        if (!TryGetProperty(element, firstName, secondName, out JsonElement propertyElement)
            || propertyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = propertyElement.GetString();

        return value is not null;
    }
}
