using Avalonia.Platform.Storage;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

internal static class AttachmentFilePickerFileTypes
{
    public static FilePickerFileType Images { get; } = CreateImages();

    private static FilePickerFileType CreateImages()
    {
        FilePickerFileType imageTypes = FilePickerFileTypes.ImageAll;
        GenerationImageFileFormatDescriptor gifFormat =
            GenerationImageFileFormats.All.Single(format => string.Equals(
                format.ContentType,
                GenerationImageContentTypes.Gif,
                StringComparison.OrdinalIgnoreCase));
        HashSet<string> gifPatterns = gifFormat.Extensions
            .Select(extension => $"*{extension}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new FilePickerFileType(imageTypes.Name)
        {
            Patterns = ExcludeValues(
                imageTypes.Patterns,
                gifPatterns.Contains),
            MimeTypes = ExcludeValues(
                imageTypes.MimeTypes,
                value => string.Equals(
                    value,
                    GenerationImageContentTypes.Gif,
                    StringComparison.OrdinalIgnoreCase)),
            AppleUniformTypeIdentifiers = ExcludeValues(
                imageTypes.AppleUniformTypeIdentifiers,
                value => value.EndsWith(
                    ".gif",
                    StringComparison.OrdinalIgnoreCase))
        };
    }

    private static IReadOnlyList<string>? ExcludeValues(
        IReadOnlyList<string>? values,
        Func<string, bool> isExcluded)
    {
        if (values is null)
        {
            return null;
        }

        return values
            .Where(value => !isExcluded(value))
            .ToList();
    }
}
