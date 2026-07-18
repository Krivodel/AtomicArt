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
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;

using static AtomicArt.Desktop.Tests.Common.DesktopTestDirectories;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailStorageTests
{
    private const string ModelId = "test-model";

    private static readonly Guid BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SaveAsync_WithGeneratedImage_WritesThumbnailToThumbnailsDirectory()
    {
        StorageTestContext context = await CreateContextAsync(
            nameof(SaveAsync_WithGeneratedImage_WritesThumbnailToThumbnailsDirectory));

        await context.Storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            context.SourceImagePath,
            CancellationToken.None);
        string? thumbnailPath = context.Storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

        thumbnailPath.Should().NotBeNull();
        File.Exists(thumbnailPath).Should().BeTrue();
        Path.GetDirectoryName(thumbnailPath).Should().Be(
            Path.GetFullPath(context.PathProvider.ThumbnailsDirectory));
    }

    [Fact]
    public async Task SaveAsync_WithExistingThumbnail_ReplacesThumbnail()
    {
        byte[] redThumbnail = GalleryThumbnailTestImages.CreatePngBytes(
            GalleryThumbnailSpecification.ShortSidePixels,
            GalleryThumbnailSpecification.ShortSidePixels,
            SKColors.Red);
        byte[] blueThumbnail = GalleryThumbnailTestImages.CreatePngBytes(
            GalleryThumbnailSpecification.ShortSidePixels,
            GalleryThumbnailSpecification.ShortSidePixels,
            SKColors.Blue);
        Mock<IGalleryThumbnailGenerator> generatorMock = new();
        StorageTestContext context = await CreateContextAsync(
            nameof(SaveAsync_WithExistingThumbnail_ReplacesThumbnail),
            sourceImagePath =>
            {
                generatorMock
                    .SetupSequence(generator => generator.CreateThumbnailAsync(
                        sourceImagePath,
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(redThumbnail)
                    .ReturnsAsync(blueThumbnail);

                return generatorMock.Object;
            });

        await context.Storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            context.SourceImagePath,
            CancellationToken.None);
        string? firstThumbnailPath = context.Storage.GetThumbnailPathOrDefault(
            BatchId,
            ItemId,
            ModelId);
        await context.Storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            context.SourceImagePath,
            CancellationToken.None);
        string? secondThumbnailPath = context.Storage.GetThumbnailPathOrDefault(
            BatchId,
            ItemId,
            ModelId);

        firstThumbnailPath.Should().Be(secondThumbnailPath);
        secondThumbnailPath.Should().NotBeNull();
        GalleryThumbnailTestImages.ReadFirstPixel(secondThumbnailPath).Should().Be(SKColors.Blue);
    }

    [Fact]
    public async Task GetThumbnailPathOrDefault_WithSavedThumbnail_ReturnsTrustedPath()
    {
        StorageTestContext context = await CreateContextAsync(
            nameof(GetThumbnailPathOrDefault_WithSavedThumbnail_ReturnsTrustedPath));

        await context.Storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            context.SourceImagePath,
            CancellationToken.None);
        string? thumbnailPath = context.Storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

        thumbnailPath.Should().NotBeNull();
        thumbnailPath.Should().Be(Path.GetFullPath(thumbnailPath));
        GalleryThumbnailTestImages.ReadSize(thumbnailPath).Width.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_WhenGeneratorThrows_LogsAndThrows()
    {
        InvalidDataException exception = new("Invalid test image.");
        Mock<IGalleryThumbnailGenerator> generatorMock = new();
        Mock<ILogger<GalleryThumbnailStorage>> loggerMock = new();
        StorageTestContext context = await CreateContextAsync(
            nameof(SaveAsync_WhenGeneratorThrows_LogsAndThrows),
            sourceImagePath =>
            {
                generatorMock
                    .Setup(generator => generator.CreateThumbnailAsync(
                        sourceImagePath,
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(exception);

                return generatorMock.Object;
            },
            loggerMock.Object);

        Func<Task> act = () => context.Storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            context.SourceImagePath,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        context.Storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId).Should().BeNull();
        VerifyWarningLogged(loggerMock, "Failed to create gallery thumbnail");
    }

    [Fact]
    public async Task BuildFileName_ForFullImageAndThumbnail_UsesGenerationImageFileNamePolicy()
    {
        GenerationImageFileNamePolicy fileNamePolicy = new();
        GalleryThumbnailImageFormat thumbnailImageFormat = new();
        StorageTestContext context = await CreateContextAsync(
            nameof(BuildFileName_ForFullImageAndThumbnail_UsesGenerationImageFileNamePolicy),
            fileNamePolicy: fileNamePolicy,
            thumbnailImageFormat: thumbnailImageFormat);
        string expectedFileName = fileNamePolicy.BuildFileName(
            BatchId,
            ItemId,
            thumbnailImageFormat.Extension);

        await context.Storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            context.SourceImagePath,
            CancellationToken.None);
        string? thumbnailPath = context.Storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

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

        await AssertSourceRejectedAsync(
            storage,
            string.Empty,
            generatorMock,
            loggerMock);
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

        await AssertSourceRejectedAsync(
            storage,
            sourceImagePath,
            generatorMock,
            loggerMock);
    }

    private static GalleryThumbnailStorage CreateStorage(
        AtomicArtDataPathProvider pathProvider,
        IGalleryThumbnailGenerator thumbnailGenerator,
        ILogger<GalleryThumbnailStorage>? logger = null,
        GenerationImageFileNamePolicy? fileNamePolicy = null,
        GalleryThumbnailImageFormat? thumbnailImageFormat = null)
    {
        logger ??= NullLogger<GalleryThumbnailStorage>.Instance;
        fileNamePolicy ??= new GenerationImageFileNamePolicy();
        thumbnailImageFormat ??= new GalleryThumbnailImageFormat();
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

    private static async Task<StorageTestContext> CreateContextAsync(
        string testName,
        Func<string, IGalleryThumbnailGenerator>? thumbnailGeneratorFactory = null,
        ILogger<GalleryThumbnailStorage>? logger = null,
        GenerationImageFileNamePolicy? fileNamePolicy = null,
        GalleryThumbnailImageFormat? thumbnailImageFormat = null)
    {
        string rootDirectory = CreateCleanDirectory(testName);
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        string sourceImagePath = await WriteSourceImageAsync(pathProvider);
        IGalleryThumbnailGenerator thumbnailGenerator =
            thumbnailGeneratorFactory?.Invoke(sourceImagePath)
            ?? CreateGenerator();
        GalleryThumbnailStorage storage = CreateStorage(
            pathProvider,
            thumbnailGenerator,
            logger,
            fileNamePolicy,
            thumbnailImageFormat);

        return new StorageTestContext(
            pathProvider,
            sourceImagePath,
            storage);
    }

    private static GalleryThumbnailGenerator CreateGenerator()
    {
        return new GalleryThumbnailGenerator(new GalleryThumbnailImageFormat());
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

    private static async Task AssertSourceRejectedAsync(
        GalleryThumbnailStorage storage,
        string sourceImagePath,
        Mock<IGalleryThumbnailGenerator> generatorMock,
        Mock<ILogger<GalleryThumbnailStorage>> loggerMock)
    {
        await storage.SaveAsync(
            BatchId,
            ItemId,
            ModelId,
            sourceImagePath,
            CancellationToken.None);
        string? thumbnailPath = storage.GetThumbnailPathOrDefault(BatchId, ItemId, ModelId);

        thumbnailPath.Should().BeNull();
        generatorMock.Verify(
            generator => generator.CreateThumbnailAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyWarningLogged(loggerMock, "Gallery thumbnail source image path is not trusted");
    }

    private static void VerifyWarningLogged(
        Mock<ILogger<GalleryThumbnailStorage>> loggerMock,
        string expectedMessage)
    {
        LoggerMockAssertions.VerifyLog(
            loggerMock,
            LogLevel.Warning,
            Times.AtLeastOnce(),
            expectedMessage);
    }

    private sealed class StorageTestContext
    {
        public AtomicArtDataPathProvider PathProvider { get; }
        public string SourceImagePath { get; }
        public GalleryThumbnailStorage Storage { get; }

        public StorageTestContext(
            AtomicArtDataPathProvider pathProvider,
            string sourceImagePath,
            GalleryThumbnailStorage storage)
        {
            PathProvider = pathProvider;
            SourceImagePath = sourceImagePath;
            Storage = storage;
        }
    }
}
