using System.Text;
using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.OpenRouter;

namespace AtomicArt.Infrastructure.Tests.Generation.OpenRouter;

public sealed class OpenRouterChatCompletionResponseTransformerTests
{
    [Fact]
    public async Task CompleteAsync_WithPreviewAndFinalImages_WritesFinalImageAsImageApiResponse()
    {
        byte[] imageBytes = [1, 2, 3, 4];
        byte[] duplicateImageBytes = [5, 6, 7, 8];
        string responseJson = $$"""
        {
          "choices": [
            {
              "message": {
                "content": "Created image.",
                "images": [
                  { "image_url": { "url": "data:image/webp;base64,{{Convert.ToBase64String(imageBytes)}}" } },
                  { "image_url": { "url": "data:image/png;base64,{{Convert.ToBase64String(duplicateImageBytes)}}" } }
                ]
              }
            }
          ]
        }
        """;
        byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
        OpenRouterChatCompletionResponseTransformer transformer = new();
        using MemoryStream destination = new();

        for (int index = 0; index < responseBytes.Length; index++)
        {
            await transformer.AppendAsync(responseBytes.AsMemory(index, 1), CancellationToken.None);
        }

        string contentType = await transformer.CompleteAsync(destination, CancellationToken.None);

        contentType.Should().Be(GenerationImageContentTypes.Png);
        using JsonDocument document = JsonDocument.Parse(destination.ToArray());
        JsonElement image = document.RootElement.GetProperty("data")[0];

        image.GetProperty("b64_json").GetString()
            .Should().Be(Convert.ToBase64String(duplicateImageBytes));
        image.GetProperty("media_type").GetString().Should().Be("image/png");
    }

    [Fact]
    public async Task CompleteAsync_WithoutImageDataUrl_ThrowsInvalidResponse()
    {
        OpenRouterChatCompletionResponseTransformer transformer = new();
        using MemoryStream destination = new();

        await transformer.AppendAsync(
            "{\"choices\":[{\"message\":{\"content\":\"No image\"}}]}"u8.ToArray(),
            CancellationToken.None);

        Func<Task> act = async () =>
            await transformer.CompleteAsync(destination, CancellationToken.None);

        OpenRouterImageException exception = (await act
            .Should()
            .ThrowAsync<OpenRouterImageException>())
            .Which;
        exception.FailureKind.Should().Be(
            ImageGenerationProviderFailureKind.InvalidResponse);
    }
}
