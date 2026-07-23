using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using MediatR;

namespace AtomicArt.Application.Features.Generation.Commands.CreateStreamingGeneration;

public sealed record CreateStreamingGenerationCommand(
    GenerationRequestMetadataDto Metadata,
    IReadOnlyList<IGenerationAttachmentSource> Attachments,
    string? ProviderCredential)
    : IRequest<GenerationAttemptPreparation>;
