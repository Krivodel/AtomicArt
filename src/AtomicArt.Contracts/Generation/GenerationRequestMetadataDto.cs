using System.Text.Json;

namespace AtomicArt.Contracts.Generation;

public sealed record GenerationRequestMetadataDto(
    Guid LogicalGenerationId,
    int AttemptNumber,
    string ModelId,
    string Prompt,
    IReadOnlyDictionary<string, JsonElement> Parameters,
    IReadOnlyList<GenerationAttachmentMetadataDto> Attachments);
