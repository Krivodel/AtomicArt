using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;

using static AtomicArt.Desktop.Tests.Common.DesktopTestDirectories;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Deletion;

public sealed class GalleryItemDeletionServiceTests
{
    private const string ModelId = "test-model";

    private static readonly Guid BatchId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid ItemId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid OtherItemId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task DeleteFilesAsync_WithImageAndThumbnail_DeletesBothFiles()
    {
        string directory = CreateCleanDirectory(nameof(DeleteFilesAsync_WithImageAndThumbnail_DeletesBothFiles));
        string imagePath = await WriteManagedFileAsync(directory);
        string thumbnailPath = await WriteManagedFileAsync(directory);
        GalleryItemDeletionService service = CreateService(new PassthroughTrustedImageFileService());
        GalleryItemDeletionRequest request = CreateRequest(imagePath, thumbnailPath);

        await service.DeleteFilesAsync(request, CancellationToken.None);

        File.Exists(imagePath).Should().BeFalse();
        File.Exists(thumbnailPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFilesAsync_WithMissingFiles_Completes()
    {
        string directory = CreateCleanDirectory(nameof(DeleteFilesAsync_WithMissingFiles_Completes));
        AtomicArtDataPathProvider pathProvider = new(directory);
        string imagePath = Path.Combine(pathProvider.ArtDirectory, BuildManagedFileName(ItemId));
        string thumbnailPath = Path.Combine(pathProvider.ThumbnailsDirectory, BuildManagedFileName(ItemId));
        (
            GalleryItemDeletionService service,
            Mock<ILogger<GalleryItemDeletionService>> loggerMock
        ) = CreateRealServiceWithLogger(pathProvider);
        GalleryItemDeletionRequest request = CreateRequest(imagePath, thumbnailPath);

        await VerifyDeletionDoesNotThrowAsync(service, request);

        VerifyNoWarningLogged(loggerMock);
    }

    [Fact]
    public async Task DeleteFilesAsync_WithMissingFile_ValidatesTrustBeforeTreatingAsDeleted()
    {
        Mock<ILogger<GalleryItemDeletionService>> loggerMock =
            await DeleteMissingFileAsync(
                nameof(DeleteFilesAsync_WithMissingFile_ValidatesTrustBeforeTreatingAsDeleted),
                ModelId);

        VerifyNoWarningLogged(loggerMock);
    }

    [Fact]
    public async Task DeleteFilesAsync_WithUntrustedPath_DoesNotDeleteFile()
    {
        string directory = CreateCleanDirectory(nameof(DeleteFilesAsync_WithUntrustedPath_DoesNotDeleteFile));
        string imagePath = await WriteManagedFileAsync(directory);
        (
            GalleryItemDeletionService service,
            Mock<ILogger<GalleryItemDeletionService>> loggerMock
        ) = CreateServiceWithLogger(new RejectingTrustedImageFileService());

        await VerifyRejectedDeletionAsync(
            service,
            loggerMock,
            imagePath,
            "path is not trusted");
    }

    [Fact]
    public async Task DeleteFilesAsync_WithUntrustedPath_DoesNotThrow()
    {
        string directory = CreateCleanDirectory(nameof(DeleteFilesAsync_WithUntrustedPath_DoesNotThrow));
        string imagePath = await WriteManagedFileAsync(directory);
        GalleryItemDeletionService service = CreateService(new RejectingTrustedImageFileService());
        GalleryItemDeletionRequest request = CreateRequest(imagePath);

        await VerifyDeletionDoesNotThrowAsync(service, request);

        File.Exists(imagePath).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteFilesAsync_WithMissingUntrustedFile_ValidatesTrustAndLogsWarning()
    {
        Mock<ILogger<GalleryItemDeletionService>> loggerMock =
            await DeleteMissingFileAsync(
                nameof(DeleteFilesAsync_WithMissingUntrustedFile_ValidatesTrustAndLogsWarning),
                "other-model");

        VerifyWarningLogged(loggerMock, "path is not trusted");
    }

    [Fact]
    public async Task DeleteFilesAsync_WithModelScopedTrustedPath_DeletesFile()
    {
        string directory = CreateCleanDirectory(nameof(DeleteFilesAsync_WithModelScopedTrustedPath_DeletesFile));
        string imagePath = await WriteManagedFileAsync(directory);
        ModelScopedTrustedImageFileService trustedImageFileService = new(ModelId);
        GalleryItemDeletionService service = CreateService(trustedImageFileService);
        GalleryItemDeletionRequest request = CreateRequest(imagePath);

        await service.DeleteFilesAsync(request, CancellationToken.None);

        trustedImageFileService.DeletionModelIds.Should().ContainSingle().Which.Should().Be(ModelId);
        File.Exists(imagePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFilesAsync_WithRealTrustedService_DeletesManagedFile()
    {
        string directory = CreateCleanDirectory(nameof(DeleteFilesAsync_WithRealTrustedService_DeletesManagedFile));
        AtomicArtDataPathProvider pathProvider = new(directory);
        GalleryItemDeletionService service = CreateRealService(pathProvider);
        string imagePath = await WriteManagedPngAsync(pathProvider, ItemId);
        GalleryItemDeletionRequest request = CreateRequest(imagePath);

        await service.DeleteFilesAsync(request, CancellationToken.None);

        File.Exists(imagePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteFilesAsync_WhenFileDeleteFails_LogsAndContinues()
    {
        string directory = CreateCleanDirectory(
            nameof(DeleteFilesAsync_WhenFileDeleteFails_LogsAndContinues));
        AtomicArtDataPathProvider pathProvider = new(directory);
        Directory.CreateDirectory(pathProvider.ArtDirectory);
        Directory.CreateDirectory(pathProvider.ThumbnailsDirectory);
        string imagePath = await WriteManagedFileAsync(pathProvider.ArtDirectory);
        string thumbnailPath = await WriteManagedFileAsync(pathProvider.ThumbnailsDirectory);
        (
            GalleryItemDeletionService service,
            Mock<ILogger<GalleryItemDeletionService>> loggerMock
        ) = CreateRealServiceWithLogger(pathProvider);
        GalleryItemDeletionRequest request = CreateRequest(imagePath, thumbnailPath);

        await using FileStream lockedImage = new(
            imagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        await VerifyDeletionDoesNotThrowAsync(service, request);

        File.Exists(imagePath).Should().BeTrue();
        File.Exists(thumbnailPath).Should().BeFalse();
        VerifyErrorLogged(loggerMock, "Failed to delete gallery item");
    }

    [Fact]
    public async Task DeleteFilesAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        string directory = CreateCleanDirectory(nameof(DeleteFilesAsync_WhenCanceled_ThrowsOperationCanceledException));
        string imagePath = Path.Combine(directory, BuildManagedFileName(ItemId));
        GalleryItemDeletionService service = CreateService(new PassthroughTrustedImageFileService());
        GalleryItemDeletionRequest request = CreateRequest(imagePath);
        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        Func<Task> act = CreateDeleteAction(service, request, cancellationTokenSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DeleteFilesAsync_WithTrustedPathForOtherItem_DoesNotDeleteFile()
    {
        string directory = CreateCleanDirectory(nameof(DeleteFilesAsync_WithTrustedPathForOtherItem_DoesNotDeleteFile));
        AtomicArtDataPathProvider pathProvider = new(directory);
        (
            GalleryItemDeletionService service,
            Mock<ILogger<GalleryItemDeletionService>> loggerMock
        ) = CreateRealServiceWithLogger(pathProvider);
        string imagePath = await WriteManagedPngAsync(pathProvider, OtherItemId);

        await VerifyRejectedDeletionAsync(
            service,
            loggerMock,
            imagePath,
            "does not belong to the item",
            exceptionExpected: false);
    }

    [Fact]
    public async Task DeleteFilesAsync_WhenResolvedPathBelongsToOtherItem_DoesNotDeleteFile()
    {
        string directory = CreateCleanDirectory(
            nameof(DeleteFilesAsync_WhenResolvedPathBelongsToOtherItem_DoesNotDeleteFile));
        string imagePath = await WriteManagedFileAsync(directory);
        string otherItemPath = await WriteManagedFileAsync(directory, OtherItemId);
        ResolvedPathTrustedImageFileService trustedImageFileService = new(otherItemPath);
        (
            GalleryItemDeletionService service,
            Mock<ILogger<GalleryItemDeletionService>> loggerMock
        ) = CreateServiceWithLogger(trustedImageFileService);

        await VerifyRejectedDeletionAsync(
            service,
            loggerMock,
            imagePath,
            "path is not trusted");

        trustedImageFileService.ValidateResolvedPathCallCount.Should().Be(1);
        File.Exists(otherItemPath).Should().BeTrue();
    }

    private static GalleryItemDeletionService CreateService(ITrustedImageFileService trustedImageFileService)
    {
        return CreateService(
            trustedImageFileService,
            Mock.Of<ILogger<GalleryItemDeletionService>>());
    }

    private static GalleryItemDeletionService CreateService(
        ITrustedImageFileService trustedImageFileService,
        ILogger<GalleryItemDeletionService> logger)
    {
        return new GalleryItemDeletionService(
            trustedImageFileService,
            new GenerationImageFileNamePolicy(),
            logger);
    }

    private static GalleryItemDeletionService CreateRealService(
        AtomicArtDataPathProvider pathProvider)
    {
        return CreateService(CreateRealTrustedImageFileService(pathProvider));
    }

    private static (
        GalleryItemDeletionService Service,
        Mock<ILogger<GalleryItemDeletionService>> LoggerMock
    ) CreateRealServiceWithLogger(AtomicArtDataPathProvider pathProvider)
    {
        return CreateServiceWithLogger(CreateRealTrustedImageFileService(pathProvider));
    }

    private static (
        GalleryItemDeletionService Service,
        Mock<ILogger<GalleryItemDeletionService>> LoggerMock
    ) CreateServiceWithLogger(ITrustedImageFileService trustedImageFileService)
    {
        Mock<ILogger<GalleryItemDeletionService>> loggerMock = new();
        GalleryItemDeletionService service = CreateService(
            trustedImageFileService,
            loggerMock.Object);

        return (service, loggerMock);
    }

    private static TrustedImageFileService CreateRealTrustedImageFileService(
        AtomicArtDataPathProvider pathProvider)
    {
        return new TrustedImageFileService(
            pathProvider,
            GenerationImageFormatRegistryTestFactory.Create(),
            NullLogger<TrustedImageFileService>.Instance);
    }

    private static GalleryItemDeletionRequest CreateRequest(
        string imagePath,
        string? thumbnailPath = null)
    {
        return new GalleryItemDeletionRequest(
            ItemId,
            ModelId,
            imagePath,
            thumbnailPath);
    }

    private static Func<Task> CreateDeleteAction(
        GalleryItemDeletionService service,
        GalleryItemDeletionRequest request)
    {
        return CreateDeleteAction(service, request, CancellationToken.None);
    }

    private static Func<Task> CreateDeleteAction(
        GalleryItemDeletionService service,
        GalleryItemDeletionRequest request,
        CancellationToken ct)
    {
        return () => service.DeleteFilesAsync(request, ct);
    }

    private static async Task VerifyDeletionDoesNotThrowAsync(
        GalleryItemDeletionService service,
        GalleryItemDeletionRequest request)
    {
        Func<Task> act = CreateDeleteAction(service, request);

        await act.Should().NotThrowAsync().ConfigureAwait(false);
    }

    private static async Task VerifyRejectedDeletionAsync(
        GalleryItemDeletionService service,
        Mock<ILogger<GalleryItemDeletionService>> loggerMock,
        string imagePath,
        string expectedMessage,
        bool exceptionExpected = true)
    {
        GalleryItemDeletionRequest request = CreateRequest(imagePath);

        await service
            .DeleteFilesAsync(request, CancellationToken.None)
            .ConfigureAwait(false);

        File.Exists(imagePath).Should().BeTrue();
        VerifyWarningLogged(loggerMock, expectedMessage, exceptionExpected);
    }

    private static string BuildManagedFileName(Guid itemId)
    {
        GenerationImageFileNamePolicy fileNamePolicy = new();

        return fileNamePolicy.BuildFileName(BatchId, itemId, ".png");
    }

    private static async Task<string> WriteFileAsync(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, [0x01, 0x02, 0x03]);

        return path;
    }

    private static Task<string> WriteManagedFileAsync(string directory)
    {
        return WriteManagedFileAsync(directory, ItemId);
    }

    private static Task<string> WriteManagedFileAsync(string directory, Guid itemId)
    {
        return WriteFileAsync(directory, BuildManagedFileName(itemId));
    }

    private static async Task<string> WriteManagedPngAsync(
        AtomicArtDataPathProvider pathProvider,
        Guid itemId)
    {
        Directory.CreateDirectory(pathProvider.ArtDirectory);
        string imagePath = Path.Combine(
            pathProvider.ArtDirectory,
            BuildManagedFileName(itemId));
        await File
            .WriteAllBytesAsync(imagePath, GenerationImageTestData.ValidPngBytes)
            .ConfigureAwait(false);

        return imagePath;
    }

    private static async Task<Mock<ILogger<GalleryItemDeletionService>>> DeleteMissingFileAsync(
        string testName,
        string trustedModelId)
    {
        string directory = CreateCleanDirectory(testName);
        string imagePath = Path.Combine(directory, BuildManagedFileName(ItemId));
        ModelScopedTrustedImageFileService trustedImageFileService = new(trustedModelId);
        (
            GalleryItemDeletionService service,
            Mock<ILogger<GalleryItemDeletionService>> loggerMock
        ) = CreateServiceWithLogger(trustedImageFileService);
        GalleryItemDeletionRequest request = CreateRequest(imagePath);

        await service
            .DeleteFilesAsync(request, CancellationToken.None)
            .ConfigureAwait(false);

        trustedImageFileService.DeletionModelIds
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .Be(ModelId);
        trustedImageFileService.ReadModelIds.Should().BeEmpty();

        return loggerMock;
    }

    private static void VerifyWarningLogged(
        Mock<ILogger<GalleryItemDeletionService>> loggerMock,
        string expectedMessage,
        bool exceptionExpected = true)
    {
        Func<Exception?, bool> exceptionPredicate = exceptionExpected
            ? exception => exception is InvalidOperationException
            : exception => exception is null;

        LoggerMockAssertions.VerifyLog(
            loggerMock,
            LogLevel.Warning,
            Times.AtLeastOnce(),
            expectedMessage,
            exceptionPredicate);
    }

    private static void VerifyNoWarningLogged(Mock<ILogger<GalleryItemDeletionService>> loggerMock)
    {
        LoggerMockAssertions.VerifyLog(
            loggerMock,
            LogLevel.Warning,
            Times.Never());
    }

    private static void VerifyErrorLogged(
        Mock<ILogger<GalleryItemDeletionService>> loggerMock,
        string expectedMessage)
    {
        LoggerMockAssertions.VerifyLog(
            loggerMock,
            LogLevel.Error,
            Times.AtLeastOnce(),
            expectedMessage);
    }

    private sealed class ModelScopedTrustedImageFileService : TrustedImageFileServiceTestDouble
    {
        private readonly string _trustedModelId;
        private readonly List<string> _readModelIds = [];
        private readonly List<string> _deletionModelIds = [];

        public IReadOnlyList<string> ReadModelIds => _readModelIds;
        public IReadOnlyList<string> DeletionModelIds => _deletionModelIds;

        public ModelScopedTrustedImageFileService(string trustedModelId)
        {
            _trustedModelId = trustedModelId;
        }

        public override string? GetTrustedImagePathOrDefault(string? path, string modelId)
        {
            _readModelIds.Add(modelId);

            return GetTrustedPathOrDefault(path, modelId);
        }

        public override void DeleteTrustedImageFileIfExists(
            string? path,
            string modelId,
            Action<string> validateResolvedPath)
        {
            _deletionModelIds.Add(modelId);

            string trustedPath = GetRequiredTrustedPath(GetTrustedPathOrDefault(path, modelId));

            if (File.Exists(trustedPath))
            {
                validateResolvedPath(trustedPath);
                File.Delete(trustedPath);
            }
        }

        private string? GetTrustedPathOrDefault(string? path, string modelId)
        {
            if (string.Equals(modelId, _trustedModelId, StringComparison.Ordinal))
            {
                return path;
            }

            return null;
        }
    }

    private sealed class ResolvedPathTrustedImageFileService : TrustedImageFileServiceTestDouble
    {
        private readonly string _resolvedPath;

        public int ValidateResolvedPathCallCount { get; private set; }

        public ResolvedPathTrustedImageFileService(string resolvedPath)
        {
            _resolvedPath = resolvedPath;
        }

        public override string? GetTrustedImagePathOrDefault(string? path, string modelId)
        {
            return path;
        }

        public override void DeleteTrustedImageFileIfExists(
            string? path,
            string modelId,
            Action<string> validateResolvedPath)
        {
            ValidateResolvedPathCallCount++;
            validateResolvedPath(_resolvedPath);

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
