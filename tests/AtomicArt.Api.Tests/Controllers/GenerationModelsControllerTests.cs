using System.Net;
using System.Net.Http.Json;

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
using AtomicArt.Tests.Common;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Api.Tests.Controllers;

public sealed class GenerationModelsControllerTests
{
    [Fact]
    public async Task GetAsync_ThroughAspNetPipeline_ReturnsSerializedCatalog()
    {
        using TemporaryDirectory contentRoot = new(
            TestDirectories.GetUniqueAssemblyDirectoryPath(typeof(GenerationModelsControllerTests)));
        ConfigureContentRoot(
            contentRoot,
            ApiTestAppSettingsJson.Create(false, "TestGenerationImages"));
        await using WebApplicationFactory<Program> factory = CreateFactory(contentRoot.DirectoryPath);
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
            ApiModelMetadataStartupTestCatalog.LoadCatalog().Models.Count);
        catalog.Models.Should().Contain(model => model.Id == ApiModelMetadataTestCatalog.NanoBanana2ModelId);
        catalog.Models.Should().Contain(model => model.Id == ApiModelMetadataTestCatalog.NanoBananaProModelId);
        catalog.Models.Should().NotContain(model => model.Id == TestGenerationModelCatalogAugmenter.ModelId);
    }

    [Fact]
    public async Task GetAsync_ThroughAspNetPipelineWithTestGenerationEnabled_ReturnsTestModel()
    {
        using TemporaryDirectory contentRoot = new(
            TestDirectories.GetUniqueAssemblyDirectoryPath(typeof(GenerationModelsControllerTests)));
        ConfigureContentRoot(
            contentRoot,
            ApiTestAppSettingsJson.Create(true, "TestGenerationImages"));
        await using WebApplicationFactory<Program> factory = CreateFactory(contentRoot.DirectoryPath);
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

    [Fact]
    public async Task GetAsync_WithCatalog_ReturnsOkResponse()
    {
        GenerationModelCatalogDto catalog = ApiModelMetadataStartupTestCatalog.LoadCatalog();
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
        string contentRoot = ApiModelMetadataTestCatalog.GetApiContentRoot();

        return CreateFactory(contentRoot);
    }

    private static WebApplicationFactory<Program> CreateFactory(string contentRoot)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseContentRoot(contentRoot));
    }

    private static void ConfigureContentRoot(
        TemporaryDirectory contentRoot,
        string appSettingsJson)
    {
        ApiContentRootTestFiles.CopyModelMetadata(contentRoot.DirectoryPath);
        ApiContentRootTestFiles.WriteAppSettings(contentRoot.DirectoryPath, appSettingsJson);
    }
}
