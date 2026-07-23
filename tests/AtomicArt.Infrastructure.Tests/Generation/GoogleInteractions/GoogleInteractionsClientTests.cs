using System.Net;
using System.Text;
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
    private const string CompletedResponseJson = """{"status":"completed"}""";
    private const string InternalServerErrorResponseJson = """{"error":"flex unavailable"}""";

    [Fact]
    public async Task CreateInteractionStreamAsync_WithProviderCredential_SendsApiKeyHeaderWithoutQueryString()
    {
        using ClientTestContext context = CreateClient(HttpStatusCode.OK, CompletedResponseJson);

        await CreateInteractionAndAssertCompletedAsync(context);

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
    public async Task CreateInteractionStreamAsync_WithProviderHttpError_MapsFailureKind(
        HttpStatusCode statusCode,
        ImageGenerationProviderFailureKind expectedFailureKind)
    {
        using ClientTestContext context = CreateClient(
            statusCode,
            """{"error":"secret detail"}""");

        await AssertProviderFailureAsync(context, expectedFailureKind);

        context.Handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateInteractionStreamAsync_WithStructuredProviderError_LogsUsefulDiagnostics()
    {
        const string responseJson =
            """{"error":{"code":403,"status":"PERMISSION_DENIED","message":"Requested model is unavailable in this region."}}""";

        (IReadOnlyList<string> logMessages, _) = await GetProviderFailureAsync(
            HttpStatusCode.Forbidden,
            responseJson);

        string logText = string.Join(Environment.NewLine, logMessages);
        logText.Should().Contain("provider code 403");
        logText.Should().Contain("provider status PERMISSION_DENIED");
        logText.Should().Contain("Requested model is unavailable in this region.");
        logText.Should().NotContain(responseJson);
    }

    [Fact]
    public async Task CreateInteractionStreamAsync_WithStringProviderErrorCode_DoesNotThrowSecondaryException()
    {
        const string responseJson =
            """{"error":{"code":"INVALID_ARGUMENT","status":"INVALID_ARGUMENT","message":"Request format is invalid."}}""";
        (
            IReadOnlyList<string> logMessages,
            ImageGenerationProviderException exception) = await GetProviderFailureAsync(
            HttpStatusCode.BadRequest,
            responseJson);

        exception.FailureKind.Should().Be(ImageGenerationProviderFailureKind.RequestRejected);
        string logText = logMessages.Last();
        logText.Should().Contain("provider status INVALID_ARGUMENT");
        logText.Should().Contain("provider message Request format is invalid.");
    }

    [Fact]
    public async Task CreateInteractionStreamAsync_WithLetterOnlyBase64InProviderMessage_LogsEncodedData()
    {
        const string base64Fragment = "QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFB";
        string responseJson =
            """{"error":{"code":400,"status":"INVALID_ARGUMENT","message":"Encoded value __BASE64__ was rejected."}}"""
                .Replace("__BASE64__", base64Fragment, StringComparison.Ordinal);

        (IReadOnlyList<string> logMessages, _) = await GetProviderFailureAsync(
            HttpStatusCode.BadRequest,
            responseJson);

        string logText = string.Join(Environment.NewLine, logMessages);
        logText.Should().Contain(base64Fragment);
        logText.Should().NotContain("[REDACTED");
    }

    [Fact]
    public async Task CreateInteractionStreamAsync_WithSensitiveProviderMessage_LogsProviderMessageWithoutRedaction()
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
        (IReadOnlyList<string> logMessages, _) = await GetProviderFailureAsync(
            HttpStatusCode.BadRequest,
            responseJson,
            requestJson,
            providerCredential);

        string logText = string.Join(Environment.NewLine, logMessages);
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
    public async Task CreateInteractionStreamAsync_WithMalformedRequestStructure_LogsProviderMessage()
    {
        const string responseJson =
            """{"error":{"code":400,"status":"INVALID_ARGUMENT","message":"Potentially echoed private input."}}""";

        (IReadOnlyList<string> logMessages, _) = await GetProviderFailureAsync(
            HttpStatusCode.BadRequest,
            responseJson,
            "{malformed request");

        string logText = string.Join(Environment.NewLine, logMessages);
        logText.Should().Contain("Potentially echoed private input.");
        logText.Should().NotContain("[REDACTED");
    }

    [Fact]
    public async Task CreateInteractionStreamAsync_WithLargeErrorBody_LogsFirst512MessageCharacters()
    {
        string providerMessage = new('x', 20 * 1024);
        string responseJson = CreateProviderErrorResponseJson(
            429,
            "RESOURCE_EXHAUSTED",
            providerMessage);

        string logText = await GetLastProviderFailureLogAsync(
            HttpStatusCode.TooManyRequests,
            responseJson);

        logText.Should().Contain("provider code 429");
        logText.Should().Contain("provider status RESOURCE_EXHAUSTED");
        logText.Should().Contain(new string('x', 512));
        logText.Should().NotContain(new string('x', 513));
    }

    [Fact]
    public async Task CreateInteractionStreamAsync_WithControlCharactersInMessage_LogsSingleLineMessage()
    {
        const string providerMessage = "First line\r\nSecond\tsegment\u0001done";
        string responseJson = CreateProviderErrorResponseJson(
            400,
            "INVALID_ARGUMENT",
            providerMessage);

        string logText = await GetLastProviderFailureLogAsync(
            HttpStatusCode.BadRequest,
            responseJson);

        logText.Should().Contain("provider message First line Second segment done.");
        logText.Should().NotContain("\r");
        logText.Should().NotContain("\n");
        logText.Should().NotContain("\t");
        logText.Should().NotContain("\u0001");
    }

    [Theory]
    [InlineData("", "Empty")]
    [InlineData("{malformed response", "Malformed")]
    public async Task CreateInteractionStreamAsync_WithUnreadableProviderError_DoesNotThrowSecondaryException(
        string responseJson,
        string expectedBodyKind)
    {
        (
            IReadOnlyList<string> logMessages,
            ImageGenerationProviderException exception) = await GetProviderFailureAsync(
            HttpStatusCode.BadRequest,
            responseJson);

        exception.Message.Should().Be("The generation provider returned an error.");
        string logText = logMessages.Last();
        logText.Should().Contain($"Error body {expectedBodyKind}");
    }

    [Fact]
    public async Task CreateInteractionStreamAsync_WithInternalServerErrorThenSuccess_DoesNotRetryOnServer()
    {
        using ClientTestContext context = CreateInternalServerErrorClient(
            2,
            (HttpStatusCode.OK, CompletedResponseJson));
        Func<Task> act = () => context.CreateInteractionStreamAsync();

        ImageGenerationProviderException exception = (await act.Should()
                .ThrowAsync<ImageGenerationProviderException>())
            .Which;

        exception.FailureKind.Should().Be(
            ImageGenerationProviderFailureKind.InternalError);
        exception.Retryable.Should().BeTrue();
        context.Handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateInteractionStreamAsync_WithPersistentInternalServerError_ReturnsRetryClassificationWithoutRetry()
    {
        using ClientTestContext context = CreateInternalServerErrorClient(5);
        Func<Task> act = () => context.CreateInteractionStreamAsync();

        ImageGenerationProviderException exception = (await act.Should()
                .ThrowAsync<ImageGenerationProviderException>())
            .Which;

        exception.FailureKind.Should().Be(
            ImageGenerationProviderFailureKind.InternalError);
        exception.Retryable.Should().BeTrue();
        context.Handler.Requests.Should().ContainSingle();
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

    private static ClientTestContext CreateInternalServerErrorClient(
        int errorCount,
        params (HttpStatusCode StatusCode, string ResponseJson)[] trailingResponses)
    {
        List<(HttpStatusCode StatusCode, string ResponseJson)> responses = Enumerable
            .Repeat(
                (HttpStatusCode.InternalServerError, InternalServerErrorResponseJson),
                errorCount)
            .ToList();
        responses.AddRange(trailingResponses);

        return CreateClient(responses.ToArray());
    }

    private static string CreateProviderErrorResponseJson(
        int code,
        string status,
        string message)
    {
        return JsonSerializer.Serialize(new
        {
            error = new
            {
                code,
                status,
                message
            }
        });
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

    private static async Task CreateInteractionAndAssertCompletedAsync(
        ClientTestContext context)
    {
        string responseJson = await context.CreateInteractionStreamAsync().ConfigureAwait(false);

        responseJson.Should().Be(CompletedResponseJson);
    }

    private static async Task<string> GetLastProviderFailureLogAsync(
        HttpStatusCode statusCode,
        string responseJson)
    {
        (IReadOnlyList<string> logMessages, _) = await GetProviderFailureAsync(
            statusCode,
            responseJson).ConfigureAwait(false);

        return logMessages.Last();
    }

    private static async Task<(
        IReadOnlyList<string> LogMessages,
        ImageGenerationProviderException Exception)> GetProviderFailureAsync(
            HttpStatusCode statusCode,
            string responseJson,
            string? requestJson = null,
            string providerCredential = TestGenerationCredentials.ProviderCredential)
    {
        using ClientTestContext context = CreateRecordingClient(statusCode, responseJson);
        Func<Task> act = () => context.CreateInteractionStreamAsync(requestJson, providerCredential);

        FluentAssertions.Specialized.ExceptionAssertions<ImageGenerationProviderException> assertions =
            await act.Should()
                .ThrowAsync<ImageGenerationProviderException>()
                .ConfigureAwait(false);
        IReadOnlyList<string> logMessages = context.LogMessages.ToList();

        return (logMessages, assertions.Which);
    }

    private static async Task AssertProviderFailureAsync(
        ClientTestContext context,
        ImageGenerationProviderFailureKind expectedFailureKind)
    {
        Func<Task> act = () => context.CreateInteractionStreamAsync();

        FluentAssertions.Specialized.ExceptionAssertions<ImageGenerationProviderException> assertions =
            await act.Should()
                .ThrowAsync<ImageGenerationProviderException>()
                .WithMessage("The generation provider returned an error.")
                .ConfigureAwait(false);
        assertions.Which.FailureKind.Should().Be(expectedFailureKind);
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

        public async Task<string> CreateInteractionStreamAsync(
            string? requestJson = null,
            string providerCredential = TestGenerationCredentials.ProviderCredential)
        {
            string request = requestJson ?? CreateRequestJson();
            using StringContent content = new(
                request,
                Encoding.UTF8,
                "application/json");
            await using GoogleInteractionsStreamingResponse response =
                await Client.CreateInteractionStreamAsync(
                        content,
                        providerCredential,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            using StreamReader reader = new(
                response.Content,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);

            return await reader.ReadToEndAsync(CancellationToken.None)
                .ConfigureAwait(false);
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
