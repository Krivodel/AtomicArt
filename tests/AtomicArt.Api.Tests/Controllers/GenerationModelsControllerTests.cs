using System.Net;
using System.Net.Http.Json;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

using AtomicArt.Api.Controllers;
using AtomicArt.Api.Tests.ModelMetadata;
using AtomicArt.Application.Features.Generation.Queries.GetGenerationModels;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Api.Tests.Controllers;

public sealed class GenerationModelsControllerTests
{
    [Fact]
    public async Task GetAsync_ThroughAspNetPipeline_ReturnsSerializedCatalog()
    {
        string contentRoot = CreateContentRoot(testGenerationEnabled: false);

        try
        {
            await using WebApplicationFactory<Program> factory = CreateFactory(contentRoot);
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync(
                $"/{GenerationApiRoutes.Models}",
                CancellationToken.None);
            GenerationModelCatalogDto? catalog = await response.Content
                .ReadFromJsonAsync<GenerationModelCatalogDto>(CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            if (catalog is null)
            {
                throw new InvalidOperationException("Generation model catalog response is missing.");
            }

            catalog.Models.Should().HaveCount(
                ApiModelMetadataTestCatalog.LoadCatalog().Models.Count);
            catalog.Models.Should().Contain(model => model.Id == ApiModelMetadataTestCatalog.NanoBanana2ModelId);
            catalog.Models.Should().Contain(model => model.Id == ApiModelMetadataTestCatalog.NanoBananaProModelId);
            catalog.Models.Should().NotContain(model => model.Id == TestGenerationModelCatalogAugmenter.ModelId);
        }
        finally
        {
            DeleteDirectoryIfExists(contentRoot);
        }
    }

    [Fact]
    public async Task GetAsync_ThroughAspNetPipelineWithTestGenerationEnabled_ReturnsTestModel()
    {
        string contentRoot = CreateContentRoot(testGenerationEnabled: true);

        try
        {
            await using WebApplicationFactory<Program> factory = CreateFactory(contentRoot);
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync(
                $"/{GenerationApiRoutes.Models}",
                CancellationToken.None);
            GenerationModelCatalogDto? catalog = await response.Content
                .ReadFromJsonAsync<GenerationModelCatalogDto>(CancellationToken.None);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            catalog.Should().NotBeNull();
            GenerationModelMetadataDto testModel = catalog.Models
                .Single(model => model.Id == TestGenerationModelCatalogAugmenter.ModelId);
            testModel.DisplayName.Should().Be("Test");
            testModel.Provider.Should().Be(GenerationProviderIds.Test);
            testModel.PanelId.Should().Be(GenerationPanelIds.NanoBanana);
        }
        finally
        {
            DeleteDirectoryIfExists(contentRoot);
        }
    }

    [Fact]
    public async Task GetAsync_WithCatalog_ReturnsOkResponse()
    {
        GenerationModelCatalogDto catalog = CreateMinimalCatalog();
        Mock<IMediator> mediator = new();
        mediator
            .Setup(currentMediator => currentMediator.Send(
                It.IsAny<GetGenerationModelsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalog);
        GenerationModelsController controller = new(mediator.Object);

        IActionResult actionResult = await controller.GetAsync(CancellationToken.None);

        OkObjectResult okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(catalog);
        mediator.Verify(
            currentMediator => currentMediator.Send(
                It.IsAny<GetGenerationModelsQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GetAsync_WithCurrentLocalApiPhase_IsExplicitlyPublic()
    {
        System.Reflection.MethodInfo methodInfo = typeof(GenerationModelsController)
            .GetMethod(nameof(GenerationModelsController.GetAsync))
            ?? throw new InvalidOperationException("Controller action was not found.");

        methodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), false)
            .Should()
            .ContainSingle();
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        string contentRoot = ApiModelMetadataTestCatalog.GetContentRoot();

        return CreateFactory(contentRoot);
    }

    private static WebApplicationFactory<Program> CreateFactory(string contentRoot)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseContentRoot(contentRoot));
    }

    private static string CreateContentRoot(bool testGenerationEnabled)
    {
        string contentRoot = Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.Api.Tests",
            nameof(GenerationModelsControllerTests),
            Guid.NewGuid().ToString("N"));
        string metadataPath = Path.Combine(contentRoot, GenerationModelCatalogDefaults.RelativePath);
        string metadataDirectory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidOperationException("Model metadata directory was not found.");
        string sourceMetadataPath = ApiModelMetadataTestCatalog.GetMetadataPath();

        Directory.CreateDirectory(metadataDirectory);
        File.Copy(sourceMetadataPath, metadataPath);
        File.WriteAllText(
            Path.Combine(contentRoot, "appsettings.json"),
            CreateAppSettingsJson(testGenerationEnabled),
            Encoding.UTF8);

        return contentRoot;
    }

    private static string CreateAppSettingsJson(bool testGenerationEnabled)
    {
        return $$"""
        {
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "GoogleInteractions": {
            "BaseUrl": "https://generativelanguage.googleapis.com",
            "TimeoutSeconds": 100
          },
          "TestGeneration": {
            "Enabled": {{testGenerationEnabled.ToString().ToLowerInvariant()}},
            "ImagesDirectory": "TestGenerationImages",
            "MaxImageBytes": 524288000
          },
          "AllowedHosts": "*"
        }
        """;
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }
    }

    private static GenerationModelCatalogDto CreateMinimalCatalog()
    {
        return new GenerationModelCatalogDto(
        [
            new(
                    "test-model",
                    "Test Model",
                    "google",
                    "provider-test-model",
                    GenerationPanelIds.NanoBanana,
                    1000,
                    500,
                    100,
                    [GenerationAspectRatios.Auto],
                    ["1k"],
                    [1],
                    new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d),
                    new GenerationModelAttachmentMetadataDto(
                        1,
                        1024,
                        2048,
                        [GenerationImageContentTypes.Png]),
                    new GenerationModelPricingMetadataDto(
                        "USD",
                        0.25m,
                        1.50m,
                        30.00m,
                        1120,
                        new Dictionary<string, int>
                        {
                            ["1k"] = 1120
                        }))
        ]);
    }
}
