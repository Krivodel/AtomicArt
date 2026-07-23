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
            .WithMessage("Метаданные генерации обязательны.");
        RuleFor(command => command.Metadata.LogicalGenerationId)
            .NotEmpty()
            .WithMessage("Идентификатор логической генерации обязателен.");
        RuleFor(command => command.Metadata.AttemptNumber)
            .InclusiveBetween(
                GenerationAttemptLimits.MinimumAttemptNumber,
                GenerationAttemptLimits.MaximumAttemptNumber)
            .WithMessage(
                $"Номер попытки должен находиться в диапазоне от {GenerationAttemptLimits.MinimumAttemptNumber} до {GenerationAttemptLimits.MaximumAttemptNumber}.");
        RuleFor(command => command.Metadata.ModelId)
            .NotEmpty()
            .WithMessage("Модель генерации обязательна.");
        RuleFor(command => command.Metadata.Prompt)
            .NotEmpty()
            .WithMessage("Запрос генерации обязателен.");
        RuleFor(command => command.Metadata.Parameters)
            .NotNull()
            .WithMessage("Параметры генерации обязательны.");
        RuleFor(command => command.Metadata.Attachments)
            .NotNull()
            .WithMessage("Метаданные вложений обязательны.");
        RuleFor(command => command.Attachments)
            .NotNull()
            .WithMessage("Вложения обязательны.");
    }
}
