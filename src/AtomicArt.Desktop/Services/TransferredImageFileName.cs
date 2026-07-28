namespace AtomicArt.Desktop.Services;

internal static class TransferredImageFileName
{
    public static string Sanitize(
        string? candidate,
        string fallback,
        int maximumCharacters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCharacters, 1);

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

        return sanitizedName.Length <= maximumCharacters
            ? sanitizedName
            : sanitizedName[..maximumCharacters];
    }
}
