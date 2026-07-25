using Avalonia.Input;

using AtomicArt.Contracts.Generation;
using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services;

internal static class ImageDataTransferFormats
{
    public static IReadOnlyList<ImageDataTransferFormatDescriptor> EncodedImages { get; } =
        CreateEncodedImageFormats();

    public static bool ContainsEncodedImage(IDataTransfer dataTransfer)
    {
        ArgumentNullException.ThrowIfNull(dataTransfer);

        return EncodedImages.Any(descriptor =>
                descriptor.Formats.Any(dataTransfer.Contains))
            || dataTransfer.Formats.Any(format =>
                TryNormalizeImageMimeType(
                    format.Identifier,
                    out string _));
    }

    public static string ResolveExtension(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return GenerationImageFileFormats.PngExtension;
        }

        ImageDataTransferFormatDescriptor? descriptor = EncodedImages.FirstOrDefault(
            candidate => string.Equals(
                candidate.ContentType,
                contentType.Trim(),
                StringComparison.OrdinalIgnoreCase));

        return descriptor?.Extension ?? GenerationImageFileFormats.PngExtension;
    }

    private static IReadOnlyList<ImageDataTransferFormatDescriptor> CreateEncodedImageFormats()
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> platformAliases =
            CreatePlatformAliases();

        List<ImageDataTransferFormatDescriptor> formats = GenerationImageFileFormats.All
            .Select(format => new ImageDataTransferFormatDescriptor(
                format.ContentType,
                format.Extensions[0],
                CreateDataFormats(format.ContentType, platformAliases)))
            .ToList();
        formats.AddRange(CreateConvertibleImageFormats());

        return formats;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CreatePlatformAliases()
    {
        return new Dictionary<string, IReadOnlyList<string>>(
            StringComparer.OrdinalIgnoreCase)
        {
            [GenerationImageContentTypes.Gif] =
            [
                "public.gif"
            ],
            [GenerationImageContentTypes.Heic] =
            [
                "public.heic"
            ],
            [GenerationImageContentTypes.Heif] =
            [
                "public.heif"
            ],
            [GenerationImageContentTypes.Jpeg] =
            [
                "image/jpg",
                "JFIF",
                "public.jpeg"
            ],
            [GenerationImageContentTypes.Png] =
            [
                PicaClipboardFormats.WindowsPng,
                PicaClipboardFormats.MacOsPng
            ],
            [GenerationImageContentTypes.Webp] =
            [
                "org.webmproject.webp"
            ]
        };
    }

    private static IReadOnlyList<DataFormat<byte[]>> CreateDataFormats(
        string contentType,
        IReadOnlyDictionary<string, IReadOnlyList<string>> platformAliases)
    {
        HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase)
        {
            contentType
        };

        if (platformAliases.TryGetValue(
                contentType,
                out IReadOnlyList<string>? aliases))
        {
            identifiers.UnionWith(aliases);
        }

        return identifiers
            .Select(DataFormat.CreateBytesPlatformFormat)
            .ToList();
    }

    private static IReadOnlyList<ImageDataTransferFormatDescriptor>
        CreateConvertibleImageFormats()
    {
        return
        [
            CreateConvertibleImageFormat(
                "image/avif",
                ".avif",
                ["public.avif"]),
            CreateConvertibleImageFormat(
                "image/bmp",
                ".bmp",
                ["com.microsoft.bmp", "public.bmp"]),
            CreateConvertibleImageFormat(
                "image/jxl",
                ".jxl",
                ["public.jpeg-xl"]),
            CreateConvertibleImageFormat(
                "image/svg+xml",
                ".svg",
                ["public.svg-image"]),
            CreateConvertibleImageFormat(
                "image/tiff",
                ".tiff",
                ["public.tiff"]),
            CreateConvertibleImageFormat(
                "image/x-icon",
                ".ico",
                ["com.microsoft.ico", "image/vnd.microsoft.icon"])
        ];
    }

    private static ImageDataTransferFormatDescriptor CreateConvertibleImageFormat(
        string contentType,
        string extension,
        IReadOnlyList<string> aliases)
    {
        IReadOnlyList<DataFormat<byte[]>> formats = aliases
            .Prepend(contentType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(DataFormat.CreateBytesPlatformFormat)
            .ToList();

        return new ImageDataTransferFormatDescriptor(
            contentType,
            extension,
            formats);
    }

    internal static bool TryNormalizeImageMimeType(
        string identifier,
        out string normalizedContentType)
    {
        normalizedContentType = identifier.Trim();

        if (!normalizedContentType.StartsWith(
                "image/",
                StringComparison.OrdinalIgnoreCase)
            || normalizedContentType.Length <= "image/".Length
            || normalizedContentType.Length > 127)
        {
            normalizedContentType = string.Empty;
            return false;
        }

        ReadOnlySpan<char> subtype = normalizedContentType.AsSpan("image/".Length);
        bool isValid = true;

        foreach (char character in subtype)
        {
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not ('.' or '+' or '-' or '_'))
            {
                isValid = false;
                break;
            }
        }

        if (!isValid)
        {
            normalizedContentType = string.Empty;
        }

        return isValid;
    }
}

internal sealed record ImageDataTransferFormatDescriptor(
    string ContentType,
    string Extension,
    IReadOnlyList<DataFormat<byte[]>> Formats);
