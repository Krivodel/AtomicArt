using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests.Services.Generation.State;

public sealed class PanelAttachmentStoreTests
{
    private const string RawPanelId = "raw-panel-id";
    private const string UnsafePanelId = @"..\unsafe/panel:id";
    private const string EncodedPanelId = "encoded-panel-key";
    private static readonly byte[] ImageBytes = [0x00, 0x01, 0x02];

    [Fact]
    public async Task SaveAsync_WithPanelId_WritesUnderEncodedStateAttachmentsDirectory()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            PanelAttachmentStore store = CreateStore(pathProvider);
            AttachedImageDto image = new(
                "source.png",
                GenerationImageContentTypes.Png,
                ImageBytes);

            PanelAttachmentState state = store.CreateState(image);
            await store.SaveAsync(RawPanelId, state, image, CancellationToken.None);

            string panelDirectory = Path.Combine(pathProvider.StateAttachmentsDirectory, EncodedPanelId);
            string attachmentPath = Path.Combine(panelDirectory, state.InternalFileName);
            File.Exists(attachmentPath).Should().BeTrue();
            attachmentPath.Should().Contain(EncodedPanelId);
            attachmentPath.Should().NotContain(RawPanelId);
        }
        finally
        {
            DeleteTempRoot(rootDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WithUnsafePanelId_UsesEncodedPathSegment()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            PanelAttachmentStore store = CreateStore(pathProvider);
            AttachedImageDto image = new(
                "source.png",
                GenerationImageContentTypes.Png,
                ImageBytes);

            PanelAttachmentState state = store.CreateState(image);
            await store.SaveAsync(UnsafePanelId, state, image, CancellationToken.None);

            string panelDirectory = Path.Combine(pathProvider.StateAttachmentsDirectory, EncodedPanelId);
            string attachmentPath = Path.Combine(panelDirectory, state.InternalFileName);
            File.Exists(attachmentPath).Should().BeTrue();
            attachmentPath.Should().Contain(EncodedPanelId);
            attachmentPath.Should().NotContain("unsafe");
            attachmentPath.Should().NotContain("panel:id");
            attachmentPath.Should().NotContain("..");
        }
        finally
        {
            DeleteTempRoot(rootDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WithAttachment_WritesOutsideArtDirectory()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            PanelAttachmentStore store = CreateStore(pathProvider);
            AttachedImageDto image = new(
                "source.png",
                GenerationImageContentTypes.Png,
                ImageBytes);

            PanelAttachmentState state = store.CreateState(image);
            await store.SaveAsync(RawPanelId, state, image, CancellationToken.None);

            string attachmentPath = Path.Combine(
                pathProvider.StateAttachmentsDirectory,
                EncodedPanelId,
                state.InternalFileName);
            Path.GetFullPath(attachmentPath).Should().StartWith(Path.GetFullPath(pathProvider.StateAttachmentsDirectory));
            Path.GetFullPath(attachmentPath).Should().NotStartWith(Path.GetFullPath(pathProvider.ArtDirectory));
            Directory.Exists(pathProvider.ArtDirectory).Should().BeFalse();
        }
        finally
        {
            DeleteTempRoot(rootDirectory);
        }
    }

    [Fact]
    public void CreateState_WithSourcePathLikeFileName_StoresOnlySafeDisplayName()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            PanelAttachmentStore store = CreateStore(pathProvider);
            AttachedImageDto image = new(
                @"C:\Users\Name\secret.png",
                GenerationImageContentTypes.Png,
                ImageBytes);

            PanelAttachmentState state = store.CreateState(image);

            state.FileName.Should().Be("secret.png");
            state.InternalFileName.Should().NotContain("secret");
            state.InternalFileName.Should().NotContain("Users");
        }
        finally
        {
            DeleteTempRoot(rootDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WithRegisteredContentTypeAndArbitraryBytes_SavesWithoutAttachmentRuleValidation()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            PanelAttachmentStore store = CreateStore(pathProvider);
            AttachedImageDto image = new(
                "source.png",
                GenerationImageContentTypes.Png,
                ImageBytes);

            PanelAttachmentState state = store.CreateState(image);
            await store.SaveAsync(RawPanelId, state, image, CancellationToken.None);

            state.SizeBytes.Should().Be(ImageBytes.LongLength);
        }
        finally
        {
            DeleteTempRoot(rootDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WithUnsafeInternalFileName_RejectsAndDoesNotWriteOutsidePanelDirectory()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            PanelAttachmentStore store = CreateStore(pathProvider);
            AttachedImageDto image = new(
                "source.png",
                GenerationImageContentTypes.Png,
                ImageBytes);
            PanelAttachmentState state = new()
            {
                Id = "unsafe-attachment",
                FileName = "unsafe.png",
                ContentType = GenerationImageContentTypes.Png,
                SizeBytes = ImageBytes.LongLength,
                InternalFileName = @"..\x.png"
            };

            Func<Task> act = async () => await store.SaveAsync(
                RawPanelId,
                state,
                image,
                CancellationToken.None);

            await act.Should().ThrowAsync<IOException>();
            string escapedAttachmentPath = Path.Combine(
                pathProvider.StateAttachmentsDirectory,
                "x.png");
            File.Exists(escapedAttachmentPath).Should().BeFalse();
            Directory.Exists(Path.Combine(
                pathProvider.StateAttachmentsDirectory,
                EncodedPanelId)).Should().BeFalse();
        }
        finally
        {
            DeleteTempRoot(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadAsync_WithMissingManagedFile_LogsWarningAndReturnsNull()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            RecordingLogger<PanelAttachmentStore> logger = new RecordingLogger<PanelAttachmentStore>();
            PanelAttachmentStore store = CreateStore(pathProvider, logger);
            PanelAttachmentState state = new()
            {
                Id = "missing-attachment",
                FileName = "missing.png",
                ContentType = GenerationImageContentTypes.Png,
                SizeBytes = ImageBytes.LongLength,
                InternalFileName = "missing.png"
            };

            AttachedImageDto? image = await store.LoadAsync(RawPanelId, state, CancellationToken.None);

            image.Should().BeNull();
            logger.WarningCount.Should().Be(1);
            logger.Messages.Should().Contain(message => message.Contains(
                state.Id,
                StringComparison.Ordinal));
            logger.Messages.Should().NotContain(message => message.Contains(
                "missing.png",
                StringComparison.Ordinal));
        }
        finally
        {
            DeleteTempRoot(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadAsync_WithUnsafeInternalFileName_LogsWarningAndReturnsNull()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            RecordingLogger<PanelAttachmentStore> logger = new RecordingLogger<PanelAttachmentStore>();
            PanelAttachmentStore store = CreateStore(pathProvider, logger);
            PanelAttachmentState state = new()
            {
                Id = "unsafe-attachment",
                FileName = "unsafe.png",
                ContentType = GenerationImageContentTypes.Png,
                SizeBytes = ImageBytes.LongLength,
                InternalFileName = @"..\x.png"
            };

            AttachedImageDto? image = await store.LoadAsync(RawPanelId, state, CancellationToken.None);

            image.Should().BeNull();
            logger.WarningCount.Should().Be(1);
            logger.Messages.Should().Contain(message => message.Contains(
                state.Id,
                StringComparison.Ordinal));
            logger.Messages.Should().NotContain(message => message.Contains(
                "..",
                StringComparison.Ordinal));
            logger.Messages.Should().NotContain(message => message.Contains(
                "x.png",
                StringComparison.Ordinal));
        }
        finally
        {
            DeleteTempRoot(rootDirectory);
        }
    }

    [Fact]
    public async Task DeleteAsync_WithExistingAttachment_RemovesManagedFile()
    {
        string rootDirectory = CreateTempRoot();

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            PanelAttachmentStore store = CreateStore(pathProvider);
            AttachedImageDto image = new(
                "source.png",
                GenerationImageContentTypes.Png,
                ImageBytes);
            PanelAttachmentState state = store.CreateState(image);
            await store.SaveAsync(RawPanelId, state, image, CancellationToken.None);
            string attachmentPath = Path.Combine(
                pathProvider.StateAttachmentsDirectory,
                EncodedPanelId,
                state.InternalFileName);

            await store.DeleteAsync(RawPanelId, state, CancellationToken.None);

            File.Exists(attachmentPath).Should().BeFalse();
        }
        finally
        {
            DeleteTempRoot(rootDirectory);
        }
    }

    private static PanelAttachmentStore CreateStore(
        AtomicArtDataPathProvider pathProvider,
        ILogger<PanelAttachmentStore>? logger = null)
    {
        return new PanelAttachmentStore(
            pathProvider,
            new FixedStatePathKeyEncoder(),
            new TestGenerationImageFormatRegistry(),
            logger ?? NullLogger<PanelAttachmentStore>.Instance);
    }

    private static string CreateTempRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.PanelAttachmentStoreTests",
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteTempRoot(string rootDirectory)
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private sealed class FixedStatePathKeyEncoder : IStatePathKeyEncoder
    {
        public string Encode(string key)
        {
            return EncodedPanelId;
        }
    }

    private sealed class TestGenerationImageFormatRegistry : IGenerationImageFormatRegistry
    {
        private static readonly IGenerationImageFormat PngFormat = new TestGenerationImageFormat();

        public IReadOnlyCollection<IGenerationImageFormat> Formats { get; } =
            [PngFormat];

        public bool TryGetByContentType(string? contentType, out IGenerationImageFormat? format)
        {
            format = string.Equals(contentType, GenerationImageContentTypes.Png, StringComparison.OrdinalIgnoreCase)
                ? PngFormat
                : null;

            return format is not null;
        }

        public bool TryGetByFileName(string fileName, out IGenerationImageFormat? format)
        {
            format = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? PngFormat
                : null;

            return format is not null;
        }
    }

    private sealed class TestGenerationImageFormat : IGenerationImageFormat
    {
        public string ContentType => GenerationImageContentTypes.Png;
        public string Extension => ".png";

        public bool MatchesContentType(string contentType)
        {
            return string.Equals(contentType, ContentType, StringComparison.OrdinalIgnoreCase);
        }

        public bool MatchesFileName(string fileName)
        {
            return fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
        }

        public bool MatchesSignature(ReadOnlySpan<byte> bytes)
        {
            return true;
        }
    }

    private sealed class RecordingLogger<TCategory> : ILogger<TCategory>
    {
        public int WarningCount { get; private set; }
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                WarningCount++;
                Messages.Add(formatter(state, exception));
            }
        }
    }
}
