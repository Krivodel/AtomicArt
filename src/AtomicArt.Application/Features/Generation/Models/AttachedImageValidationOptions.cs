using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;

namespace AtomicArt.Application.Features.Generation.Models;

internal sealed record AttachedImageValidationOptions(
    IAttachedImageFormatRegistry FormatRegistry);
