using System.Text.Json;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

internal sealed record LocalCodexTurnResult(
    string ThreadId, string? ModelId, JsonElement? Image, string Text)
{
    internal GenerationChatImageCall GetImageCall(string hash)
    {
        JsonElement image = Image ?? throw new InvalidOperationException("A text reply has no image generation record.");

        return new GenerationChatImageCall(hash, LocalCodexProtocol.GetOptionalString(image, "revisedPrompt"),
            image.TryGetProperty("transparentBackground", out JsonElement transparent)
                && transparent.ValueKind is JsonValueKind.True or JsonValueKind.False ? transparent.GetBoolean() : null,
            LocalCodexProtocol.GetOptionalString(image, "generationId"));
    }
}
