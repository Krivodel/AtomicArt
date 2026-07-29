using FluentValidation;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Validators;

public sealed class ImageGenerationRequestDtoValidator : AbstractValidator<ImageGenerationRequestDto>
{
    public ImageGenerationRequestDtoValidator()
    {
        RuleFor(request => request.ModelId)
            .Must(modelId => !string.IsNullOrWhiteSpace(modelId))
            .WithMessage("Model ID is required.");

        RuleFor(request => request.Prompt)
            .Must(prompt => !string.IsNullOrWhiteSpace(prompt))
            .WithMessage("Prompt is required.");

        RuleFor(request => request.AspectRatio)
            .Must(aspectRatio => !string.IsNullOrWhiteSpace(aspectRatio))
            .WithMessage("Aspect ratio is required.");

        RuleFor(request => request.Resolution)
            .Must(resolution => !string.IsNullOrWhiteSpace(resolution))
            .WithMessage("Resolution is required.");

        RuleFor(request => request.Temperature)
            .Must(double.IsFinite)
            .WithMessage("Temperature must be a finite number.");

        RuleFor(request => request.GenerationCount)
            .GreaterThan(0)
            .WithMessage("Generation count must be positive.");
    }
}
