using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services;

public sealed class PicaStartupRequestFactoryTests
{
    [Fact]
    public async Task CreateAsync_WithFileArguments_CreatesStandaloneRequest()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string filePath = Path.Combine(temporaryDirectory.DirectoryPath, "image.png");
        await File.WriteAllBytesAsync(filePath, [1, 2, 3]);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [filePath],
            CancellationToken.None);

        request.HostConnection.Should().BeNull();
        request.ViewerRequest.Items.Should().ContainSingle();
        request.ViewerRequest.Items[0].FilePath.Should().Be(Path.GetFullPath(filePath));
        request.ViewerRequest.Items[0].FileName.Should().Be("image.png");
        request.ViewerRequest.SelectedItemId.Should().Be(request.ViewerRequest.Items[0].Id);
        request.ViewerRequest.Actions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithSingleImage_IncludesDirectoryImagesSortedByNewestFirst()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string firstImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "01.jpg");
        string selectedImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "02.png");
        string iconPath = Path.Combine(temporaryDirectory.DirectoryPath, "03.ico");
        string unsupportedFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "notes.txt");
        await File.WriteAllBytesAsync(firstImagePath, [1]);
        await File.WriteAllBytesAsync(selectedImagePath, [2]);
        await File.WriteAllBytesAsync(iconPath, [3]);
        await File.WriteAllTextAsync(unsupportedFilePath, "text");
        SetLastWriteTimesNewestFirst(selectedImagePath, iconPath, firstImagePath);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedImagePath],
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("02.png", "03.ico", "01.jpg");
        request.ViewerRequest.SelectedItemId.Should().Be(
            request.ViewerRequest.Items.Single(item => item.FileName == "02.png").Id);
    }

    [Fact]
    public async Task CreateAsync_WithSelectedIcon_IncludesAllSupportedDirectoryImages()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string pngPath = Path.Combine(temporaryDirectory.DirectoryPath, "01.png");
        string selectedIconPath = Path.Combine(temporaryDirectory.DirectoryPath, "02.ico");
        await File.WriteAllBytesAsync(pngPath, [1]);
        await File.WriteAllBytesAsync(selectedIconPath, [2]);
        SetLastWriteTimesNewestFirst(pngPath, selectedIconPath);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedIconPath],
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("01.png", "02.ico");
        request.ViewerRequest.SelectedItemId.Should().Be(
            request.ViewerRequest.Items.Single(item => item.FileName == "02.ico").Id);
    }

    [Fact]
    public async Task CreateAsync_WithMultipleImagesFromSameDirectory_IncludesAllSupportedDirectoryImages()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string firstRequestedImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "01.jpg");
        string secondRequestedImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "02.jpg");
        string otherFormatImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "03.png");
        await File.WriteAllBytesAsync(firstRequestedImagePath, [1]);
        await File.WriteAllBytesAsync(secondRequestedImagePath, [2]);
        await File.WriteAllBytesAsync(otherFormatImagePath, [3]);
        SetLastWriteTimesNewestFirst(
            firstRequestedImagePath,
            secondRequestedImagePath,
            otherFormatImagePath);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [firstRequestedImagePath, secondRequestedImagePath],
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("01.jpg", "02.jpg", "03.png");
        request.ViewerRequest.SelectedItemId.Should().Be(
            request.ViewerRequest.Items.Single(item => item.FileName == "01.jpg").Id);
    }

    [Fact]
    public async Task CreateAsync_WithMultipleImageArguments_DoesNotExpandDirectories()
    {
        using PicaTemporaryDirectory firstTemporaryDirectory = new();
        using PicaTemporaryDirectory secondTemporaryDirectory = new();
        string selectedImagePath = Path.Combine(
            firstTemporaryDirectory.DirectoryPath,
            "selected.png");
        string unrequestedImagePath = Path.Combine(
            firstTemporaryDirectory.DirectoryPath,
            "unrequested.png");
        string requestedImagePath = Path.Combine(
            secondTemporaryDirectory.DirectoryPath,
            "requested.jpg");
        await File.WriteAllBytesAsync(selectedImagePath, [1]);
        await File.WriteAllBytesAsync(unrequestedImagePath, [2]);
        await File.WriteAllBytesAsync(requestedImagePath, [3]);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedImagePath, requestedImagePath],
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FilePath)
            .Should()
            .Equal(Path.GetFullPath(selectedImagePath), Path.GetFullPath(requestedImagePath));
    }

    private static void SetLastWriteTimesNewestFirst(params string[] paths)
    {
        DateTime newestDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int index = 0; index < paths.Length; index++)
        {
            File.SetLastWriteTimeUtc(paths[index], newestDate.AddDays(-index));
        }
    }

    private static PicaStartupRequestFactory CreateFactory()
    {
        return new PicaStartupRequestFactory(
            new ImageFormatRegistry(),
            NullLogger<PicaStartupRequestFactory>.Instance);
    }
}
