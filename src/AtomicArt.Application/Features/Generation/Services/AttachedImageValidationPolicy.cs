using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Application.Features.Generation.Services;

internal static class AttachedImageValidationPolicy
{
    public static bool HasContent(byte[]? content)
    {
        return content is { Length: > 0 };
    }

    public static bool HasValidFileName(string? fileName)
    {
        return AttachedImageFileNamePolicy.IsValid(fileName);
    }

    public static string? NormalizeFileName(string? fileName)
    {
        return AttachedImageFileNamePolicy.Normalize(fileName);
    }

    public static bool HasSupportedContentType(
        string? contentType,
        IReadOnlyList<AttachedImageSignatureRule> signatureRules)
    {
        return GetSignatureRule(contentType, signatureRules) is not null;
    }

    public static bool HasValidSignature(
        string? contentType,
        byte[]? content,
        IReadOnlyList<AttachedImageSignatureRule> signatureRules)
    {
        if (content is not { Length: > 0 } signatureContent)
        {
            return true;
        }

        AttachedImageSignatureRule? signatureRule = GetSignatureRule(contentType, signatureRules);

        return signatureRule is null || signatureRule.Matches(signatureContent);
    }

    public static AttachedImageSignatureRule? GetSignatureRule(
        string? contentType,
        IReadOnlyList<AttachedImageSignatureRule> signatureRules)
    {
        ArgumentNullException.ThrowIfNull(signatureRules);

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        string normalizedContentType = contentType.Trim();

        return signatureRules.FirstOrDefault(rule =>
            string.Equals(rule.ContentType, normalizedContentType, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<AttachedImageSignatureRule> CreateSignatureRules(
        IEnumerable<IAttachedImageFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        List<IAttachedImageFormat> availableFormats = formats.ToList();

        if (availableFormats.Count == 0)
        {
            throw new InvalidOperationException(
                "No attachment formats are registered.");
        }

        return availableFormats
            .Select(format => new AttachedImageSignatureRule(
                format.ContentType,
                content => format.MatchesSignature(content)))
            .ToList();
    }
}
