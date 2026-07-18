using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailStorageTests
{
    private const string ModelId = "test-model";

    private static readonly Guid BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SaveAsync_WithGeneratedImage_WritesThumbnailToThumbnailsDirectory()
    {
        string rootDirectory = CreateCleanDirectory(nameof(SaveAsync_WithGeneratedImage_WritesThumbnailToThumbnailsDirectory));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        string sourceImagePath = await WriteSourceImageAsync(pathProvider);
        GalleryThumbnailStorage storage = CreateStorage(pathProvider, CreateGenerator());

        await storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            sourceImagePath,
            CancellationToken.None);
        string? thumbnailPath = storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

        thumbnailPath.Should().NotBeNull();
        File.Exists(thumbnailPath).Should().BeTrue();
        Path.GetDirectoryName(thumbnailPath).Should().Be(Path.GetFullPath(pathProvider.ThumbnailsDirectory));
    }

    [Fact]
    public async Task SaveAsync_WithExistingThumbnail_ReplacesThumbnail()
    {
        string rootDirectory = CreateCleanDirectory(nameof(SaveAsync_WithExistingThumbnail_ReplacesThumbnail));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        string sourceImagePath = await WriteSourceImageAsync(pathProvider);
        byte[] redThumbnail = GalleryThumbnailTestImages.CreatePngBytes(
            GalleryThumbnailSpecification.ShortSidePixels,
            GalleryThumbnailSpecification.ShortSidePixels,
            SKColors.Red);
        byte[] blueThumbnail = GalleryThumbnailTestImages.CreatePngBytes(
            GalleryThumbnailSpecification.ShortSidePixels,
            GalleryThumbnailSpecification.ShortSidePixels,
            SKColors.Blue);
        Mock<IGalleryThumbnailGenerator> generatorMock = new();
        generatorMock
            .SetupSequence(generator => generator.CreateThumbnailAsync(sourceImagePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(redThumbnail)
            .ReturnsAsync(blueThumbnail);
        GalleryThumbnailStorage storage = CreateStorage(pathProvider, generatorMock.Object);

        await storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            sourceImagePath,
            CancellationToken.None);
        string? firstThumbnailPath = storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);
        await storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            sourceImagePath,
            CancellationToken.None);
        string? secondThumbnailPath = storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

        firstThumbnailPath.Should().Be(secondThumbnailPath);
        secondThumbnailPath.Should().NotBeNull();
        GalleryThumbnailTestImages.ReadFirstPixel(secondThumbnailPath).Should().Be(SKColors.Blue);
    }

    [Fact]
    public async Task GetThumbnailPathOrDefault_WithSavedThumbnail_ReturnsTrustedPath()
    {
        string rootDirectory = CreateCleanDirectory(
            nameof(GetThumbnailPathOrDefault_WithSavedThumbnail_ReturnsTrustedPath));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        string sourceImagePath = await WriteSourceImageAsync(pathProvider);
        GalleryThumbnailStorage storage = CreateStorage(pathProvider, CreateGenerator());

        await storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            sourceImagePath,
            CancellationToken.None);
        string? thumbnailPath = storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

        thumbnailPath.Should().NotBeNull();
        thumbnailPath.Should().Be(Path.GetFullPath(thumbnailPath));
        GalleryThumbnailTestImages.ReadSize(thumbnailPath).Width.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_WhenGeneratorThrows_LogsAndThrows()
    {
        string rootDirectory = CreateCleanDirectory(nameof(SaveAsync_WhenGeneratorThrows_LogsAndThrows));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        string sourceImagePath = await WriteSourceImageAsync(pathProvider);
        InvalidDataException exception = new("Invalid test image.");
        Mock<IGalleryThumbnailGenerator> generatorMock = new();
        generatorMock
            .Setup(generator => generator.CreateThumbnailAsync(sourceImagePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        Mock<ILogger<GalleryThumbnailStorage>> loggerMock = new();
        GalleryThumbnailStorage storage = CreateStorage(pathProvider, generatorMock.Object, loggerMock.Object);

        Func<Task> act = () => storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            sourceImagePath,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId).Should().BeNull();
        VerifyWarningLogged(loggerMock, "Failed to create gallery thumbnail");
    }

    [Fact]
    public async Task BuildFileName_ForFullImageAndThumbnail_UsesGenerationImageFileNamePolicy()
    {
        string rootDirectory = CreateCleanDirectory(nameof(BuildFileName_ForFullImageAndThumbnail_UsesGenerationImageFileNamePolicy));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        string sourceImagePath = await WriteSourceImageAsync(pathProvider);
        GenerationImageFileNamePolicy fileNamePolicy = new();
        GalleryThumbnailImageFormat thumbnailImageFormat = new();
        GalleryThumbnailStorage storage = CreateStorage(
            pathProvider,
            CreateGenerator(),
            NullLogger<GalleryThumbnailStorage>.Instance,
            fileNamePolicy,
            thumbnailImageFormat);
        string expectedFileName = fileNamePolicy.BuildFileName(
            BatchId,
            ItemId,
            thumbnailImageFormat.Extension);

        await storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            sourceImagePath,
            CancellationToken.None);
        string? thumbnailPath = storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

        thumbnailPath.Should().NotBeNull();
        Path.GetFileName(thumbnailPath).Should().Be(expectedFileName);
    }

    [Fact]
    public async Task SaveAsync_WithEmptyImagePath_LogsAndDoesNotCreateThumbnail()
    {
        string rootDirectory = CreateCleanDirectory(nameof(SaveAsync_WithEmptyImagePath_LogsAndDoesNotCreateThumbnail));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        Mock<IGalleryThumbnailGenerator> generatorMock = new();
        Mock<ILogger<GalleryThumbnailStorage>> loggerMock = new();
        GalleryThumbnailStorage storage = CreateStorage(pathProvider, generatorMock.Object, loggerMock.Object);

        await storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            string.Empty,
            CancellationToken.None);
        string? thumbnailPath = storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

        thumbnailPath.Should().BeNull();
        generatorMock.Verify(
            generator => generator.CreateThumbnailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyWarningLogged(loggerMock, "Gallery thumbnail source image path is not trusted");
    }

    [Fact]
    public async Task SaveAsync_WithUntrustedImagePath_LogsAndDoesNotCreateThumbnail()
    {
        string rootDirectory = CreateCleanDirectory(
            nameof(SaveAsync_WithUntrustedImagePath_LogsAndDoesNotCreateThumbnail));
        string untrustedDirectory = CreateCleanDirectory(
            string.Concat(nameof(SaveAsync_WithUntrustedImagePath_LogsAndDoesNotCreateThumbnail), "-untrusted"));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        string sourceImagePath = Path.Combine(untrustedDirectory, "source.png");
        await File.WriteAllBytesAsync(
            sourceImagePath,
            GalleryThumbnailTestImages.CreatePngBytes(
                GalleryThumbnailSpecification.ShortSidePixels,
                GalleryThumbnailSpecification.ShortSidePixels));
        Mock<IGalleryThumbnailGenerator> generatorMock = new();
        Mock<ILogger<GalleryThumbnailStorage>> loggerMock = new();
        GalleryThumbnailStorage storage = CreateStorage(pathProvider, generatorMock.Object, loggerMock.Object);

        await storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            sourceImagePath,
            CancellationToken.None);
        string? thumbnailPath = storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

        thumbnailPath.Should().BeNull();
        generatorMock.Verify(
            generator => generator.CreateThumbnailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyWarningLogged(loggerMock, "Gallery thumbnail source image path is not trusted");
    }

    private static GalleryThumbnailStorage CreateStorage(
        AtomicArtDataPathProvider pathProvider,
        IGalleryThumbnailGenerator thumbnailGenerator)
    {
        return CreateStorage(
            pathProvider,
            thumbnailGenerator,
            NullLogger<GalleryThumbnailStorage>.Instance);
    }

    private static GalleryThumbnailStorage CreateStorage(
        AtomicArtDataPathProvider pathProvider,
        IGalleryThumbnailGenerator thumbnailGenerator,
        ILogger<GalleryThumbnailStorage> logger)
    {
        return CreateStorage(
            pathProvider,
            thumbnailGenerator,
            logger,
            new GenerationImageFileNamePolicy(),
            new GalleryThumbnailImageFormat());
    }

    private static GalleryThumbnailStorage CreateStorage(
        AtomicArtDataPathProvider pathProvider,
        IGalleryThumbnailGenerator thumbnailGenerator,
        ILogger<GalleryThumbnailStorage> logger,
        GenerationImageFileNamePolicy fileNamePolicy,
        GalleryThumbnailImageFormat thumbnailImageFormat)
    {
        TrustedImageFileService trustedImageFileService = new(
            pathProvider,
            GenerationImageFormatRegistryTestFactory.Create(),
            NullLogger<TrustedImageFileService>.Instance);

        return new GalleryThumbnailStorage(
            pathProvider,
            trustedImageFileService,
            fileNamePolicy,
            thumbnailImageFormat,
            thumbnailGenerator,
            logger);
    }

    private static GalleryThumbnailGenerator CreateGenerator()
    {
        return new GalleryThumbnailGenerator(new GalleryThumbnailImageFormat());
    }

    private static string CreateCleanDirectory(string name)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "AtomicArtDesktopTests",
            nameof(GalleryThumbnailStorageTests),
            name);

        DeleteDirectoryIfExists(directory);
        Directory.CreateDirectory(directory);

        return directory;
    }

    private static void DeleteDirectoryIfExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<string> WriteSourceImageAsync(AtomicArtDataPathProvider pathProvider)
    {
        pathProvider.EnsureDirectoryExists(pathProvider.ArtDirectory);
        string sourceImagePath = Path.Combine(pathProvider.ArtDirectory, "source.png");
        await File.WriteAllBytesAsync(
            sourceImagePath,
            GalleryThumbnailTestImages.CreatePngBytes(
                GalleryThumbnailSpecification.ShortSidePixels * 2,
                GalleryThumbnailSpecification.ShortSidePixels * 2));

        return sourceImagePath;
    }

    private static void VerifyWarningLogged(
        Mock<ILogger<GalleryThumbnailStorage>> loggerMock,
        string expectedMessage)
    {
        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    (state.ToString() ?? string.Empty).Contains(expectedMessage, StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
