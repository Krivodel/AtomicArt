namespace AtomicArt.Infrastructure.Generation;

internal static class BoundedStreamReader
{
    internal const int BufferSize = 81920;

    internal static async Task<byte[]> ReadToEndAsync(
        Stream stream,
        long maxBytes,
        Func<Exception> createTooLargeException,
        CancellationToken ct)
    {
        byte[] buffer = new byte[BufferSize];
        long totalBytesRead = 0L;
        using MemoryStream memoryStream = new();

        while (true)
        {
            int bytesRead = await stream
                .ReadAsync(buffer.AsMemory(0, buffer.Length), ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;

            if (totalBytesRead > maxBytes)
            {
                throw createTooLargeException();
            }

            await memoryStream
                .WriteAsync(buffer.AsMemory(0, bytesRead), ct)
                .ConfigureAwait(false);
        }

        return memoryStream.ToArray();
    }
}
