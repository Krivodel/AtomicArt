namespace AtomicArt.Desktop.Services;

internal static class LimitedContentReader
{
    public static async Task<byte[]> ReadAsync(
        Stream input,
        int maxBytes,
        string tooLargeMessage,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(tooLargeMessage);

        await using LimitedMemoryStream output = new(maxBytes);

        try
        {
            await input.CopyToAsync(output, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException(tooLargeMessage, ex);
        }

        return output.ToArray();
    }
}
