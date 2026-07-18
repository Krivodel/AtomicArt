namespace AtomicArt.Desktop.Services;

internal static class SkiaImageDecodeFailure
{
    private const string CodecParameterName = "codec";

    public static bool IsInvalidImage(ArgumentNullException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return string.Equals(
            exception.ParamName,
            CodecParameterName,
            StringComparison.Ordinal);
    }
}
