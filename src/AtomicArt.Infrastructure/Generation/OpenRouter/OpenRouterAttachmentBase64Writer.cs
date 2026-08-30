using System.Buffers;
using System.Buffers.Text;

using AtomicArt.Application.Features.Generation.Interfaces;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal static class OpenRouterAttachmentBase64Writer
{
    private const int InputBufferSize = 49152;
    private const int OutputBufferSize = 65536;

    public static long GetEncodedLength(long byteLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteLength);

        return checked(((byteLength + 2L) / 3L) * 4L);
    }

    public static async Task WriteAsync(
        Stream destination,
        IGenerationAttachmentSource attachment,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(attachment);

        byte[] inputBuffer = ArrayPool<byte>.Shared.Rent(InputBufferSize);
        byte[] outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        long totalBytesRead = 0L;

        try
        {
            await using Stream source = await attachment.OpenReadAsync(ct).ConfigureAwait(false);

            while (true)
            {
                int bytesRead = await FillBufferAsync(source, inputBuffer, ct).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
                OperationStatus status = Base64.EncodeToUtf8(
                    inputBuffer.AsSpan(0, bytesRead),
                    outputBuffer,
                    out int consumed,
                    out int written);

                if (status != OperationStatus.Done || consumed != bytesRead)
                {
                    throw new InvalidOperationException(
                        "Attachment Base64 encoding did not consume the complete input block.");
                }

                await destination.WriteAsync(outputBuffer.AsMemory(0, written), ct).ConfigureAwait(false);
            }

            if (totalBytesRead != attachment.Metadata.ByteLength)
            {
                throw new InvalidDataException("Attachment length changed after request validation.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(inputBuffer);
            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }

    private static async Task<int> FillBufferAsync(Stream source, byte[] buffer, CancellationToken ct)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < InputBufferSize)
        {
            int bytesRead = await source
                .ReadAsync(buffer.AsMemory(totalBytesRead, InputBufferSize - totalBytesRead), ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }
}
