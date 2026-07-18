using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;
using CommonApiModelMetadataTestCatalog = AtomicArt.Tests.Common.Generation.ApiModelMetadataTestCatalog;

namespace AtomicArt.Domain.Tests.Generation;

internal static class ApiModelMetadataTestCatalog
{
    public static GenerationModelConstraints LoadNanoBanana2Constraints(string? modelId = null)
    {
        GenerationModelMetadataDto metadata = LoadNanoBanana2Metadata();

        return new GenerationModelConstraints(
            modelId ?? metadata.Id,
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
            metadata.Attachments.SupportedContentTypes);
    }

    public static GenerationModelMetadataDto LoadNanoBanana2Metadata()
    {
        return CommonApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
    }
}
