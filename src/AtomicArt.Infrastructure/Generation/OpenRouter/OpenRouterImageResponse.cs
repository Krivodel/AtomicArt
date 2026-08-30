namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterImageResponse : IAsyncDisposable
{
    public HttpResponseMessage Response { get; }
    public Stream Content { get; }

    public OpenRouterImageResponse(HttpResponseMessage response, Stream content)
    {
        Response = response ?? throw new ArgumentNullException(nameof(response));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync().ConfigureAwait(false);
        Response.Dispose();
    }
}
