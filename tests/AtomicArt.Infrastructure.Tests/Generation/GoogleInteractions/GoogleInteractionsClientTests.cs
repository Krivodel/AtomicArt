using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation.GoogleInteractions;

public sealed class GoogleInteractionsClientTests
{
    [Fact]
    public async Task CreateInteractionAsync_WithProviderCredential_SendsApiKeyHeaderWithoutQueryString()
    {
        TestHttpMessageHandler handler = new(HttpStatusCode.OK, """{"status":"completed"}""");
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        GoogleInteractionsClient client = new(
            httpClient,
            NullLogger<GoogleInteractionsClient>.Instance);

        string responseJson = await client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        responseJson.Should().Be("""{"status":"completed"}""");
        HttpRequestMessage request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri.Should().NotBeNull();
        Uri requestUri = request.RequestUri ?? throw new InvalidOperationException("Request URI is missing.");
        requestUri.AbsolutePath.Should().Be("/v1beta/interactions");
        requestUri.Query.Should().BeEmpty();
        requestUri.ToString().Should().NotContain("test-provider-key");
        request.Headers.TryGetValues("X-Goog-Api-Key", out IEnumerable<string>? values).Should().BeTrue();
        values.Should().BeEquivalentTo(new[] { "test-provider-key" });
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
        TestHttpMessageHandler handler = new(statusCode, """{"error":"secret detail"}""");
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        GoogleInteractionsClient client = new(
            httpClient,
            NullLogger<GoogleInteractionsClient>.Instance);

        Func<Task> act = () => client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        FluentAssertions.Specialized.ExceptionAssertions<ImageGenerationProviderException> assertions =
            await act.Should()
                .ThrowAsync<ImageGenerationProviderException>()
                .WithMessage("The generation provider returned an error.");
        assertions.Which.FailureKind.Should().Be(expectedFailureKind);
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateInteractionAsync_WithStructuredProviderError_LogsUsefulDiagnostics()
    {
        const string responseJson =
            """{"error":{"code":403,"status":"PERMISSION_DENIED","message":"Requested model is unavailable in this region."}}""";
        TestHttpMessageHandler handler = new(HttpStatusCode.Forbidden, responseJson);
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        RecordingClientLogger logger = new();
        GoogleInteractionsClient client = new(httpClient, logger);

        Func<Task> act = () => client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = string.Join(Environment.NewLine, logger.Messages);
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
        TestHttpMessageHandler handler = new(HttpStatusCode.BadRequest, responseJson);
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        RecordingClientLogger logger = new();
        GoogleInteractionsClient client = new(httpClient, logger);

        Func<Task> act = () => client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        FluentAssertions.Specialized.ExceptionAssertions<ImageGenerationProviderException> assertions =
            await act.Should()
            .ThrowAsync<ImageGenerationProviderException>()
            .WithMessage("The generation provider returned an error.");
        assertions.Which.FailureKind.Should().Be(ImageGenerationProviderFailureKind.RequestRejected);
        string logText = logger.Messages.Last();
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
        TestHttpMessageHandler handler = new(HttpStatusCode.BadRequest, responseJson);
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        RecordingClientLogger logger = new();
        GoogleInteractionsClient client = new(httpClient, logger);

        Func<Task> act = () => client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = string.Join(Environment.NewLine, logger.Messages);
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
        TestHttpMessageHandler handler = new(HttpStatusCode.BadRequest, responseJson);
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        RecordingClientLogger logger = new();
        GoogleInteractionsClient client = new(httpClient, logger);

        Func<Task> act = () => client.CreateInteractionAsync(
            requestJson,
            providerCredential,
            CancellationToken.None);

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = string.Join(Environment.NewLine, logger.Messages);
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
        TestHttpMessageHandler handler = new(HttpStatusCode.BadRequest, responseJson);
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        RecordingClientLogger logger = new();
        GoogleInteractionsClient client = new(httpClient, logger);

        Func<Task> act = () => client.CreateInteractionAsync(
            "{malformed request",
            "test-provider-key",
            CancellationToken.None);

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = string.Join(Environment.NewLine, logger.Messages);
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
        TestHttpMessageHandler handler = new(HttpStatusCode.TooManyRequests, responseJson);
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        RecordingClientLogger logger = new();
        GoogleInteractionsClient client = new(httpClient, logger);

        Func<Task> act = () => client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = logger.Messages.Last();
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
        TestHttpMessageHandler handler = new(HttpStatusCode.BadRequest, responseJson);
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        RecordingClientLogger logger = new();
        GoogleInteractionsClient client = new(httpClient, logger);

        Func<Task> act = () => client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        await act.Should().ThrowAsync<ImageGenerationProviderException>();
        string logText = logger.Messages.Last();
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
        TestHttpMessageHandler handler = new(HttpStatusCode.BadRequest, responseJson);
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        RecordingClientLogger logger = new();
        GoogleInteractionsClient client = new(httpClient, logger);

        Func<Task> act = () => client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        await act.Should().ThrowAsync<ImageGenerationProviderException>()
            .WithMessage("The generation provider returned an error.");
        string logText = logger.Messages.Last();
        logText.Should().Contain($"Error body {expectedBodyKind}");
    }

    [Fact]
    public async Task CreateInteractionAsync_WithInternalServerErrorThenSuccess_RetriesAndReturnsResponse()
    {
        TestHttpMessageHandler handler = new(
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.OK, """{"status":"completed"}"""));
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        GoogleInteractionsClient client = new(
            httpClient,
            NullLogger<GoogleInteractionsClient>.Instance);

        string responseJson = await client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        responseJson.Should().Be("""{"status":"completed"}""");
        handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateInteractionAsync_WithPersistentInternalServerError_ThrowsAfterRetryAttempts()
    {
        TestHttpMessageHandler handler = new(
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""),
            (HttpStatusCode.InternalServerError, """{"error":"flex unavailable"}"""));
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://example.invalid")
        };
        GoogleInteractionsClient client = new(
            httpClient,
            NullLogger<GoogleInteractionsClient>.Instance);

        Func<Task> act = () => client.CreateInteractionAsync(
            CreateRequestJson(),
            "test-provider-key",
            CancellationToken.None);

        FluentAssertions.Specialized.ExceptionAssertions<ImageGenerationProviderException> assertions =
            await act.Should()
                .ThrowAsync<ImageGenerationProviderException>()
                .WithMessage("The generation provider returned an error.");
        assertions.Which.FailureKind.Should().Be(ImageGenerationProviderFailureKind.InternalError);
        handler.Requests.Should().HaveCount(5);
    }

    private static string CreateRequestJson()
    {
        return $$"""{"model":"{{ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().ProviderModelId}}"}""";
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

    private sealed class RecordingClientLogger : ILogger<GoogleInteractionsClient>
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            _messages.Add(formatter(state, exception));
        }

        private sealed class NullDisposable : IDisposable
        {
            public static NullDisposable Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
