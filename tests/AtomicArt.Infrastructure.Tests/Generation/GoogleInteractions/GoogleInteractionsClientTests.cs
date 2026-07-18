using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Tests.Common.Generation;

using RecordingClientLogger =
    AtomicArt.Tests.Common.RecordingLogger<
        AtomicArt.Infrastructure.Generation.GoogleInteractions.GoogleInteractionsClient>;

namespace AtomicArt.Infrastructure.Tests.Generation.GoogleInteractions;

public sealed class GoogleInteractionsClientTests
{
    [Fact]
    public async Task CreateInteractionAsync_WithProviderCredential_SendsApiKeyHeaderWithoutQueryString()
    {
        using ClientTestContext context = CreateClient(
            HttpStatusCode.OK,
            """{"status":"completed"}""");

        string responseJson = await context.CreateInteractionAsync();

        responseJson.Should().Be("""{"status":"completed"}""");
        HttpRequestMessage request = context.Handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri.Should().NotBeNull();
        Uri requestUri = request.RequestUri ?? throw new InvalidOperationException("Request URI is missing.");
        requestUri.AbsolutePath.Should().Be("/v1beta/interactions");
        requestUri.Query.Should().BeEmpty();
        requestUri.ToString().Should().NotContain(TestGenerationCredentials.ProviderCredential);
        request.Headers.TryGetValues("X-Goog-Api-Key", out IEnumerable<string>? values).Should().BeTrue();
        values.Should().BeEquivalentTo(new[] { TestGenerationCredentials.ProviderCredential });
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ImageGenerationProviderFailureKind.RequestRejected)]
    [InlineData(HttpStatusCode.Unauthorized, ImageGenerationProviderFailureKind.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, ImageGenerationProviderFailureKind.Authorization)]
    [InlineData(HttpStatusCode.NotFound, ImageGenerationProviderFailureKind.ResourceNotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, ImageGenerationProviderFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.BadGateway, ImageGenerationProviderFailureKind.InvalidResponse)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ImageGenerationProviderFailureKind.Unavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout, ImageGenerationProviderFailureKind.Timeout)]
    [InlineData((HttpStatusCode)418, ImageGenerationProviderFailureKind.Unknown)]
    public async Task CreateInteractionAsync_WithProviderHttpError_MapsFailureKind(
        HttpStatusCode statusCode,
        ImageGenerationProviderFailureKind expectedFailureKind)
    {
        using ClientTestContext context = CreateClient(
            statusCode,
            """{"error":"secret detail"}""");

        Func<Task> act = () => context.CreateInteractionAsync();

        FluentAssertions.Specialized.ExceptionAssertions<ImageGenerationProviderException> assertions =
            await act.Should()
                .ThrowAsync<ImageGenerationProviderException>()
                .WithMessage("The generation provider returned an error.");
        assertions.Which.FailureKind.Should().Be(expectedFailureKind);
        context.Handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateInteractionAsync_WithStructuredProviderError_LogsUsefulDiagnostics()
    {
        const string responseJson =
            """{"error":{"code":403,"status":"PERMISSION_DENIED","message":"Requested model is unavailable in this region."}}""";
        using ClientTestContext context = CreateRecordingClient(
            HttpStatusCode.Forbidden,
            responseJson);

        Func<Task> act = () => context.CreateInteractionAsync();

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = string.Join(Environment.NewLine, context.LogMessages);
        logText.Should().Contain("provider code 403");
        logText.Should().Contain("provider status PERMISSION_DENIED");
        logText.Should().Contain("Requested model is unavailable in this region.");
        logText.Should().NotContain(responseJson);
    }

    [Fact]
    public async Task CreateInteractionAsync_WithStringProviderErrorCode_DoesNotThrowSecondaryException()
    {
        const string responseJson =
            """{"error":{"code":"INVALID_ARGUMENT","status":"INVALID_ARGUMENT","message":"Request format is invalid."}}""";
        using ClientTestContext context = CreateRecordingClient(
            HttpStatusCode.BadRequest,
            responseJson);

        Func<Task> act = () => context.CreateInteractionAsync();

        FluentAssertions.Specialized.ExceptionAssertions<ImageGenerationProviderException> assertions =
            await act.Should()
            .ThrowAsync<ImageGenerationProviderException>()
            .WithMessage("The generation provider returned an error.");
        assertions.Which.FailureKind.Should().Be(ImageGenerationProviderFailureKind.RequestRejected);
        string logText = context.LogMessages.Last();
        logText.Should().Contain("provider status INVALID_ARGUMENT");
        logText.Should().Contain("provider message Request format is invalid.");
    }

    [Fact]
    public async Task CreateInteractionAsync_WithLetterOnlyBase64InProviderMessage_LogsEncodedData()
    {
        const string base64Fragment = "QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFB";
        string responseJson =
            """{"error":{"code":400,"status":"INVALID_ARGUMENT","message":"Encoded value __BASE64__ was rejected."}}"""
                .Replace("__BASE64__", base64Fragment, StringComparison.Ordinal);
        using ClientTestContext context = CreateRecordingClient(
            HttpStatusCode.BadRequest,
            responseJson);

        Func<Task> act = () => context.CreateInteractionAsync();

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = string.Join(Environment.NewLine, context.LogMessages);
        logText.Should().Contain(base64Fragment);
        logText.Should().NotContain("[REDACTED");
    }

    [Fact]
    public async Task CreateInteractionAsync_WithSensitiveProviderMessage_LogsProviderMessageWithoutRedaction()
    {
        const string providerCredential = "provider-key-secret-123456";
        const string requestJson =
            """{"input":[{"text":"very-private-prompt-fragment-that-must-not-leak"},{"inline_data":{"data":"iVBORw0KGgoAAAANSUhEUgAAAAUA"}}]}""";
        const string responseJson =
            """
            {
              "error": {
                "code": 400,
                "status": "../../secret-status",
                "message": "Echo PRIVATE-PROMPT and KEY-SECRET and iVBORw0KGgo; https://secret.invalid/a; owner@example.com; C:\\Users\\owner\\secret.txt; /home/owner/secret.txt"
              }
            }
            """;
        using ClientTestContext context = CreateRecordingClient(
            HttpStatusCode.BadRequest,
            responseJson);

        Func<Task> act = () => context.CreateInteractionAsync(
            requestJson,
            providerCredential);

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = string.Join(Environment.NewLine, context.LogMessages);
        logText.Should().Contain("PRIVATE-PROMPT");
        logText.Should().Contain("KEY-SECRET");
        logText.Should().Contain("iVBORw0KGgo");
        logText.Should().Contain("https://secret.invalid/a");
        logText.Should().Contain("owner@example.com");
        logText.Should().Contain(@"C:\Users\owner\secret.txt");
        logText.Should().Contain("/home/owner/secret.txt");
        logText.Should().NotContain("[REDACTED");
        logText.Should().NotContain("secret-status");
    }

    [Fact]
    public async Task CreateInteractionAsync_WithMalformedRequestStructure_LogsProviderMessage()
    {
        const string responseJson =
            """{"error":{"code":400,"status":"INVALID_ARGUMENT","message":"Potentially echoed private input."}}""";
        using ClientTestContext context = CreateRecordingClient(
            HttpStatusCode.BadRequest,
            responseJson);

        Func<Task> act = () => context.CreateInteractionAsync("{malformed request");

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = string.Join(Environment.NewLine, context.LogMessages);
        logText.Should().Contain("Potentially echoed private input.");
        logText.Should().NotContain("[REDACTED");
    }

    [Fact]
    public async Task CreateInteractionAsync_WithLargeErrorBody_LogsFirst512MessageCharacters()
    {
        string providerMessage = new('x', 20 * 1024);
        string responseJson = JsonSerializer.Serialize(new
        {
            error = new
            {
                code = 429,
                status = "RESOURCE_EXHAUSTED",
                message = providerMessage
            }
        });
        using ClientTestContext context = CreateRecordingClient(
            HttpStatusCode.TooManyRequests,
            responseJson);

        Func<Task> act = () => context.CreateInteractionAsync();

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = context.LogMessages.Last();
        logText.Should().Contain("provider code 429");
        logText.Should().Contain("provider status RESOURCE_EXHAUSTED");
        logText.Should().Contain(new string('x', 512));
        logText.Should().NotContain(new string('x', 513));
    }

    [Fact]
    public async Task CreateInteractionAsync_WithControlCharactersInMessage_LogsSingleLineMessage()
    {
        const string providerMessage = "First line\r\nSecond\tsegment\u0001done";
        string responseJson = JsonSerializer.Serialize(new
        {
            error = new
            {
                code = 400,
                status = "INVALID_ARGUMENT",
                message = providerMessage
            }
        });
        using ClientTestContext context = CreateRecordingClient(
            HttpStatusCode.BadRequest,
            responseJson);

        Func<Task> act = () => context.CreateInteractionAsync();

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = context.LogMessages.Last();
        logText.Should().Contain("provider message First line Second segment done.");
        logText.Should().NotContain("\r");
        logText.Should().NotContain("\n");
        logText.Should().NotContain("\t");
        logText.Should().NotContain("\u0001");
    }

    [Theory]
    [InlineData("", "Empty")]
    [InlineData("{malformed response", "Malformed")]
    public async Task CreateInteractionAsync_WithUnreadableProviderError_DoesNotThrowSecondaryException(
        string responseJson,
        string expectedBodyKind)
    {
        using ClientTestContext context = CreateRecordingClient(
            HttpStatusCode.BadRequest,
            responseJson);

        Func<Task> act = () => context.CreateInteractionAsync();

        await act.Should().ThrowAsync<ImageGenerationProviderException>()
            .WithMessage("The generation provider returned an error.");
        string logText = context.LogMessages.Last();
        logText.Should().Contain($"Error body {expectedBodyKind}");
    }

    [Fact]
    public async Task CreateInteractionAsync_WithInternalServerErrorThenSuccess_RetriesAndReturnsResponse()
    {
        using ClientTestContext context = CreateClient(
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.OK, """{"status":"completed"}"""));

        string responseJson = await context.CreateInteractionAsync();

        responseJson.Should().Be("""{"status":"completed"}""");
        context.Handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateInteractionAsync_WithPersistentInternalServerError_ThrowsAfterRetryAttempts()
    {
        using ClientTestContext context = CreateClient(
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""));

        Func<Task> act = () => context.CreateInteractionAsync();

        FluentAssertions.Specialized.ExceptionAssertions<ImageGenerationProviderException> assertions =
            await act.Should()
                .ThrowAsync<ImageGenerationProviderException>()
                .WithMessage("The generation provider returned an error.");
        assertions.Which.FailureKind.Should().Be(ImageGenerationProviderFailureKind.InternalError);
        context.Handler.Requests.Should().HaveCount(5);
    }

    private static ClientTestContext CreateClient(
        HttpStatusCode statusCode,
        string responseJson)
    {
        return CreateClient(
            statusCode,
            responseJson,
            NullLogger<GoogleInteractionsClient>.Instance,
            null);
    }

    private static ClientTestContext CreateRecordingClient(
        HttpStatusCode statusCode,
        string responseJson)
    {
        RecordingClientLogger logger = new();

        return CreateClient(
            statusCode,
            responseJson,
            logger,
            logger);
    }

    private static ClientTestContext CreateClient(
        HttpStatusCode statusCode,
        string responseJson,
        ILogger<GoogleInteractionsClient> logger,
        RecordingClientLogger? recordingLogger)
    {
        TestHttpMessageHandler handler = new(statusCode, responseJson);

        return CreateClient(handler, logger, recordingLogger);
    }

    private static ClientTestContext CreateClient(
        params (HttpStatusCode StatusCode, string ResponseJson)[] responses)
    {
        TestHttpMessageHandler handler = new(responses);

        return CreateClient(
            handler,
            NullLogger<GoogleInteractionsClient>.Instance,
            null);
    }

    private static ClientTestContext CreateClient(
        TestHttpMessageHandler handler,
        ILogger<GoogleInteractionsClient> logger,
        RecordingClientLogger? recordingLogger)
    {
        return new ClientTestContext(handler, logger, recordingLogger);
    }

    private static string CreateRequestJson()
    {
        return $$"""{"model":"{{ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().ProviderModelId}}"}""";
    }

    private sealed class ClientTestContext : IDisposable
    {
        public GoogleInteractionsClient Client { get; }
        public TestHttpMessageHandler Handler { get; }
        public IReadOnlyList<string> LogMessages =>
            _recordingLogger?.Messages ?? Array.Empty<string>();

        private readonly HttpClient _httpClient;
        private readonly RecordingClientLogger? _recordingLogger;

        public ClientTestContext(
            TestHttpMessageHandler handler,
            ILogger<GoogleInteractionsClient> logger,
            RecordingClientLogger? recordingLogger)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(logger);

            Handler = handler;
            _recordingLogger = recordingLogger;
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://example.invalid")
            };
            Client = new GoogleInteractionsClient(_httpClient, logger);
        }

        public Task<string> CreateInteractionAsync(
            string? requestJson = null,
            string providerCredential = TestGenerationCredentials.ProviderCredential)
        {
            string request = requestJson ?? CreateRequestJson();

            return Client.CreateInteractionAsync(
                request,
                providerCredential,
                CancellationToken.None);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string ResponseJson)> _responses;
        private readonly List<HttpRequestMessage> _requests = [];

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        public TestHttpMessageHandler(
            HttpStatusCode statusCode,
            string responseJson)
            : this(new (HttpStatusCode StatusCode, string ResponseJson)[]
            {
                (statusCode, responseJson)
            })
        {
        }

        public TestHttpMessageHandler(
            params (HttpStatusCode StatusCode, string ResponseJson)[] responses)
        {
            _responses = new Queue<(HttpStatusCode StatusCode, string ResponseJson)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No test HTTP response was configured.");
            }

            (HttpStatusCode statusCode, string responseJson) = _responses.Dequeue();
            _requests.Add(request);
            HttpResponseMessage response = new(statusCode)
            {
                Content = new StringContent(responseJson)
            };

            return Task.FromResult(response);
        }
    }

}
