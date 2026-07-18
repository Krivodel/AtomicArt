using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed class MetadataImageModelDefinition : IImageModelDefinition
{
    private readonly GenerationModelRules _rules;
    private readonly GenerationModelConstraints _constraints;
    private readonly IReadOnlyList<AttachedImageSignatureRule> _signatureRules;

    public string Id => _constraints.ModelId;
    public string DisplayName { get; }
    public string Provider { get; }
    public string ProviderModelId { get; }
    public string PanelId { get; }
    public int ContextWindowTokens { get; }
    public int MaxOutputTokens { get; }
    public int MaxAttachedImages => _constraints.MaxAttachedImages;
    public int? MaxPromptLength => _constraints.MaxPromptLength;
    public long MaxAttachedImageBytes => _constraints.MaxAttachedImageBytes;
    public long MaxTotalAttachedImageBytes => _constraints.MaxTotalAttachedImageBytes;
    public GenerationModelTemperatureMetadataDto Temperature { get; }
    public GenerationModelThinkingMetadataDto? Thinking { get; }
    public GenerationModelPricingMetadataDto Pricing { get; }
    public GenerationModelConstraints Constraints => _constraints;

    public MetadataImageModelDefinition(
        GenerationModelMetadataDto metadata,
        GenerationModelRules rules,
        IEnumerable<IAttachedImageFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(metadata.Attachments);

        _rules = rules;
        _constraints = GenerationModelMetadataDomainMapper.ToConstraints(metadata);
        _signatureRules = AttachedImageValidationPolicy.CreateSignatureRules(formats);
        DisplayName = metadata.DisplayName;
        Provider = metadata.Provider;
        ProviderModelId = metadata.ProviderModelId;
        PanelId = metadata.PanelId;
        ContextWindowTokens = metadata.ContextWindowTokens;
        MaxOutputTokens = metadata.MaxOutputTokens;
        Temperature = metadata.Temperature;
        Thinking = metadata.Thinking;
        Pricing = metadata.Pricing;
    }

    public IReadOnlyList<string> GetAspectRatios()
    {
        return _constraints.AspectRatios;
    }

    public IReadOnlyList<string> GetResolutions()
    {
        return _constraints.Resolutions;
    }

    public IReadOnlyList<int> GetGenerationCounts()
    {
        return _constraints.GenerationCounts;
    }

    public IReadOnlyList<string> GetSupportedContentTypes()
    {
        return _constraints.SupportedContentTypes;
    }

    public Result<ImageGenerationRequestDto> Validate(ImageGenerationRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.ModelId, Id, StringComparison.Ordinal))
        {
            return Result<ImageGenerationRequestDto>.NotFound(
                GenerationErrorCodes.ModelNotFound,
                "Модель генерации не найдена.");
        }

        Result<ImageGenerationRequestDto> attachmentValidationResult = ValidateAttachedImages(request);

        if (!attachmentValidationResult.IsSuccess)
        {
            return attachmentValidationResult;
        }

        if (attachmentValidationResult.Value is null)
        {
            return Result<ImageGenerationRequestDto>.ValidationError(
                GenerationErrorCodes.ModelRequestValidation,
                "Вложения не прошли проверку.");
        }

        ImageGenerationRequestDto normalizedRequest = ApplyModelDefaults(attachmentValidationResult.Value);
        GenerationValidationResult rulesResult = ValidateRules(normalizedRequest);

        if (!rulesResult.IsValid)
        {
            return Result<ImageGenerationRequestDto>.ValidationError(
                rulesResult.ErrorCode ?? GenerationErrorCodes.ModelRequestValidation,
                rulesResult.ErrorMessage ?? "Запрос генерации не прошёл проверку.");
        }

        return Result<ImageGenerationRequestDto>.Success(normalizedRequest);
    }

    private Result<ImageGenerationRequestDto> ValidateAttachedImages(ImageGenerationRequestDto request)
    {
        Result<IReadOnlyList<AttachedImageDto>> attachmentValidationResult = AttachedImageValidationService
            .Validate(request.AttachedImages, CreateAttachedImageValidationOptions());

        if (!attachmentValidationResult.IsSuccess)
        {
            return Result<ImageGenerationRequestDto>.ValidationError(
                attachmentValidationResult.ErrorCode ?? GenerationErrorCodes.ModelRequestValidation,
                attachmentValidationResult.ErrorMessage ?? "Вложения не прошли проверку.");
        }

        if (attachmentValidationResult.Value is null)
        {
            return Result<ImageGenerationRequestDto>.ValidationError(
                GenerationErrorCodes.ModelRequestValidation,
                "Вложения не прошли проверку.");
        }

        return Result<ImageGenerationRequestDto>.Success(request with
        {
            AttachedImages = attachmentValidationResult.Value
        });
    }

    private GenerationValidationResult ValidateRules(ImageGenerationRequestDto request)
    {
        IReadOnlyList<GenerationAttachedImage> attachedImages = request.AttachedImages
            .Select(CreateGenerationAttachedImage)
            .ToList();

        return _rules.Validate(
            _constraints,
            request.Prompt,
            request.AspectRatio,
            request.Resolution,
            request.Temperature,
            request.GenerationCount,
            attachedImages,
            request.ThinkingLevel);
    }

    private ImageGenerationRequestDto ApplyModelDefaults(ImageGenerationRequestDto request)
    {
        if (_constraints.Thinking is null || request.ThinkingLevel is not null)
        {
            return request;
        }

        return request with
        {
            ThinkingLevel = _constraints.Thinking.Default
        };
    }

    private AttachedImageValidationOptions CreateAttachedImageValidationOptions()
    {
        return new AttachedImageValidationOptions(
            _signatureRules);
    }

    private static GenerationAttachedImage CreateGenerationAttachedImage(AttachedImageDto? attachedImage)
    {
        string? contentType = attachedImage?.ContentType;
        long sizeInBytes = attachedImage?.Content?.LongLength ?? 0L;

        return new GenerationAttachedImage(contentType, sizeInBytes);
    }
}
