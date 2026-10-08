using System.Net;

using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

using AtomicArt.Desktop.Services.OpenRouter;

namespace AtomicArt.Desktop.Tests.Services.OpenRouter;

public sealed class OpenRouterBalanceApiClientTests
{
    [Theory]
    [InlineData(100.5, 25.75, 74.75)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 2, -1)]
    public async Task GetBalanceAsync_WithValidCredits_ReturnsAccountBalance(
        decimal credits, decimal usage, decimal expected)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new
        {
            data = new { total_credits = credits, total_usage = usage }
        });
        using HttpClient httpClient = CreateHttpClient(json);
        OpenRouterBalanceApiClient client = new(httpClient);

        decimal balance = await client.GetBalanceAsync("management-key", CancellationToken.None);

        balance.Should().Be(expected);
    }

    [Fact]
    public async Task GetBalanceAsync_WithManagementKey_UsesFixedHttpsEndpointAndBearerHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        using HttpClient httpClient = CreateHttpClient(
            """{"data":{"total_credits":10,"total_usage":2}}""",
            captureRequest: request => capturedRequest = request);
        httpClient.BaseAddress = new Uri("https://unrelated.example/");
        OpenRouterBalanceApiClient client = new(httpClient);

        await client.GetBalanceAsync(" management-key ", CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest?.Method.Should().Be(HttpMethod.Get);
        capturedRequest?.RequestUri.Should().Be(new Uri("https://openrouter.ai/api/v1/credits"));
        capturedRequest?.Headers.Authorization?.Scheme.Should().Be("Bearer");
        capturedRequest?.Headers.Authorization?.Parameter.Should().Be("management-key");
        httpClient.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{\"total_credits\":1}}")]
    [InlineData("{\"data\":{\"total_credits\":\"100\",\"total_usage\":1}}")]
    [InlineData("{\"data\":{\"total_credits\":-1,\"total_usage\":0}}")]
    public async Task GetBalanceAsync_WithInvalidAmounts_RejectsResponse(string json)
    {
        using HttpClient httpClient = CreateHttpClient(json);
        OpenRouterBalanceApiClient client = new(httpClient);

        Func<Task> action = async () => await client.GetBalanceAsync("management-key", CancellationToken.None);

        await action.Should().ThrowAsync<InvalidDataException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetBalanceAsync_WithProviderError_ThrowsWithoutExposingResponseBody(HttpStatusCode status)
    {
        const string ResponseBody = "untrusted provider error";
        using HttpClient httpClient = CreateHttpClient(ResponseBody, status);
        OpenRouterBalanceApiClient client = new(httpClient);

        Func<Task> action = async () => await client.GetBalanceAsync("management-key", CancellationToken.None);

        HttpRequestException exception = (await action.Should().ThrowAsync<HttpRequestException>()).Which;
        exception.StatusCode.Should().Be(status);
        exception.Message.Should().NotContain(ResponseBody);
        exception.Message.Should().NotContain("management-key");
    }

    [Fact]
    public async Task GetBalanceAsync_WithOversizedResponse_RejectsBeforeParsing()
    {
        const int OversizedResponseCharacters = 32 * 1024;
        using HttpClient httpClient = CreateHttpClient(new string(' ', OversizedResponseCharacters));
        OpenRouterBalanceApiClient client = new(httpClient);

        Func<Task> action = async () => await client.GetBalanceAsync("management-key", CancellationToken.None);

        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetBalanceAsync_WithCanceledRequest_DoesNotSendRequest()
    {
        using HttpClient httpClient = CreateHttpClient("{}");
        OpenRouterBalanceApiClient client = new(httpClient);
        using CancellationTokenSource cancellationSource = new();
        cancellationSource.Cancel();

        Func<Task> action = async () => await client.GetBalanceAsync("management-key", cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static HttpClient CreateHttpClient(
        string json,
        HttpStatusCode status = HttpStatusCode.OK,
        Action<HttpRequestMessage>? captureRequest = null)
    {
        Mock<HttpMessageHandler> handler = new();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => captureRequest?.Invoke(request))
            .ReturnsAsync(() => new HttpResponseMessage(status)
            {
                Content = new StringContent(json)
            });
        HttpClient client = new(handler.Object);
        OpenRouterBalanceApiClient.Configure(client);

        return client;
    }
}
