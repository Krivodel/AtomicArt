namespace AtomicArt.Desktop.Services;

internal sealed class LimitedMemoryStream : MemoryStream
{
    private readonly long _maxBytes;

    internal LimitedMemoryStream(long maxBytes)
    {
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "Stream size limit must be positive.");
        }

        _maxBytes = maxBytes;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureCanWrite(count);

        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureCanWrite(buffer.Length);

        base.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        EnsureCanWrite(count);

        return base.WriteAsync(buffer, offset, count, ct);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        EnsureCanWrite(buffer.Length);

        return base.WriteAsync(buffer, ct);
    }

    private void EnsureCanWrite(int count)
    {
        if (Position + count > _maxBytes)
        {
            throw new InvalidOperationException("Stream size limit exceeded.");
        }
    }
}
