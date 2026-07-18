namespace AtomicArt.Application.Features.Generation.Services;

internal static class AttachedImageFileNamePolicy
{
    private static readonly char[] DisallowedFileNameChars =
    [
        '/',
        '\\',
        ':',
        '*',
        '?',
        '"',
        '<',
        '>',
        '|'
    ];

    public static bool IsValid(string? fileName)
    {
        return Normalize(fileName) is not null;
    }

    public static string? Normalize(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string normalizedFileName = fileName.Trim();

        if (!string.Equals(Path.GetFileName(normalizedFileName), normalizedFileName, StringComparison.Ordinal))
        {
            return null;
        }

        if (normalizedFileName is "." or "..")
        {
            return null;
        }

        if (normalizedFileName.IndexOfAny(DisallowedFileNameChars) >= 0)
        {
            return null;
        }

        if (normalizedFileName.Any(char.IsControl))
        {
            return null;
        }

        return normalizedFileName;
    }
}
