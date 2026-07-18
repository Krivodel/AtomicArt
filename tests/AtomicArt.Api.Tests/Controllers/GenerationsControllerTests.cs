using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;

using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

using AtomicArt.Api.Controllers;
using AtomicArt.Api.Filters;
using AtomicArt.Api.Tests.ModelMetadata;
using AtomicArt.Application;
using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain;
using AtomicArt.Infrastructure;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Tests.Common;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Api.Tests.Controllers;

public sealed class GenerationsControllerTests
{
    private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static string ModelId => ApiModelMetadataTestCatalog.NanoBanana2ModelId;

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsOkResponse()
    {
        GenerationBatchDto batch = CreateBatchWithContent();
        Mock<IMediator> mediator = CreateMediator(Result<GenerationBatchDto>.Success(batch));
        GenerationsController controller = CreateController(
            mediator.Object,
            TestGenerationCredentials.ProviderCredential);
        ImageGenerationRequestDto request = CreateRequest();

        IActionResult actionResult = await controller.CreateAsync(request, CancellationToken.None);

        OkObjectResult okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(batch);
        string responseJson = JsonSerializer.Serialize(okResult.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        responseJson.Should().Contain("\"imageContent\"");
        responseJson.Should().NotContain("previewContent");
        responseJson.Should().NotContain("previewPath");
        mediator.Verify(
            currentMediator => currentMediator.Send(
                It.Is<CreateImageGenerationCommand>(command =>
                    command.Request.ModelId == request.ModelId
                    && command.Request.Prompt == request.Prompt
                    && command.Request.AspectRatio == request.AspectRatio
                    && command.Request.Resolution == request.Resolution
                    && command.Request.GenerationCount == request.GenerationCount
                    && ReferenceEquals(command.Request.AttachedImages, request.AttachedImages)
                    && command.ProviderCredential == TestGenerationCredentials.ProviderCredential),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithHttpsBoundary_ReturnsOkResponse()
    {
        GenerationBatchDto batch = CreateBatchWithContent();
        Mock<IMediator> mediator = CreateMediator(Result<GenerationBatchDto>.Success(batch));
        GenerationsController controller = CreateController(
            mediator.Object,
            TestGenerationCredentials.ProviderCredential,
            isHttps: true,
            remoteIpAddress: IPAddress.Parse("203.0.113.10"));
        ImageGenerationRequestDto request = CreateRequest();

        IActionResult actionResult = await controller.CreateAsync(request, CancellationToken.None);

        OkObjectResult okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(batch);
        mediator.Verify(
            currentMediator => currentMediator.Send(
                It.IsAny<CreateImageGenerationCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNonLoopbackHttpBoundary_ReturnsOkResponse()
    {
        GenerationBatchDto batch = CreateBatchWithContent();
        Mock<IMediator> mediator = CreateMediator(Result<GenerationBatchDto>.Success(batch));
        GenerationsController controller = CreateController(
            mediator.Object,
            TestGenerationCredentials.ProviderCredential,
            isHttps: false,
            remoteIpAddress: IPAddress.Parse("203.0.113.10"));
        ImageGenerationRequestDto request = CreateRequest();

        IActionResult actionResult = await controller.CreateAsync(request, CancellationToken.None);

        OkObjectResult okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(batch);
        mediator.Verify(
            currentMediator => currentMediator.Send(
                It.IsAny<CreateImageGenerationCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateEndpoint_WithoutProviderCredential_ReturnsUnauthorized()
    {
        await using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();
        ImageGenerationRequestDto request = CreateRequest();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            GenerationApiRoutes.Generations,
            request,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEndpoint_WithTestModelWithoutProviderCredential_ReturnsOk()
    {
        string relativeImagesDirectory = Path.Combine("TestGenerationImages", Guid.NewGuid().ToString("N"));
        using TemporaryDirectory contentRoot = new(
            TestDirectories.GetUniqueDirectoryPath(
                typeof(GenerationsControllerTests),
                "ContentRoot"));
        ApiContentRootTestFiles.CopyModelMetadata(contentRoot.DirectoryPath);
        ApiContentRootTestFiles.WriteAppSettings(
            contentRoot.DirectoryPath,
            ApiTestAppSettingsJson.Create(true, relativeImagesDirectory));
        using TemporaryDirectory imagesDirectory = new(
            Path.Combine(AppContext.BaseDirectory, relativeImagesDirectory));
        await File.WriteAllBytesAsync(
            Path.Combine(imagesDirectory.DirectoryPath, "any-file-name"),
            CreatePngBytes(),
            CancellationToken.None);
        await using WebApplicationFactory<Program> factory = CreateFactoryWithTestGeneration(
            contentRoot.DirectoryPath);
        using HttpClient client = factory.CreateClient();
        ImageGenerationRequestDto request = CreateTestRequest();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            GenerationApiRoutes.Generations,
            request,
            CancellationToken.None);
        GenerationBatchDto? batch = await response.Content
            .ReadFromJsonAsync<GenerationBatchDto>(CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        batch.Should().NotBeNull();
        GenerationItemDto item = batch.Items.Should().ContainSingle().Which;
        item.ModelId.Should().Be(TestGenerationModelCatalogAugmenter.ModelId);
        GenerationImageContentDto imageContent = item.ImageContent
            ?? throw new InvalidOperationException("Generated image content is missing.");
        imageContent.ContentType.Should().Be(GenerationImageContentTypes.Png);
    }

    [Fact]
    public async Task ModelsEndpoint_WithoutProviderCredential_ReturnsOk()
    {
        await using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            GenerationApiRoutes.Models,
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void LaunchSettings_WithDefaultApiUrl_UsesLocalApiPort()
    {
        string path = TestRepositoryFiles.Find(Path.Combine(
            "src",
            "AtomicArt.Api",
            "Properties",
            "launchSettings.json"));
        string json = File.ReadAllText(path);
        using JsonDocument document = JsonDocument.Parse(json);
        string? applicationUrl = document.RootElement
            .GetProperty("profiles")
            .GetProperty("AtomicArt.Api")
            .GetProperty("applicationUrl")
            .GetString();

        applicationUrl.Should().Be(LocalApiEndpointTestDefaults.LocalApiUrl);
    }

    [Fact]
    public void RequiredBodyActionFilter_WithNullCreateRequest_ReturnsBadRequest()
    {
        RequiredBodyActionFilter filter = new(
            NullLogger<RequiredBodyActionFilter>.Instance);
        ActionExecutingContext context = CreateBodyActionContext(nameof(GenerationsController.CreateAsync));

        filter.OnActionExecuting(context);

        BadRequestObjectResult badRequest = context.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problemDetails = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateAsync_WithValidationResult_ReturnsBadRequestProblemDetails()
    {
        Result<GenerationBatchDto> result = Result<GenerationBatchDto>.ValidationError("ERR-GEN-004", "Invalid request");
        Mock<IMediator> mediator = CreateMediator(result);
        GenerationsController controller = CreateController(mediator.Object);
        ImageGenerationRequestDto request = CreateRequest();

        IActionResult actionResult = await controller.CreateAsync(request, CancellationToken.None);

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Extensions.Should().ContainKey("code");
    }

    [Fact]
    public async Task CreateAsync_WithNullAttachedImage_ReturnsBadRequestProblemDetails()
    {
        Result<GenerationBatchDto> result = Result<GenerationBatchDto>.ValidationError("ERR-GEN-004", "Invalid attachment");
        Mock<IMediator> mediator = CreateMediator(result);
        GenerationsController controller = CreateController(mediator.Object);
        ImageGenerationRequestDto request = CreateRequestWithNullAttachedImage();

        IActionResult actionResult = await controller.CreateAsync(request, CancellationToken.None);

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Extensions.Should().ContainKey("code");
        mediator.Verify(
            currentMediator => currentMediator.Send(
                It.Is<CreateImageGenerationCommand>(command => CommandHasSingleNullAttachedImage(command)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_DoesNotWriteGenerationFiles()
    {
        using TemporaryCurrentDirectory outputDirectory = new(
            typeof(GenerationsControllerTests),
            nameof(CreateAsync_WithValidRequest_DoesNotWriteGenerationFiles));
        ServiceCollection services = [];
        IConfiguration configuration = CreateConfiguration();
        services.AddLogging();
        services.AddDomainServices();
        services.AddSingleton(ApiModelMetadataStartupTestCatalog.LoadCatalog());
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);
        services.AddScoped<Application.Features.Generation.Interfaces.IImageGenerationContentProvider>(
            _ => new TestImageGenerationContentProvider());
        await using ServiceProvider serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        using IServiceScope scope = serviceProvider.CreateScope();
        IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        GenerationsController controller = CreateController(
            mediator,
            TestGenerationCredentials.ProviderCredential);
        ImageGenerationRequestDto request = CreateRequest();

        IActionResult actionResult = await controller.CreateAsync(request, CancellationToken.None);

        OkObjectResult okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        GenerationBatchDto batch = okResult.Value.Should().BeOfType<GenerationBatchDto>().Subject;
        batch.Items.Should().ContainSingle();
        GenerationItemDto item = batch.Items.Single();
        item.ImagePath.Should().BeNull();
        item.ImageContent.Should().NotBeNull();
        Directory.Exists(Path.Combine(outputDirectory.DirectoryPath, "generations")).Should().BeFalse();
        outputDirectory.GetEntries().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithNotFoundResult_ReturnsBadRequestProblemDetails()
    {
        Result<GenerationBatchDto> result = Result<GenerationBatchDto>.NotFound("ERR-GEN-001", "Model not found");
        Mock<IMediator> mediator = CreateMediator(result);
        GenerationsController controller = CreateController(mediator.Object);
        ImageGenerationRequestDto request = CreateRequest();

        IActionResult actionResult = await controller.CreateAsync(request, CancellationToken.None);

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().NotBe("Model not found");
        problemDetails.Extensions.Should().ContainKey("code");
    }

    [Fact]
    public async Task CreateAsync_WithUnavailableResult_ReturnsInternalServerErrorProblemDetails()
    {
        string rawErrorMessage = "Provider failed at C:\\internal\\path";
        Result<GenerationBatchDto> result = Result<GenerationBatchDto>.Unavailable("ERR-GEN-999", rawErrorMessage);
        Mock<IMediator> mediator = CreateMediator(result);
        GenerationsController controller = CreateController(mediator.Object);
        ImageGenerationRequestDto request = CreateRequest();

        IActionResult actionResult = await controller.CreateAsync(request, CancellationToken.None);

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Detail.Should().NotBe(rawErrorMessage);
        problemDetails.Extensions.Should().ContainKey("code");
    }

    [Theory]
    [InlineData(
        ImageGenerationProviderFailureKind.Authentication,
        StatusCodes.Status401Unauthorized)]
    [InlineData(
        ImageGenerationProviderFailureKind.Authorization,
        StatusCodes.Status403Forbidden)]
    [InlineData(
        ImageGenerationProviderFailureKind.RateLimited,
        StatusCodes.Status429TooManyRequests)]
    [InlineData(
        ImageGenerationProviderFailureKind.RequestRejected,
        StatusCodes.Status502BadGateway)]
    [InlineData(
        ImageGenerationProviderFailureKind.ResourceNotFound,
        StatusCodes.Status502BadGateway)]
    [InlineData(
        ImageGenerationProviderFailureKind.InternalError,
        StatusCodes.Status502BadGateway)]
    [InlineData(
        ImageGenerationProviderFailureKind.InvalidResponse,
        StatusCodes.Status502BadGateway)]
    [InlineData(
        ImageGenerationProviderFailureKind.Unknown,
        StatusCodes.Status502BadGateway)]
    [InlineData(
        ImageGenerationProviderFailureKind.Timeout,
        StatusCodes.Status504GatewayTimeout)]
    [InlineData(
        ImageGenerationProviderFailureKind.Unavailable,
        StatusCodes.Status503ServiceUnavailable)]
    public async Task CreateAsync_WithProviderUnavailableResult_ReturnsProviderStatusProblemDetails(
        ImageGenerationProviderFailureKind failureKind,
        int expectedStatusCode)
    {
        string rawErrorMessage = "Provider failed with secret response body";
        string errorCode = ImageGenerationProviderFailureCatalog.GetErrorCode(failureKind);
        Result<GenerationBatchDto> result = Result<GenerationBatchDto>.Unavailable(errorCode, rawErrorMessage);
        Mock<IMediator> mediator = CreateMediator(result);
        GenerationsController controller = CreateController(mediator.Object);
        ImageGenerationRequestDto request = CreateRequest();

        IActionResult actionResult = await controller.CreateAsync(request, CancellationToken.None);

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatusCode);
        problemDetails.Detail.Should().NotBe(rawErrorMessage);
        problemDetails.Extensions.Should().ContainKey("code");
        problemDetails.Extensions["code"].Should().Be(errorCode);
    }

    private static Mock<IMediator> CreateMediator(Result<GenerationBatchDto> result)
    {
        Mock<IMediator> mediator = new();
        mediator
            .Setup(currentMediator => currentMediator.Send(
                It.IsAny<CreateImageGenerationCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return mediator;
    }

    private static GenerationsController CreateController(
        IMediator mediator,
        string? providerCredential = TestGenerationCredentials.ProviderCredential,
        bool isHttps = false,
        IPAddress? remoteIpAddress = null,
        GenerationModelCatalogDto? modelCatalog = null)
    {
        DefaultHttpContext httpContext = new()
        {
            Request =
            {
                Scheme = isHttps ? "https" : "http"
            },
            Connection =
            {
                RemoteIpAddress = remoteIpAddress ?? IPAddress.Loopback
            }
        };

        GenerationsController controller = new(
            mediator,
            modelCatalog ?? ApiModelMetadataStartupTestCatalog.LoadCatalog(),
            NullLogger<GenerationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        if (!string.IsNullOrWhiteSpace(providerCredential))
        {
            controller.HttpContext.Request.Headers[GenerationApiRoutes.ProviderApiKeyHeaderName] = providerCredential;
        }

        return controller;
    }

    private static IConfiguration CreateConfiguration()
    {
        Dictionary<string, string?> values = new(StringComparer.Ordinal)
        {
            [CreateGoogleInteractionsKey(nameof(GoogleInteractionsOptions.BaseUrl))] =
                "https://generativelanguage.googleapis.com",
            [CreateGoogleInteractionsKey(nameof(GoogleInteractionsOptions.TimeoutSeconds))] = "30"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static string CreateGoogleInteractionsKey(string key)
    {
        return $"{GoogleInteractionsOptions.SectionName}:{key}";
    }

    private static bool CommandHasSingleNullAttachedImage(CreateImageGenerationCommand command)
    {
        return command.Request.AttachedImages is [null];
    }

    private static ImageGenerationRequestDto CreateRequest()
    {
        return CreateRequest(ModelId, ApiModelMetadataStartupTestCatalog.LoadCatalog());
    }

    private static ImageGenerationRequestDto CreateTestRequest()
    {
        GenerationModelCatalogDto catalog = TestGenerationModelCatalogAugmenter.AddTestModelIfEnabled(
            ApiModelMetadataStartupTestCatalog.LoadCatalog(),
            new TestGenerationOptions
            {
                Enabled = true
            });

        return CreateRequest(TestGenerationModelCatalogAugmenter.ModelId, catalog);
    }

    private static ImageGenerationRequestDto CreateRequest(
        string modelId,
        GenerationModelCatalogDto catalog)
    {
        GenerationModelMetadataDto metadata = catalog
            .Models
            .Single(model => model.Id == modelId);

        return new ImageGenerationRequestDto(
            modelId,
            "Prompt",
            metadata.AspectRatios.First(),
            metadata.Resolutions.First(),
            metadata.Temperature.Default,
            1,
            []);
    }

    private static WebApplicationFactory<Program> CreateFactoryWithTestGeneration(string contentRoot)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseContentRoot(contentRoot));
    }

    private static byte[] CreatePngBytes()
    {
        byte[] content = [.. GenerationImageFileSignatures.Png, 0x00];

        return content;
    }

    private static ImageGenerationRequestDto CreateRequestWithNullAttachedImage()
    {
        ImageGenerationRequestDto? request = JsonSerializer.Deserialize<ImageGenerationRequestDto>(
            $$"""
            {
              "modelId": "{{ModelId}}",
              "prompt": "Prompt",
              "aspectRatio": "авто",
              "resolution": "1k",
              "generationCount": 1,
              "attachedImages": [null]
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (request is null)
        {
            throw new InvalidOperationException("Failed to deserialize test request.");
        }

        return request;
    }

    private static GenerationBatchDto CreateBatchWithContent()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationImageContentDto content = new("image/png", "iVBORw0KGgo=");
        GenerationItemDto item = new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ModelId,
            metadata.DisplayName,
            "Prompt",
            GenerationAspectRatios.Auto,
            "1k",
            new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            GenerationItemStatus.Generated,
            null,
            content);

        return new GenerationBatchDto(BatchId, [item]);
    }

    private static ActionExecutingContext CreateBodyActionContext(string actionName)
    {
        ActionContext actionContext = new(
            new DefaultHttpContext(),
            new RouteData(),
            CreateActionDescriptor(actionName));
        Dictionary<string, object?> actionArguments = new(StringComparer.Ordinal)
        {
            ["request"] = null
        };

        return new ActionExecutingContext(
            actionContext,
            Array.Empty<IFilterMetadata>(),
            actionArguments,
            new object());
    }

    private static ControllerActionDescriptor CreateActionDescriptor(string actionName)
    {
        System.Reflection.MethodInfo methodInfo = typeof(GenerationsController)
            .GetMethod(actionName)
            ?? throw new InvalidOperationException("Controller action was not found.");
        System.Reflection.ParameterInfo requestParameter = methodInfo
            .GetParameters()
            .Single(parameter => parameter.Name == "request");

        return new ControllerActionDescriptor
        {
            MethodInfo = methodInfo,
            Parameters = new List<ParameterDescriptor>
            {
                new ControllerParameterDescriptor
                {
                    Name = "request",
                    ParameterInfo = requestParameter,
                    BindingInfo = new BindingInfo
                    {
                        BindingSource = BindingSource.Body
                    }
                }
            }
        };
    }

    private sealed class TestImageGenerationContentProvider
        : Application.Features.Generation.Interfaces.IImageGenerationContentProvider
    {
        public Task<ImageGenerationContentResult> GetContentAsync(
            ImageGenerationContentProviderContext context,
            CancellationToken ct)
        {
            ImageGenerationContentResult result = new(
                "image/png",
                "iVBORw0KGgo=");

            return Task.FromResult(result);
        }
    }
}
