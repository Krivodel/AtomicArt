namespace AtomicArt.Desktop.Services.Generation;

internal sealed class JsonStringEscapeDecoder
{
    public int? DecodedCharacter { get; private set; }

    private const int UnicodeHexDigitCount = 4;
    private const int HexadecimalRadix = 16;
    private const int DecimalDigitCount = 10;

    private int _remainingUnicodeDigits;
    private int _unicodeCharacter;

    public void Process(byte value)
    {
        DecodedCharacter = null;

        if (_remainingUnicodeDigits == 0)
        {
            if (value == (byte)'u')
            {
                _remainingUnicodeDigits = UnicodeHexDigitCount;
                _unicodeCharacter = 0;
                return;
            }

            DecodedCharacter = value switch
            {
                (byte)'"' => '"',
                (byte)'\\' => '\\',
                (byte)'/' => '/',
                (byte)'b' => '\b',
                (byte)'f' => '\f',
                (byte)'n' => '\n',
                (byte)'r' => '\r',
                (byte)'t' => '\t',
                _ => throw new InvalidDataException(
                    "Provider image string contains an invalid JSON escape sequence.")
            };

            return;
        }

        int digit = value switch
        {
            >= (byte)'0' and <= (byte)'9' => value - (byte)'0',
            >= (byte)'a' and <= (byte)'f' => value - (byte)'a' + DecimalDigitCount,
            >= (byte)'A' and <= (byte)'F' => value - (byte)'A' + DecimalDigitCount,
            _ => throw new InvalidDataException(
                "Provider image string contains an invalid JSON Unicode escape sequence.")
        };
        _unicodeCharacter = (_unicodeCharacter * HexadecimalRadix) + digit;
        _remainingUnicodeDigits--;

        if (_remainingUnicodeDigits == 0)
        {
            DecodedCharacter = _unicodeCharacter;
        }
    }
}
