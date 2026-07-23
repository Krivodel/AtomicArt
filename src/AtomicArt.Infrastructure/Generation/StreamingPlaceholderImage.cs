namespace AtomicArt.Infrastructure.Generation;

internal sealed class StreamingPlaceholderImage : IAsyncDisposable
{
    public string ContentType { get; }
    public long ContentLength { get; }
    public Stream Content { get; }

    public StreamingPlaceholderImage(
        string contentType,
        long contentLength,
        Stream content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfLessThan(contentLength, 1L);

        ContentType = contentType;
        ContentLength = contentLength;
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public ValueTask DisposeAsync()
    {
        return Content.DisposeAsync();
    }
}
