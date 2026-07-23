namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleInteractionsStreamingResponse : IAsyncDisposable
{
    public HttpResponseMessage Response { get; }
    public Stream Content { get; }

    public GoogleInteractionsStreamingResponse(
        HttpResponseMessage response,
        Stream content)
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
