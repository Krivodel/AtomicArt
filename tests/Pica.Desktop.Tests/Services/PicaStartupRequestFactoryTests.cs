using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services;

public sealed class PicaStartupRequestFactoryTests
{
    [Fact]
    public async Task CreateAsync_WithFileArguments_CreatesStandaloneRequest()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directoryPath, "image.png");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(filePath, [1, 2, 3]);
        PicaStartupRequestFactory factory = CreateFactory();

        try
        {
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
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public async Task CreateAsync_WithSingleImage_IncludesDirectoryImagesSortedByNewestFirst()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string firstImagePath = Path.Combine(directoryPath, "01.jpg");
        string selectedImagePath = Path.Combine(directoryPath, "02.png");
        string iconPath = Path.Combine(directoryPath, "03.ico");
        string unsupportedFilePath = Path.Combine(directoryPath, "notes.txt");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(firstImagePath, [1]);
        await File.WriteAllBytesAsync(selectedImagePath, [2]);
        await File.WriteAllBytesAsync(iconPath, [3]);
        await File.WriteAllTextAsync(unsupportedFilePath, "text");
        SetLastWriteTimesNewestFirst(selectedImagePath, iconPath, firstImagePath);
        PicaStartupRequestFactory factory = CreateFactory();

        try
        {
            PicaStartupRequest request = await factory.CreateAsync(
                [selectedImagePath],
                CancellationToken.None);

            request.ViewerRequest.Items.Select(item => item.FileName)
                .Should()
                .Equal("02.png", "03.ico", "01.jpg");
            request.ViewerRequest.SelectedItemId.Should().Be(
                request.ViewerRequest.Items.Single(item => item.FileName == "02.png").Id);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public async Task CreateAsync_WithSelectedIcon_IncludesAllSupportedDirectoryImages()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string pngPath = Path.Combine(directoryPath, "01.png");
        string selectedIconPath = Path.Combine(directoryPath, "02.ico");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(pngPath, [1]);
        await File.WriteAllBytesAsync(selectedIconPath, [2]);
        SetLastWriteTimesNewestFirst(pngPath, selectedIconPath);
        PicaStartupRequestFactory factory = CreateFactory();

        try
        {
            PicaStartupRequest request = await factory.CreateAsync(
                [selectedIconPath],
                CancellationToken.None);

            request.ViewerRequest.Items.Select(item => item.FileName)
                .Should()
                .Equal("01.png", "02.ico");
            request.ViewerRequest.SelectedItemId.Should().Be(
                request.ViewerRequest.Items.Single(item => item.FileName == "02.ico").Id);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public async Task CreateAsync_WithMultipleImagesFromSameDirectory_IncludesAllSupportedDirectoryImages()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string firstRequestedImagePath = Path.Combine(directoryPath, "01.jpg");
        string secondRequestedImagePath = Path.Combine(directoryPath, "02.jpg");
        string otherFormatImagePath = Path.Combine(directoryPath, "03.png");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(firstRequestedImagePath, [1]);
        await File.WriteAllBytesAsync(secondRequestedImagePath, [2]);
        await File.WriteAllBytesAsync(otherFormatImagePath, [3]);
        SetLastWriteTimesNewestFirst(
            firstRequestedImagePath,
            secondRequestedImagePath,
            otherFormatImagePath);
        PicaStartupRequestFactory factory = CreateFactory();

        try
        {
            PicaStartupRequest request = await factory.CreateAsync(
                [firstRequestedImagePath, secondRequestedImagePath],
                CancellationToken.None);

            request.ViewerRequest.Items.Select(item => item.FileName)
                .Should()
                .Equal("01.jpg", "02.jpg", "03.png");
            request.ViewerRequest.SelectedItemId.Should().Be(
                request.ViewerRequest.Items.Single(item => item.FileName == "01.jpg").Id);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public async Task CreateAsync_WithMultipleImageArguments_DoesNotExpandDirectories()
    {
        string firstDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string secondDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string selectedImagePath = Path.Combine(firstDirectoryPath, "selected.png");
        string unrequestedImagePath = Path.Combine(firstDirectoryPath, "unrequested.png");
        string requestedImagePath = Path.Combine(secondDirectoryPath, "requested.jpg");
        Directory.CreateDirectory(firstDirectoryPath);
        Directory.CreateDirectory(secondDirectoryPath);
        await File.WriteAllBytesAsync(selectedImagePath, [1]);
        await File.WriteAllBytesAsync(unrequestedImagePath, [2]);
        await File.WriteAllBytesAsync(requestedImagePath, [3]);
        PicaStartupRequestFactory factory = CreateFactory();

        try
        {
            PicaStartupRequest request = await factory.CreateAsync(
                [selectedImagePath, requestedImagePath],
                CancellationToken.None);

            request.ViewerRequest.Items.Select(item => item.FilePath)
                .Should()
                .Equal(Path.GetFullPath(selectedImagePath), Path.GetFullPath(requestedImagePath));
        }
        finally
        {
            Directory.Delete(firstDirectoryPath, true);
            Directory.Delete(secondDirectoryPath, true);
        }
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
