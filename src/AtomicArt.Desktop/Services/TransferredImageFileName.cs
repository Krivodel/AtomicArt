namespace AtomicArt.Desktop.Services;

internal static class TransferredImageFileName
{
    private const int MaximumLength = 128;

    public static string Sanitize(string? candidate, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return fallback;
        }

        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitizedName = string.Concat(candidate
            .Trim()
            .Select(character =>
                invalidCharacters.Contains(character)
                || char.IsControl(character)
                    ? '_'
                    : character));

        if (string.IsNullOrWhiteSpace(sanitizedName)
            || sanitizedName is "." or "..")
        {
            return fallback;
        }

        return sanitizedName.Length <= MaximumLength
            ? sanitizedName
            : sanitizedName[..MaximumLength];
    }
}
