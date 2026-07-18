namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationImageFileNamePolicy
{
    private const string FileNamePrefix = "generation";

    public string BuildFileName(
        Guid batchId,
        Guid itemId,
        string extension)
    {
        string normalizedExtension = NormalizeExtension(extension);

        if (batchId == Guid.Empty)
        {
            throw new ArgumentException("Generation batch id must not be empty.", nameof(batchId));
        }

        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("Generation item id must not be empty.", nameof(itemId));
        }

        return $"{FileNamePrefix}-{batchId:N}-{itemId:N}{normalizedExtension}";
    }

    public bool IsFileNameForItem(string fileName, Guid itemId)
    {
        if (string.IsNullOrWhiteSpace(fileName) || itemId == Guid.Empty)
        {
            return false;
        }

        string normalizedFileName = Path.GetFileName(fileName);

        if (!string.Equals(fileName, normalizedFileName, StringComparison.Ordinal))
        {
            return false;
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(normalizedFileName);
        string[] segments = fileNameWithoutExtension.Split('-');

        return segments.Length == 3
            && string.Equals(segments[0], FileNamePrefix, StringComparison.Ordinal)
            && Guid.TryParseExact(segments[1], "N", out Guid _)
            && Guid.TryParseExact(segments[2], "N", out Guid parsedItemId)
            && parsedItemId == itemId
            && !string.IsNullOrWhiteSpace(Path.GetExtension(normalizedFileName));
    }

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        string trimmedExtension = extension.Trim();

        if (trimmedExtension == "."
            || trimmedExtension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmedExtension.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || trimmedExtension.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Generation image extension must be a file extension.", nameof(extension));
        }

        if (trimmedExtension.StartsWith(".", StringComparison.Ordinal))
        {
            return trimmedExtension;
        }

        return $".{trimmedExtension}";
    }
}
