using AtomicArt.Application.Common.Models;
using AtomicArt.Contracts.Generation;
using MediatR;

namespace AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;

public sealed record CreateImageGenerationCommand(
    ImageGenerationRequestDto Request,
    string? ProviderCredential = null)
    : IRequest<Result<GenerationBatchDto>>;
