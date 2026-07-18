namespace AtomicArt.Desktop.Services;

public sealed record ApiBaseAddress
{
    public Uri Value { get; }

    private ApiBaseAddress(Uri value)
    {
        Value = value;
    }

    public static bool TryCreate(string? value, out ApiBaseAddress? baseAddress)
    {
        baseAddress = null;

        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? parsedAddress)
            || parsedAddress is null
            || !IsSupportedScheme(parsedAddress.Scheme)
            || !string.IsNullOrEmpty(parsedAddress.UserInfo)
            || !string.IsNullOrEmpty(parsedAddress.Query)
            || !string.IsNullOrEmpty(parsedAddress.Fragment))
        {
            return false;
        }

        string normalizedValue = parsedAddress.AbsoluteUri.EndsWith(
            "/",
            StringComparison.Ordinal)
            ? parsedAddress.AbsoluteUri
            : $"{parsedAddress.AbsoluteUri}/";

        if (!Uri.TryCreate(normalizedValue, UriKind.Absolute, out Uri? normalizedAddress)
            || normalizedAddress is null)
        {
            return false;
        }

        baseAddress = new ApiBaseAddress(normalizedAddress);
        return true;
    }

    public override string ToString()
    {
        return Value.AbsoluteUri;
    }

    private static bool IsSupportedScheme(string scheme)
    {
        return string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
