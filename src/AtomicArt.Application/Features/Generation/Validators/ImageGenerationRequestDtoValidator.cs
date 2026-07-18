using FluentValidation;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Validators;

public sealed class ImageGenerationRequestDtoValidator : AbstractValidator<ImageGenerationRequestDto>
{
    public ImageGenerationRequestDtoValidator()
    {
        RuleFor(request => request.ModelId)
            .Must(modelId => !string.IsNullOrWhiteSpace(modelId))
            .WithMessage("Идентификатор модели обязателен.");

        RuleFor(request => request.Prompt)
            .Must(prompt => !string.IsNullOrWhiteSpace(prompt))
            .WithMessage("Промпт обязателен.");

        RuleFor(request => request.AspectRatio)
            .Must(aspectRatio => !string.IsNullOrWhiteSpace(aspectRatio))
            .WithMessage("Соотношение сторон обязательно.");

        RuleFor(request => request.Resolution)
            .Must(resolution => !string.IsNullOrWhiteSpace(resolution))
            .WithMessage("Разрешение обязательно.");

        RuleFor(request => request.Temperature)
            .Must(double.IsFinite)
            .WithMessage("Температура должна быть конечным числом.");

        RuleFor(request => request.GenerationCount)
            .GreaterThan(0)
            .WithMessage("Количество изображений должно быть положительным.");
    }
}
