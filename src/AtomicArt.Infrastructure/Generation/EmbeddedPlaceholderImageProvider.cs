using System.Reflection;

namespace AtomicArt.Infrastructure.Generation;

internal sealed class EmbeddedPlaceholderImageProvider : IPlaceholderImageProvider
{
    private const string JpegContentType = "image/jpeg";
    private const long MaxPlaceholderImageBytes = 128L * 1024L * 1024L;
    private const string PlaceholdersResourceSegment = ".Placeholders.";
    private const string PngContentType = "image/png";
    private const string WebpContentType = "image/webp";

    private static readonly Assembly ResourceAssembly = typeof(EmbeddedPlaceholderImageProvider).Assembly;
    private static readonly IReadOnlyDictionary<string, string> ContentTypesByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpeg"] = JpegContentType,
            [".jpg"] = JpegContentType,
            [".png"] = PngContentType,
            [".webp"] = WebpContentType
        };
    private static readonly string[] ResourceNames = ResourceAssembly
        .GetManifestResourceNames()
        .Where(IsPlaceholderImageResource)
        .OrderBy(resourceName => resourceName, StringComparer.Ordinal)
        .ToArray();

    public async Task<PlaceholderImage> GetNextAsync(
        string modelId,
        int itemIndex,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentOutOfRangeException.ThrowIfNegative(itemIndex);
        ct.ThrowIfCancellationRequested();

        if (ResourceNames.Length == 0)
        {
            throw new InvalidOperationException("No embedded placeholder images were found.");
        }

        string resourceName = ResourceNames[itemIndex % ResourceNames.Length];
        await using Stream resourceStream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The embedded placeholder image was not found.");
        byte[] content = await ReadResourceContentAsync(resourceStream, resourceName, ct)
            .ConfigureAwait(false);

        return new PlaceholderImage(
            GetContentType(resourceName),
            content);
    }

    internal static async Task<byte[]> ReadResourceContentAsync(
        Stream resourceStream,
        string resourceName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resourceStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ct.ThrowIfCancellationRequested();

        if (resourceStream is { CanSeek: true, Length: > MaxPlaceholderImageBytes })
        {
            throw CreateResourceTooLargeException(resourceName);
        }

        return await BoundedStreamReader
            .ReadToEndAsync(
                resourceStream,
                MaxPlaceholderImageBytes,
                () => CreateResourceTooLargeException(resourceName),
                ct)
            .ConfigureAwait(false);
    }

    private static bool IsPlaceholderImageResource(string resourceName)
    {
        return resourceName.Contains(PlaceholdersResourceSegment, StringComparison.Ordinal)
            && ContentTypesByExtension.ContainsKey(Path.GetExtension(resourceName));
    }

    private static string GetContentType(string resourceName)
    {
        string extension = Path.GetExtension(resourceName);

        if (ContentTypesByExtension.TryGetValue(extension, out string? contentType))
        {
            return contentType;
        }

        throw new InvalidOperationException("The embedded placeholder image type is not supported.");
    }

    private static InvalidOperationException CreateResourceTooLargeException(string resourceName)
    {
        return new InvalidOperationException(
            $"The embedded placeholder image '{resourceName}' exceeds 128 MB.");
    }
}
