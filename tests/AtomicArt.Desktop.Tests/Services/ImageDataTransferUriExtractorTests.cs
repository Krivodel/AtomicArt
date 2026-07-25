using System.Text;

using Avalonia.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ImageDataTransferUriExtractorTests
{
    private static readonly Uri ImageUri =
        new("https://images.atomicart.test/reference.png");

    [Fact]
    public void TryGetImageUri_WithPlainTextUrl_ReturnsUri()
    {
        DataTransfer dataTransfer = CreateTextTransfer(DataFormat.Text, ImageUri.AbsoluteUri);

        bool result = ImageDataTransferUriExtractor.TryGetImageUri(
            dataTransfer,
            out Uri? imageUri);

        result.Should().BeTrue();
        imageUri.Should().Be(ImageUri);
    }

    [Fact]
    public void TryGetImageUri_WithWindowsUnicodeUrlFormat_ReturnsUri()
    {
        DataFormat<byte[]> format = DataFormat.CreateBytesPlatformFormat(
            "UniformResourceLocatorW");
        byte[] content = Encoding.Unicode.GetBytes(
            string.Concat(ImageUri.AbsoluteUri, '\0'));
        DataTransfer dataTransfer = CreateBytesTransfer(format, content);

        bool result = ImageDataTransferUriExtractor.TryGetImageUri(
            dataTransfer,
            out Uri? imageUri);

        result.Should().BeTrue();
        imageUri.Should().Be(ImageUri);
    }

    [Fact]
    public void TryGetImageUri_WithUriList_ReturnsFirstSupportedUri()
    {
        DataFormat<byte[]> format = DataFormat.CreateBytesPlatformFormat(
            "text/uri-list");
        byte[] content = Encoding.UTF8.GetBytes(
            $"# browser image{Environment.NewLine}{ImageUri.AbsoluteUri}");
        DataTransfer dataTransfer = CreateBytesTransfer(format, content);

        bool result = ImageDataTransferUriExtractor.TryGetImageUri(
            dataTransfer,
            out Uri? imageUri);

        result.Should().BeTrue();
        imageUri.Should().Be(ImageUri);
    }

    [Fact]
    public void TryGetImageUri_WithBrowserHtml_ReturnsImageSource()
    {
        DataFormat<byte[]> format = DataFormat.CreateBytesPlatformFormat(
            "HTML Format");
        string html =
            "<html><body><img alt=\"reference\" src=\"https://images.atomicart.test/reference.png?x=1&amp;y=2\"></body></html>";
        DataTransfer dataTransfer = CreateBytesTransfer(
            format,
            Encoding.UTF8.GetBytes(html));

        bool result = ImageDataTransferUriExtractor.TryGetImageUri(
            dataTransfer,
            out Uri? imageUri);

        result.Should().BeTrue();
        imageUri.Should().Be(
            new Uri("https://images.atomicart.test/reference.png?x=1&y=2"));
    }

    [Fact]
    public void TryGetImageUri_WithBrowserDownloadUrl_ReturnsUri()
    {
        DataFormat<byte[]> format = DataFormat.CreateBytesPlatformFormat(
            "DownloadURL");
        string downloadValue = string.Concat(
            "image/png:reference.png:",
            ImageUri.AbsoluteUri);
        DataTransfer dataTransfer = CreateBytesTransfer(
            format,
            Encoding.UTF8.GetBytes(downloadValue));

        bool result = ImageDataTransferUriExtractor.TryGetImageUri(
            dataTransfer,
            out Uri? imageUri);

        result.Should().BeTrue();
        imageUri.Should().Be(ImageUri);
    }

    [Fact]
    public void TryGetImageUri_WithNonUrlText_ReturnsFalse()
    {
        DataTransfer dataTransfer = CreateTextTransfer(
            DataFormat.Text,
            "reference image");

        bool result = ImageDataTransferUriExtractor.TryGetImageUri(
            dataTransfer,
            out Uri? imageUri);

        result.Should().BeFalse();
        imageUri.Should().BeNull();
    }

    private static DataTransfer CreateTextTransfer(
        DataFormat<string> format,
        string content)
    {
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.Create(format, content));

        return dataTransfer;
    }

    private static DataTransfer CreateBytesTransfer(
        DataFormat<byte[]> format,
        byte[] content)
    {
        DataTransfer dataTransfer = new();
        dataTransfer.Add(DataTransferItem.Create(format, content));

        return dataTransfer;
    }
}
