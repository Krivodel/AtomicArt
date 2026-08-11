using Avalonia.Platform.Storage;
using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class AttachmentFilePickerFileTypesTests
{
    [Fact]
    public void Images_WithDefaultImageTypes_ExcludesGif()
    {
        FilePickerFileType imageTypes = AttachmentFilePickerFileTypes.Images;

        imageTypes.Patterns.Should().NotContain(
            pattern => pattern.EndsWith(
                ".gif",
                StringComparison.OrdinalIgnoreCase));
        imageTypes.MimeTypes.Should().NotContain(
            contentType => string.Equals(
                contentType,
                GenerationImageContentTypes.Gif,
                StringComparison.OrdinalIgnoreCase));
        imageTypes.AppleUniformTypeIdentifiers.Should().NotContain(
            identifier => identifier.EndsWith(
                ".gif",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Images_WithDefaultImageTypes_PreservesNonGifPatterns()
    {
        IReadOnlyList<string>? expectedPatterns = FilePickerFileTypes
            .ImageAll
            .Patterns?
            .Where(pattern => !pattern.EndsWith(
                ".gif",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        AttachmentFilePickerFileTypes.Images.Patterns.Should().Equal(
            expectedPatterns);
    }
}
