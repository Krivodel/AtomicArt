using System.Text.Json;

using AtomicArt.Application.Features.Generation.Interfaces;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed record StreamingImageGenerationRequest(
    Guid LogicalGenerationId,
    int AttemptNumber,
    string ModelId,
    string Prompt,
    string AspectRatio,
    string Resolution,
    double Temperature,
    string? ThinkingLevel,
    IReadOnlyDictionary<string, JsonElement> Parameters,
    IReadOnlyList<IGenerationAttachmentSource> Attachments);
