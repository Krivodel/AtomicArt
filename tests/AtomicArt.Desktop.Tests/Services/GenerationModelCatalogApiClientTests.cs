using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class GenerationModelCatalogApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetCatalogAsync_WithSuccessfulResponse_DeserializesCatalog()
    {
        CapturingHttpMessageHandler handler = new(CreateCatalogJson());
        using HttpClient httpClient = new(handler);
        GenerationModelCatalogApiClient apiClient = new(
            httpClient,
            TestApiEndpointServiceFactory.Create(),
            NullLogger<GenerationModelCatalogApiClient>.Instance);

        GenerationModelCatalogDto catalog = await apiClient.GetCatalogAsync(CancellationToken.None);

        handler.RequestMethod.Should().Be(HttpMethod.Get);
        Uri requestUri = handler.RequestUri
            ?? throw new InvalidOperationException("Request URI must be captured.");
        requestUri.AbsolutePath.Should().Be($"/{GenerationApiRoutes.Models}");
        catalog.Models.Should().HaveCount(3);
        catalog.Models.Should().Contain(model => model.Id == ApiModelMetadataTestCatalog.NanoBanana2ModelId);
        catalog.Models.Should().Contain(model => model.Id == ApiModelMetadataTestCatalog.NanoBananaProModelId);
    }

    [Fact]
    public async Task GetCatalogAsync_WithEmptyResponse_ThrowsInvalidOperationException()
    {
        CapturingHttpMessageHandler handler = new("null");
        using HttpClient httpClient = new(handler);
        GenerationModelCatalogApiClient apiClient = new(
            httpClient,
            TestApiEndpointServiceFactory.Create(),
            NullLogger<GenerationModelCatalogApiClient>.Instance);

        Func<Task> act = () => apiClient.GetCatalogAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetCatalogAsync_WithOversizedProblemDetails_DoesNotLogResponseBody()
    {
        const string ErrorCode = GenerationProviderFailureErrorCodes.Unavailable;
        string confidentialBody = new('S', 20 * 1024);
        CapturingHttpMessageHandler handler = new(
            $$"""{"code":"{{ErrorCode}}","detail":"{{confidentialBody}}"}""",
            HttpStatusCode.ServiceUnavailable);
        using HttpClient httpClient = new(handler);
        RecordingLogger<GenerationModelCatalogApiClient> logger = new();
        GenerationModelCatalogApiClient apiClient = new(
            httpClient,
            TestApiEndpointServiceFactory.Create(),
            logger);

        Func<Task> act = () => apiClient.GetCatalogAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        logger.Messages.Should().Contain(message =>
            message.Contains("503", StringComparison.Ordinal)
            && message.Contains("unavailable", StringComparison.Ordinal));
        logger.Messages.Should().OnlyContain(message =>
            !message.Contains(confidentialBody, StringComparison.Ordinal)
            && !message.Contains(ErrorCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetCatalogAsync_WhenEndpointChanges_KeepsInFlightAddressAndUsesNewAddressNext()
    {
        DelayedCapturingHttpMessageHandler handler = new(CreateCatalogJson());
        using HttpClient httpClient = new(handler);
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create(
            "https://first.atomicart.test/");
        GenerationModelCatalogApiClient apiClient = new(
            httpClient,
            endpointService,
            NullLogger<GenerationModelCatalogApiClient>.Instance);

        Task<GenerationModelCatalogDto> firstRequest = apiClient.GetCatalogAsync(
            CancellationToken.None);
        await handler.RequestReceivedTask;
        Uri firstRequestUri = handler.RequestUris.Should().ContainSingle().Subject;
        ApiBaseAddress.TryCreate(
            "https://second.atomicart.test/root/",
            out ApiBaseAddress? secondAddress).Should().BeTrue();
        endpointService.SetBaseAddress(secondAddress
            ?? throw new InvalidOperationException("Second address is required."));
        handler.Complete();
        await firstRequest;

        await apiClient.GetCatalogAsync(CancellationToken.None);

        firstRequestUri.Should().Be(
            new Uri($"https://first.atomicart.test/{GenerationApiRoutes.Models}"));
        handler.RequestUris.Should().HaveCount(2);
        handler.RequestUris[1].Should().Be(
            new Uri($"https://second.atomicart.test/root/{GenerationApiRoutes.Models}"));
    }

    private static string CreateCatalogJson()
    {
        GenerationModelCatalogDto catalog = ApiModelMetadataTestCatalog.LoadCatalog();

        return JsonSerializer.Serialize(catalog, JsonOptions);
    }

    private sealed class DelayedCapturingHttpMessageHandler : HttpMessageHandler
    {
        public IReadOnlyList<Uri> RequestUris => _requestUris;
        public Task RequestReceivedTask => _requestReceived.Task;

        private readonly string _responseJson;
        private readonly List<Uri> _requestUris = [];
        private readonly TaskCompletionSource _requestReceived = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public DelayedCapturingHttpMessageHandler(string responseJson)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);

            _responseJson = responseJson;
        }

        public void Complete()
        {
            _release.TrySetResult();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri requestUri = request.RequestUri
                ?? throw new InvalidOperationException("Request URI is required.");
            _requestUris.Add(requestUri);
            _requestReceived.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
