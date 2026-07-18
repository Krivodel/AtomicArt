namespace AtomicArt.Desktop.Services.Paths;

internal static class SafeFileName
{
    public static bool IsValid(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        string normalizedFileName = Path.GetFileName(fileName);

        return string.Equals(normalizedFileName, fileName, StringComparison.Ordinal)
            && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
}
