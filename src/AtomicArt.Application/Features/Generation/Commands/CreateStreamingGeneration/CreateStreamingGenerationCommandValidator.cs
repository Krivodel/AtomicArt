using FluentValidation;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Commands.CreateStreamingGeneration;

public sealed class CreateStreamingGenerationCommandValidator
    : AbstractValidator<CreateStreamingGenerationCommand>
{
    public CreateStreamingGenerationCommandValidator()
    {
        RuleFor(command => command.Metadata)
            .NotNull()
            .WithMessage("Generation metadata is required.");
        RuleFor(command => command.Metadata.LogicalGenerationId)
            .NotEmpty()
            .WithMessage("Logical generation ID is required.");
        RuleFor(command => command.Metadata.AttemptNumber)
            .InclusiveBetween(
                GenerationAttemptLimits.MinimumAttemptNumber,
                GenerationAttemptLimits.MaximumAttemptNumber)
            .WithMessage(
                $"Attempt number must be between {GenerationAttemptLimits.MinimumAttemptNumber} and {GenerationAttemptLimits.MaximumAttemptNumber}.");
        RuleFor(command => command.Metadata.ModelId)
            .NotEmpty()
            .WithMessage("Generation model is required.");
        RuleFor(command => command.Metadata.Prompt)
            .NotEmpty()
            .WithMessage("Generation prompt is required.");
        RuleFor(command => command.Metadata.Parameters)
            .NotNull()
            .WithMessage("Generation parameters are required.");
        RuleFor(command => command.Metadata.Attachments)
            .NotNull()
            .WithMessage("Attachment metadata is required.");
        RuleFor(command => command.Attachments)
            .NotNull()
            .WithMessage("Attachments are required.");
    }
}
