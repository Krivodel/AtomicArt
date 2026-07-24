using System.Text.Json;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal static class GoogleInteractionsContentContract
{
    public const string TypePropertyName = "type";
    public const string TextPropertyName = "text";
    public const string MimeTypePropertyName = "mime_type";
    public const string DataPropertyName = "data";
    public const string SignaturePropertyName = "signature";
    public const string ImageType = "image";
    public const string TextType = "text";

    public static bool IsTextContent(JsonElement element)
    {
        return GoogleInteractionsJsonElementReader.TryGetProperty(
                element,
                TextPropertyName,
                out JsonElement textElement)
            && textElement.ValueKind == JsonValueKind.String
            && GoogleInteractionsJsonElementReader.TryGetProperty(
                element,
                TypePropertyName,
                out JsonElement typeElement)
            && IsTextType(typeElement);
    }

    private static bool IsTextType(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String
            && (element.ValueEquals(TextType)
                || string.Equals(
                    element.GetString(),
                    TextType,
                    StringComparison.OrdinalIgnoreCase));
    }
}
