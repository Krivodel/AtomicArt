using System.Text.Json;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterImageUsageReader
{
    private const int MaximumRetainedResponseTailBytes = 65536;

    private readonly byte[] _responseTail = new byte[MaximumRetainedResponseTailBytes];

    private int _responseTailLength;

    public void Append(ReadOnlySpan<byte> responseBytes)
    {
        if (responseBytes.Length >= MaximumRetainedResponseTailBytes)
        {
            responseBytes[^MaximumRetainedResponseTailBytes..].CopyTo(_responseTail);
            _responseTailLength = MaximumRetainedResponseTailBytes;

            return;
        }

        int preservedLength = Math.Min(
            _responseTailLength,
            MaximumRetainedResponseTailBytes - responseBytes.Length);

        if (preservedLength > 0)
        {
            Buffer.BlockCopy(
                _responseTail,
                _responseTailLength - preservedLength,
                _responseTail,
                0,
                preservedLength);
        }

        responseBytes.CopyTo(_responseTail.AsSpan(preservedLength));
        _responseTailLength = preservedLength + responseBytes.Length;
    }

    public GenerationPriceDto? ReadPrice()
    {
        ReadOnlySpan<byte> responseTail = _responseTail.AsSpan(0, _responseTailLength);
        ReadOnlySpan<byte> usageProperty = "\"usage\""u8;

        for (int index = 0; index <= responseTail.Length - usageProperty.Length; index++)
        {
            if (!responseTail.Slice(index, usageProperty.Length).SequenceEqual(usageProperty))
            {
                continue;
            }

            if (TryReadUsagePrice(responseTail, index + usageProperty.Length, out GenerationPriceDto? price))
            {
                return price;
            }
        }

        return null;
    }

    private static bool TryReadUsagePrice(
        ReadOnlySpan<byte> responseTail,
        int startIndex,
        out GenerationPriceDto? price)
    {
        int valueStartIndex = SkipWhitespace(responseTail, startIndex);

        if (valueStartIndex >= responseTail.Length || responseTail[valueStartIndex] != (byte)':')
        {
            price = null;
            return false;
        }

        valueStartIndex = SkipWhitespace(responseTail, valueStartIndex + 1);

        if (valueStartIndex >= responseTail.Length || responseTail[valueStartIndex] != (byte)'{')
        {
            price = null;
            return false;
        }

        int valueLength = GetObjectLength(responseTail[valueStartIndex..]);

        if (valueLength <= 0)
        {
            price = null;
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(
            responseTail.Slice(valueStartIndex, valueLength).ToArray());

        if (!document.RootElement.TryGetProperty("cost", out JsonElement cost)
            || cost.ValueKind != JsonValueKind.Number
            || !cost.TryGetDecimal(out decimal amount)
            || amount < 0m)
        {
            price = null;
            return true;
        }

        price = new GenerationPriceDto(
            amount,
            "USD",
            GenerationPriceSources.ActualProviderUsage);
        return true;
    }

    private static int SkipWhitespace(ReadOnlySpan<byte> value, int index)
    {
        while (index < value.Length
            && (value[index] == (byte)' '
                || value[index] == (byte)'\t'
                || value[index] == (byte)'\r'
                || value[index] == (byte)'\n'))
        {
            index++;
        }

        return index;
    }

    private static int GetObjectLength(ReadOnlySpan<byte> value)
    {
        bool isInsideString = false;
        bool isEscaped = false;
        int objectDepth = 0;

        for (int index = 0; index < value.Length; index++)
        {
            byte current = value[index];

            if (isInsideString)
            {
                if (isEscaped)
                {
                    isEscaped = false;
                }
                else if (current == (byte)'\\')
                {
                    isEscaped = true;
                }
                else if (current == (byte)'\"')
                {
                    isInsideString = false;
                }

                continue;
            }

            if (current == (byte)'\"')
            {
                isInsideString = true;
            }
            else if (current == (byte)'{')
            {
                objectDepth++;
            }
            else if (current == (byte)'}')
            {
                objectDepth--;

                if (objectDepth == 0)
                {
                    return index + 1;
                }
            }
        }

        return 0;
    }
}
