using System.Net;

namespace AtomicArt.Desktop.Services;

internal static class HtmlImageSourceExtractor
{
    public static bool TryExtract(string html, out string? source)
    {
        ArgumentNullException.ThrowIfNull(html);
        int searchIndex = 0;

        while (searchIndex < html.Length)
        {
            int imageTagIndex = html.IndexOf(
                "<img",
                searchIndex,
                StringComparison.OrdinalIgnoreCase);

            if (imageTagIndex < 0)
            {
                break;
            }

            int tagNameEndIndex = imageTagIndex + 4;

            if (tagNameEndIndex < html.Length
                && !char.IsWhiteSpace(html[tagNameEndIndex])
                && html[tagNameEndIndex] is not ('/' or '>'))
            {
                searchIndex = tagNameEndIndex;
                continue;
            }

            int tagEndIndex = html.IndexOf('>', imageTagIndex + 4);

            if (tagEndIndex < 0)
            {
                break;
            }

            ReadOnlySpan<char> imageTag = html.AsSpan(
                imageTagIndex + 4,
                tagEndIndex - imageTagIndex - 4);

            if (TryGetAttributeValue(imageTag, "src", out string? attributeValue))
            {
                source = WebUtility.HtmlDecode(attributeValue);
                return !string.IsNullOrWhiteSpace(source);
            }

            searchIndex = tagEndIndex + 1;
        }

        source = null;
        return false;
    }

    private static bool TryGetAttributeValue(
        ReadOnlySpan<char> tag,
        string attributeName,
        out string? value)
    {
        int index = 0;

        while (index < tag.Length)
        {
            SkipWhiteSpace(tag, ref index);
            int nameStart = index;

            while (index < tag.Length
                   && !char.IsWhiteSpace(tag[index])
                   && tag[index] != '=')
            {
                index++;
            }

            ReadOnlySpan<char> name = tag[nameStart..index];
            SkipWhiteSpace(tag, ref index);

            if (index >= tag.Length || tag[index] != '=')
            {
                SkipAttribute(tag, ref index);
                continue;
            }

            index++;
            SkipWhiteSpace(tag, ref index);
            ReadOnlySpan<char> attributeValue = ReadAttributeValue(tag, ref index);

            if (name.Equals(attributeName, StringComparison.OrdinalIgnoreCase))
            {
                value = attributeValue.ToString();
                return true;
            }
        }

        value = null;
        return false;
    }

    private static ReadOnlySpan<char> ReadAttributeValue(
        ReadOnlySpan<char> tag,
        ref int index)
    {
        if (index >= tag.Length)
        {
            return [];
        }

        char quote = tag[index];

        if (quote is '"' or '\'')
        {
            index++;
            int valueStart = index;

            while (index < tag.Length && tag[index] != quote)
            {
                index++;
            }

            ReadOnlySpan<char> value = tag[valueStart..index];

            if (index < tag.Length)
            {
                index++;
            }

            return value;
        }

        int unquotedValueStart = index;

        while (index < tag.Length && !char.IsWhiteSpace(tag[index]))
        {
            index++;
        }

        return tag[unquotedValueStart..index];
    }

    private static void SkipWhiteSpace(ReadOnlySpan<char> value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }

    private static void SkipAttribute(ReadOnlySpan<char> tag, ref int index)
    {
        while (index < tag.Length && !char.IsWhiteSpace(tag[index]))
        {
            index++;
        }
    }
}
