using System.Net;
using System.Text;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    public HttpMethod? RequestMethod { get; private set; }
    public Uri? RequestUri { get; private set; }
    public string RequestBody { get; private set; } = string.Empty;
    public string? ProviderCredential { get; private set; }
    public IReadOnlyList<Uri> RequestUris => _requestUris;

    private readonly string _responseJson;
    private readonly HttpStatusCode _statusCode;
    private readonly List<Uri> _requestUris = [];

    public CapturingHttpMessageHandler(
        string responseJson,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);

        _responseJson = responseJson;
        _statusCode = statusCode;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestMethod = request.Method;
        RequestUri = request.RequestUri;

        if (RequestUri is not null)
        {
            _requestUris.Add(RequestUri);
        }

        ProviderCredential = request.Headers.TryGetValues(
            GenerationApiRoutes.ProviderApiKeyHeaderName,
            out IEnumerable<string>? values)
            ? values.SingleOrDefault()
            : null;

        if (request.Content is not null)
        {
            RequestBody = await request.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        };
    }
}
