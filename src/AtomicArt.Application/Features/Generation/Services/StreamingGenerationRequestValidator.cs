using System.Text.Json;

using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;
using DomainGenerationErrorCodes = AtomicArt.Domain.Generation.GenerationErrorCodes;

namespace AtomicArt.Application.Features.Generation.Services;

public sealed class StreamingGenerationRequestValidator
{
    private const int SignatureProbeLength = 64;

    private readonly IAttachedImageFormatRegistry _formatRegistry;
    private readonly GenerationModelRules _rules;

    public StreamingGenerationRequestValidator(
        IAttachedImageFormatRegistry formatRegistry,
        GenerationModelRules rules)
    {
        _formatRegistry = formatRegistry
            ?? throw new ArgumentNullException(nameof(formatRegistry));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public async Task<Result<StreamingImageGenerationRequest>> ValidateAsync(
        GenerationRequestMetadataDto metadata,
        IReadOnlyList<IGenerationAttachmentSource> attachments,
        IImageModelDefinition modelDefinition,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(modelDefinition);

        if (modelDefinition.Metadata.TransportLimits is
                GenerationModelTransportLimitsDto transportLimits
            && metadata.Attachments is not null
            && metadata.Attachments.Sum(attachment => attachment.ByteLength)
                > transportLimits.MaxRequestBytes)
        {
            return Result<StreamingImageGenerationRequest>.ValidationError(
                DomainGenerationErrorCodes.ModelRequestValidation,
                "Запрос превышает транспортный предел выбранной модели.");
        }

        Result<IReadOnlyDictionary<string, JsonElement>> parametersResult =
            ValidateParameters(metadata.Parameters, modelDefinition.Metadata.Parameters);

        if (parametersResult is not { IsSuccess: true, Value: { } parameters })
        {
            return Result<StreamingImageGenerationRequest>.ValidationError(
                parametersResult.ErrorCode
                    ?? GenerationProtocolErrorCodes.InvalidParameters,
                parametersResult.ErrorMessage
                    ?? "Параметры генерации не прошли проверку.");
        }

        Result<IReadOnlyList<GenerationAttachedImage>> attachmentMetadataResult =
            await ValidateAttachmentsAsync(
                    metadata.Attachments,
                    attachments,
                    ct)
                .ConfigureAwait(false);

        if (attachmentMetadataResult
            is not { IsSuccess: true, Value: { } attachmentMetadata })
        {
            return Result<StreamingImageGenerationRequest>.ValidationError(
                attachmentMetadataResult.ErrorCode
                    ?? DomainGenerationErrorCodes.ModelRequestValidation,
                attachmentMetadataResult.ErrorMessage
                    ?? "Вложения не прошли проверку.");
        }

        if (!TryReadString(
                parameters,
                GenerationParameterNames.AspectRatio,
                out string? aspectRatio)
            || !TryReadString(
                parameters,
                GenerationParameterNames.Resolution,
                out string? resolution)
            || !TryReadDouble(
                parameters,
                GenerationParameterNames.Temperature,
                out double temperature))
        {
            return Result<StreamingImageGenerationRequest>.ValidationError(
                GenerationProtocolErrorCodes.InvalidParameters,
                "Обязательные параметры генерации не переданы.");
        }

        TryReadString(
            parameters,
            GenerationParameterNames.ThinkingLevel,
            out string? thinkingLevel);
        string normalizedAspectRatio = aspectRatio ?? string.Empty;
        string normalizedResolution = resolution ?? string.Empty;
        GenerationValidationRequest validationRequest = new(
            modelDefinition.Constraints,
            metadata.Prompt,
            normalizedAspectRatio,
            normalizedResolution,
            temperature,
            1,
            attachmentMetadata,
            thinkingLevel);
        GenerationValidationResult validationResult = _rules.Validate(validationRequest);

        if (!validationResult.IsValid)
        {
            return Result<StreamingImageGenerationRequest>.ValidationError(
                validationResult.ErrorCode
                    ?? DomainGenerationErrorCodes.ModelRequestValidation,
                validationResult.ErrorMessage
                    ?? "Запрос генерации не прошёл проверку.");
        }

        StreamingImageGenerationRequest request = new(
            metadata.LogicalGenerationId,
            metadata.AttemptNumber,
            metadata.ModelId,
            metadata.Prompt,
            normalizedAspectRatio,
            normalizedResolution,
            temperature,
            thinkingLevel,
            parameters,
            attachments);

        return Result<StreamingImageGenerationRequest>.Success(request);
    }

    private static Result<IReadOnlyDictionary<string, JsonElement>> ValidateParameters(
        IReadOnlyDictionary<string, JsonElement>? parameters,
        IReadOnlyList<GenerationModelParameterMetadataDto>? definitions)
    {
        if (parameters is null || definitions is null || definitions.Count == 0)
        {
            return Result<IReadOnlyDictionary<string, JsonElement>>.ValidationError(
                GenerationProtocolErrorCodes.InvalidParameters,
                "Каталог модели не содержит определения параметров.");
        }

        Dictionary<string, GenerationModelParameterMetadataDto> definitionsByName =
            definitions.ToDictionary(
                definition => definition.Name,
                definition => definition,
                StringComparer.Ordinal);
        Dictionary<string, JsonElement> normalizedParameters =
            new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonElement> parameter in parameters)
        {
            if (!definitionsByName.TryGetValue(
                    parameter.Key,
                    out GenerationModelParameterMetadataDto? definition)
                || !IsValidParameterValue(parameter.Value, definition))
            {
                return Result<IReadOnlyDictionary<string, JsonElement>>.ValidationError(
                    GenerationProtocolErrorCodes.InvalidParameters,
                    "Передан неизвестный или недопустимый параметр генерации.");
            }

            normalizedParameters[parameter.Key] = parameter.Value.Clone();
        }

        foreach (GenerationModelParameterMetadataDto definition in definitions)
        {
            if (normalizedParameters.ContainsKey(definition.Name))
            {
                continue;
            }

            if (definition.DefaultValue is JsonElement defaultValue)
            {
                normalizedParameters[definition.Name] = defaultValue.Clone();

                continue;
            }

            if (definition.Required)
            {
                return Result<IReadOnlyDictionary<string, JsonElement>>.ValidationError(
                    GenerationProtocolErrorCodes.InvalidParameters,
                    "Обязательный параметр генерации не передан.");
            }
        }

        return Result<IReadOnlyDictionary<string, JsonElement>>.Success(
            normalizedParameters);
    }

    private async Task<Result<IReadOnlyList<GenerationAttachedImage>>> ValidateAttachmentsAsync(
        IReadOnlyList<GenerationAttachmentMetadataDto>? metadata,
        IReadOnlyList<IGenerationAttachmentSource> attachments,
        CancellationToken ct)
    {
        if (metadata is null || metadata.Count != attachments.Count)
        {
            return CreateAttachmentFailure("Число вложений не совпадает с метаданными.");
        }

        List<GenerationAttachedImage> validatedAttachments = [];

        for (int index = 0; index < attachments.Count; index++)
        {
            IGenerationAttachmentSource attachment = attachments[index];
            GenerationAttachmentMetadataDto descriptor = metadata[index];

            if (descriptor.Index != index
                || attachment.Metadata != descriptor
                || AttachedImageFileNamePolicy.Normalize(descriptor.FileName) is null
                || descriptor.ByteLength <= 0
                || !_formatRegistry.TryGetByContentType(
                    descriptor.ContentType,
                    out IAttachedImageFormat? format)
                || format is null)
            {
                return CreateAttachmentFailure("Метаданные вложения не прошли проверку.");
            }

            byte[] signatureProbe = new byte[SignatureProbeLength];
            int bytesRead;

            await using (Stream stream = await attachment.OpenReadAsync(ct).ConfigureAwait(false))
            {
                bytesRead = await ReadProbeAsync(stream, signatureProbe, ct).ConfigureAwait(false);
            }

            if (!format.MatchesSignature(signatureProbe.AsSpan(0, bytesRead)))
            {
                return CreateAttachmentFailure(
                    "Сигнатура вложения не соответствует типу содержимого.");
            }

            validatedAttachments.Add(new GenerationAttachedImage(
                format.ContentType,
                descriptor.ByteLength));
        }

        return Result<IReadOnlyList<GenerationAttachedImage>>.Success(
            validatedAttachments);
    }

    private static bool IsValidParameterValue(
        JsonElement value,
        GenerationModelParameterMetadataDto definition)
    {
        if (string.Equals(
                definition.Type,
                GenerationParameterTypes.String,
                StringComparison.Ordinal))
        {
            return value.ValueKind == JsonValueKind.String
                && IsAllowedValue(value, definition.AllowedValues);
        }

        if (string.Equals(
                definition.Type,
                GenerationParameterTypes.Number,
                StringComparison.Ordinal)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out double number))
        {
            return (!definition.Minimum.HasValue || number >= definition.Minimum.Value)
                && (!definition.Maximum.HasValue || number <= definition.Maximum.Value)
                && IsAllowedValue(value, definition.AllowedValues);
        }

        if (string.Equals(
                definition.Type,
                GenerationParameterTypes.Integer,
                StringComparison.Ordinal))
        {
            return value.ValueKind == JsonValueKind.Number
                && value.TryGetInt64(out long _)
                && IsAllowedValue(value, definition.AllowedValues);
        }

        if (string.Equals(
                definition.Type,
                GenerationParameterTypes.Boolean,
                StringComparison.Ordinal))
        {
            return value.ValueKind is JsonValueKind.True or JsonValueKind.False
                && IsAllowedValue(value, definition.AllowedValues);
        }

        return false;
    }

    private static bool IsAllowedValue(
        JsonElement value,
        IReadOnlyList<JsonElement>? allowedValues)
    {
        return allowedValues is null
            || allowedValues.Count == 0
            || allowedValues.Any(allowed => JsonElement.DeepEquals(allowed, value));
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, JsonElement> parameters,
        string name,
        out string? value)
    {
        value = null;

        if (!parameters.TryGetValue(name, out JsonElement element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();

        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadDouble(
        IReadOnlyDictionary<string, JsonElement> parameters,
        string name,
        out double value)
    {
        value = default;

        return parameters.TryGetValue(name, out JsonElement element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDouble(out value);
    }

    private static async Task<int> ReadProbeAsync(
        Stream stream,
        byte[] probe,
        CancellationToken ct)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < probe.Length)
        {
            int bytesRead = await stream
                .ReadAsync(probe.AsMemory(totalBytesRead), ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    private static Result<IReadOnlyList<GenerationAttachedImage>> CreateAttachmentFailure(
        string message)
    {
        return Result<IReadOnlyList<GenerationAttachedImage>>.ValidationError(
            DomainGenerationErrorCodes.ModelRequestValidation,
            message);
    }
}
