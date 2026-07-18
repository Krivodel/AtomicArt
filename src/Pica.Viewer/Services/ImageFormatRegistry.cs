namespace Pica.Viewer.Services;

public sealed class ImageFormatRegistry : IImageFormatRegistry
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypesByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PicaImageFormats.PngExtension] = PicaImageFormats.PngContentType,
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".bmp"] = "image/bmp",
            [".gif"] = "image/gif",
            [".ico"] = "image/x-icon"
        };

    public bool IsSupportedFileName(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return ContentTypesByExtension.ContainsKey(extension);
    }

    public string GetContentType(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return ContentTypesByExtension.GetValueOrDefault(extension, PicaImageFormats.PngContentType);
    }

    public string GetExtension(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        return string.IsNullOrWhiteSpace(extension) ? PicaImageFormats.PngExtension : extension;
    }
}
