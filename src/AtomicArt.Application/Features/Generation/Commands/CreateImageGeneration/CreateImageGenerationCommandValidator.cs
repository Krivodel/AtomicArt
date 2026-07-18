using FluentValidation;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Application.Features.Generation.Validators;

namespace AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;

public sealed class CreateImageGenerationCommandValidator : AbstractValidator<CreateImageGenerationCommand>
{
    private readonly IImageModelRegistry _modelRegistry;

    public CreateImageGenerationCommandValidator(IImageModelRegistry modelRegistry)
    {
        ArgumentNullException.ThrowIfNull(modelRegistry);

        _modelRegistry = modelRegistry;

        RuleFor(command => command.Request)
            .NotNull()
            .WithMessage("Запрос генерации обязателен.")
            .SetValidator(new ImageGenerationRequestDtoValidator());

        RuleFor(command => command.ProviderCredential)
            .Must((command, providerCredential) => HasRequiredProviderCredential(command, providerCredential))
            .WithMessage("Временный ключ провайдера обязателен для Google-модели.");
    }

    private bool HasRequiredProviderCredential(
        CreateImageGenerationCommand command,
        string? providerCredential)
    {
        if (command.Request is null)
        {
            return true;
        }

        IImageModelDefinition? modelDefinition = _modelRegistry.GetById(command.Request.ModelId);

        if (modelDefinition is null
            || !GenerationProviderCredentialRequirements
                .Resolve(modelDefinition.Metadata.Provider)
                .RequiredForApplicationValidation)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(providerCredential);
    }
}
