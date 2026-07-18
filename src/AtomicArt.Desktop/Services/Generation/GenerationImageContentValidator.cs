using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationImageContentValidator : IGenerationImageContentValidator
{
    public const int DefaultMaxImageBytes = 128 * 1_048_576;

    private readonly IGenerationImageFormatRegistry _formatRegistry;
    private readonly int _maxImageBytes;

    public GenerationImageContentValidator(IGenerationImageFormatRegistry formatRegistry)
        : this(formatRegistry, DefaultMaxImageBytes)
    {
    }

    internal GenerationImageContentValidator(
        IGenerationImageFormatRegistry formatRegistry,
        int maxImageBytes)
    {
        ArgumentNullException.ThrowIfNull(formatRegistry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxImageBytes);

        _formatRegistry = formatRegistry;
        _maxImageBytes = maxImageBytes;
    }

    public bool TryValidate(
        GenerationImageContentDto? content,
        out GenerationImageContentValidationResult? result)
    {
        result = null;

        if (content is null
            || string.IsNullOrWhiteSpace(content.Base64Data)
            || IsBase64TooLarge(content.Base64Data))
        {
            return false;
        }

        if (!_formatRegistry.TryGetByContentType(
            content.ContentType,
            out IGenerationImageFormat? format)
            || format is null)
        {
            return false;
        }

        byte[] buffer = new byte[GetMaxDecodedBytes(content.Base64Data)];

        if (!Convert.TryFromBase64String(
            content.Base64Data,
            buffer,
            out int bytesWritten))
        {
            return false;
        }

        byte[] bytes = buffer[..bytesWritten];

        if (bytes.Length <= 0
            || bytes.Length > _maxImageBytes
            || !format.MatchesSignature(bytes))
        {
            return false;
        }

        result = new GenerationImageContentValidationResult(format.ContentType, bytes);
        return true;
    }

    private bool IsBase64TooLarge(string base64Data)
    {
        int maxBase64Chars = ((_maxImageBytes + 2) / 3) * 4;

        return base64Data.Length > maxBase64Chars;
    }

    private int GetMaxDecodedBytes(string base64Data)
    {
        int maxDecodedBytes = (base64Data.Length / 4) * 3;

        if (maxDecodedBytes <= 0)
        {
            return 1;
        }

        return Math.Min(maxDecodedBytes, _maxImageBytes);
    }
}
