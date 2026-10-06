using System.Text.Json;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation.OpenRouter;

internal static class OpenRouterGenerationContextTestFactory
{
    public static StreamingGenerationProviderContext Create(
        string modelId,
        string resolution = "1K",
        string aspectRatio = GenerationAspectRatios.Auto,
        string? quality = null,
        IReadOnlyList<IGenerationAttachmentSource>? attachments = null)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadCatalog()
            .Models.Single(model => model.Id == modelId);
        Dictionary<string, JsonElement> parameters = new(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(quality))
        {
            parameters[GenerationParameterNames.Quality] = JsonSerializer.SerializeToElement(quality);
        }

        StreamingImageGenerationRequest request = new(
            Guid.Parse("b06c4d8c-2d05-4fce-a005-2ec1e3da9b82"),
            1,
            modelId,
            "A bright landscape",
            aspectRatio,
            resolution,
            metadata.Temperature.Default,
            metadata.Thinking?.Default,
            parameters,
            attachments ?? Array.Empty<IGenerationAttachmentSource>());

        return new StreamingGenerationProviderContext(
            request,
            metadata.Provider,
            metadata.ProviderModelId,
            metadata.Pricing,
            TestGenerationCredentials.ProviderCredential,
            metadata.TransportLimits);
    }
}
