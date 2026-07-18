using System.Reflection;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation;

internal sealed class EmbeddedPlaceholderImageProvider : PlaceholderImageProvider
{
    internal const int MaxPlaceholderImageMegabytes = 128;
    internal const long MaxPlaceholderImageBytes =
        MaxPlaceholderImageMegabytes * 1024L * 1024L;

    private const string PlaceholdersResourceSegment = ".Placeholders.";

    private static readonly Assembly ResourceAssembly = typeof(EmbeddedPlaceholderImageProvider).Assembly;
    private static readonly string[] SupportedContentTypes =
    [
        GenerationImageContentTypes.Jpeg,
        GenerationImageContentTypes.Png,
        GenerationImageContentTypes.Webp
    ];
    private static readonly IReadOnlyDictionary<string, string> ContentTypesByExtension =
        GenerationImageFileFormats.All
            .Where(format => SupportedContentTypes.Contains(
                format.ContentType,
                StringComparer.Ordinal))
            .SelectMany(
                format => format.Extensions,
                (format, extension) => new KeyValuePair<string, string>(
                    extension,
                    format.ContentType))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
    private static readonly string[] ResourceNames = ResourceAssembly
        .GetManifestResourceNames()
        .Where(IsPlaceholderImageResource)
        .OrderBy(resourceName => resourceName, StringComparer.Ordinal)
        .ToArray();

    protected override async Task<PlaceholderImage> GetNextCoreAsync(
        string modelId,
        int itemIndex,
        CancellationToken ct)
    {
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

    internal static string GetContentType(string resourceName)
    {
        string extension = Path.GetExtension(resourceName);

        if (ContentTypesByExtension.TryGetValue(extension, out string? contentType))
        {
            return contentType;
        }

        throw new InvalidOperationException("The embedded placeholder image type is not supported.");
    }

    private static bool IsPlaceholderImageResource(string resourceName)
    {
        return resourceName.Contains(PlaceholdersResourceSegment, StringComparison.Ordinal)
            && ContentTypesByExtension.ContainsKey(Path.GetExtension(resourceName));
    }

    private static InvalidOperationException CreateResourceTooLargeException(string resourceName)
    {
        return new InvalidOperationException(
            $"The embedded placeholder image '{resourceName}' exceeds {MaxPlaceholderImageMegabytes} MB.");
    }
}
