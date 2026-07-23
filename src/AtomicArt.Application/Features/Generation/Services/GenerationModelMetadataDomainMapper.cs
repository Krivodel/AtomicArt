using System.Text.Json;

using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Services;

internal static class GenerationModelMetadataDomainMapper
{
    public static GenerationModelConstraints ToConstraints(GenerationModelMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(metadata.Attachments);
        ArgumentNullException.ThrowIfNull(metadata.Temperature);

        return new GenerationModelConstraints(
            metadata.Id,
            metadata.MaxPromptLength,
            metadata.AspectRatios,
            metadata.Resolutions,
            metadata.GenerationCounts,
            new GenerationModelTemperatureConstraints(
                metadata.Temperature.Minimum,
                metadata.Temperature.Maximum,
                metadata.Temperature.Default,
                metadata.Temperature.Step),
            metadata.Attachments.MaxCount,
            metadata.Attachments.MaxSingleFileBytes,
            metadata.Attachments.MaxTotalBytes,
            metadata.Attachments.SupportedContentTypes,
            CreateThinkingConstraints(metadata.Thinking));
    }

    public static GenerationModelPricing ToPricing(
        string modelId,
        GenerationModelPricingMetadataDto pricing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(pricing);

        return new GenerationModelPricing(
            modelId,
            pricing.CurrencyCode,
            pricing.InputTokenUsdPerMillion,
            pricing.TextOutputTokenUsdPerMillion,
            pricing.ImageOutputTokenUsdPerMillion,
            pricing.InputImageTokens,
            pricing.OutputImageTokensByResolution);
    }

    public static GenerationModelMetadataDto ToMetadataSnapshot(
        GenerationModelMetadataDto metadata,
        GenerationModelConstraints constraints,
        GenerationModelPricing pricing,
        GenerationModelThinkingMetadataDto? thinking)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(pricing);

        return metadata with
        {
            MaxPromptLength = constraints.MaxPromptLength,
            AspectRatios = constraints.AspectRatios,
            Resolutions = constraints.Resolutions,
            GenerationCounts = constraints.GenerationCounts,
            Temperature = new GenerationModelTemperatureMetadataDto(
                constraints.Temperature.Minimum,
                constraints.Temperature.Maximum,
                constraints.Temperature.Default,
                constraints.Temperature.Step),
            Attachments = new GenerationModelAttachmentMetadataDto(
                constraints.MaxAttachedImages,
                constraints.MaxAttachedImageBytes,
                constraints.MaxTotalAttachedImageBytes,
                constraints.SupportedContentTypes),
            Pricing = new GenerationModelPricingMetadataDto(
                pricing.CurrencyCode,
                pricing.InputTokenUsdPerMillion,
                pricing.TextOutputTokenUsdPerMillion,
                pricing.ImageOutputTokenUsdPerMillion,
                pricing.InputImageTokens,
                pricing.OutputImageTokensByResolution),
            Thinking = thinking,
            Parameters = metadata.Parameters is { Count: > 0 }
                ? CreateParameterSnapshot(metadata.Parameters)
                : CreateParameterMetadata(constraints),
            TransportLimits = CreateTransportLimitsSnapshot(
                metadata.TransportLimits,
                constraints)
        };
    }

    private static GenerationModelTransportLimitsDto CreateTransportLimitsSnapshot(
        GenerationModelTransportLimitsDto? limits,
        GenerationModelConstraints constraints)
    {
        GenerationModelTransportLimitsDto resolvedLimits = limits
            ?? new GenerationModelTransportLimitsDto(
                constraints.MaxTotalAttachedImageBytes + 1024L * 1024L,
                512L * 1024L * 1024L,
                1,
                4 * 1024 * 1024,
                64,
                512,
                new List<string>
                {
                    GenerationImageContentTypes.Jpeg,
                    GenerationImageContentTypes.Png,
                    GenerationImageContentTypes.Webp
                });

        if (resolvedLimits.MaxRequestBytes <= 0
            || resolvedLimits.MaxResponseBytes <= 0
            || resolvedLimits.MaxResultCount <= 0
            || resolvedLimits.MaxStatisticsBytes <= 0
            || resolvedLimits.MaxStructureDepth <= 0
            || resolvedLimits.MaxDiagnosticTextCharacters <= 0
            || resolvedLimits.AllowedResponseContentTypes is null
            || resolvedLimits.AllowedResponseContentTypes.Count == 0
            || resolvedLimits.AllowedResponseContentTypes.Any(
                string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "Generation model transport limits are invalid.");
        }

        return resolvedLimits with
        {
            AllowedResponseContentTypes =
                resolvedLimits.AllowedResponseContentTypes
                    .Select(contentType => contentType.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
        };
    }

    private static IReadOnlyList<GenerationModelParameterMetadataDto> CreateParameterSnapshot(
        IReadOnlyList<GenerationModelParameterMetadataDto> parameters)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        List<GenerationModelParameterMetadataDto> snapshot = [];

        foreach (GenerationModelParameterMetadataDto parameter in parameters)
        {
            if (parameter is null
                || string.IsNullOrWhiteSpace(parameter.Name)
                || string.IsNullOrWhiteSpace(parameter.Type)
                || !names.Add(parameter.Name.Trim()))
            {
                throw new InvalidOperationException(
                    "Generation model parameter metadata contains an invalid or duplicate definition.");
            }

            snapshot.Add(parameter with
            {
                Name = parameter.Name.Trim(),
                Type = parameter.Type.Trim(),
                DefaultValue = parameter.DefaultValue?.Clone(),
                AllowedValues = parameter.AllowedValues?
                    .Select(value => value.Clone())
                    .ToList()
            });
        }

        return snapshot.AsReadOnly();
    }

    private static IReadOnlyList<GenerationModelParameterMetadataDto> CreateParameterMetadata(
        GenerationModelConstraints constraints)
    {
        List<GenerationModelParameterMetadataDto> parameters =
        [
            new GenerationModelParameterMetadataDto(
                GenerationParameterNames.Temperature,
                GenerationParameterTypes.Number,
                false,
                JsonSerializer.SerializeToElement(constraints.Temperature.Default),
                constraints.Temperature.Minimum,
                constraints.Temperature.Maximum,
                constraints.Temperature.Step),
            new GenerationModelParameterMetadataDto(
                GenerationParameterNames.AspectRatio,
                GenerationParameterTypes.String,
                true,
                null,
                null,
                null,
                null,
                constraints.AspectRatios
                    .Select(value => JsonSerializer.SerializeToElement(value))
                    .ToList()),
            new GenerationModelParameterMetadataDto(
                GenerationParameterNames.Resolution,
                GenerationParameterTypes.String,
                true,
                null,
                null,
                null,
                null,
                constraints.Resolutions
                    .Select(value => JsonSerializer.SerializeToElement(value))
                    .ToList())
        ];

        if (constraints.Thinking is not null)
        {
            parameters.Add(new GenerationModelParameterMetadataDto(
                GenerationParameterNames.ThinkingLevel,
                GenerationParameterTypes.String,
                false,
                JsonSerializer.SerializeToElement(constraints.Thinking.Default),
                null,
                null,
                null,
                constraints.Thinking.Levels
                    .Select(value => JsonSerializer.SerializeToElement(value))
                    .ToList()));
        }

        return parameters.AsReadOnly();
    }

    private static GenerationModelThinkingConstraints? CreateThinkingConstraints(
        GenerationModelThinkingMetadataDto? thinking)
    {
        if (thinking is null)
        {
            return null;
        }

        IReadOnlyList<string>? levels = thinking.Levels?
            .Select(level => level?.Value ?? string.Empty)
            .ToList();

        return new GenerationModelThinkingConstraints(levels, thinking.Default);
    }
}
