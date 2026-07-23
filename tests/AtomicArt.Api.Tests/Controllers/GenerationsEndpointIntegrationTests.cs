using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using FluentAssertions;
using Xunit;

using AtomicArt.Api.Tests.ModelMetadata;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;
using AtomicArt.Tests.Common;

namespace AtomicArt.Api.Tests.Controllers;

public sealed class GenerationsEndpointIntegrationTests
{
    private static readonly Guid LogicalGenerationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task PostAsync_WithStreamingMultipartBody_DoesNotPreReadRequestBody()
    {
        using TemporaryDirectory contentRoot = new(
            TestDirectories.GetUniqueAssemblyDirectoryPath(
                typeof(GenerationsEndpointIntegrationTests)));
        ApiContentRootTestFiles.CopyModelMetadata(contentRoot.DirectoryPath);
        ApiContentRootTestFiles.WriteAppSettings(
            contentRoot.DirectoryPath,
            """
            {
              "GoogleInteractions": {
                "TimeoutSeconds": 900
              },
              "Generation": {
                "MaxConcurrentGenerations": 64
              },
              "TestGeneration": {
                "Enabled": false
              }
            }
            """);
        await using WebApplicationFactory<Program> factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                    builder.UseContentRoot(contentRoot.DirectoryPath));
        using HttpClient client = factory.CreateClient();
        using MultipartFormDataContent content = CreateRequestContent();

        using HttpResponseMessage response = await client.PostAsync(
            $"/{GenerationApiRoutes.Generations}",
            content,
            CancellationToken.None);
        string responseJson = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(responseJson);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        document.RootElement
            .GetProperty(
                GenerationApiRoutes.ProblemDetailsErrorCodeExtensionName)
            .GetString()
            .Should()
            .Be(GenerationErrorCodes.ModelNotFound);
    }

    private static MultipartFormDataContent CreateRequestContent()
    {
        byte[] attachmentBytes =
        [
            0x89,
            0x50,
            0x4E,
            0x47,
            0x0D,
            0x0A,
            0x1A,
            0x0A
        ];
        GenerationRequestMetadataDto metadata = new(
            LogicalGenerationId,
            1,
            "missing-model",
            "Create an image",
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new List<GenerationAttachmentMetadataDto>
            {
                new(
                    0,
                    "reference.png",
                    GenerationImageContentTypes.Png,
                    attachmentBytes.LongLength)
            });
        string metadataJson = JsonSerializer.Serialize(
            metadata,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        MultipartFormDataContent content = new();
        content.Add(
            new StringContent(
                metadataJson,
                Encoding.UTF8,
                "application/json"),
            GenerationApiRoutes.MetadataPartName);
        ByteArrayContent attachmentContent = new(attachmentBytes);
        attachmentContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                GenerationImageContentTypes.Png);
        content.Add(
            attachmentContent,
            $"{GenerationApiRoutes.AttachmentPartNamePrefix}0",
            "reference.png");

        return content;
    }
}
