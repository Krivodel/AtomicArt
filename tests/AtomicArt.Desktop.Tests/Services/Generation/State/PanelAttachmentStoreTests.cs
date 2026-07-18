using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Tests.Common;

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
        await AssertSavedPathAsync(
            RawPanelId,
            attachmentPath => attachmentPath.Should().NotContain(RawPanelId));
    }

    [Fact]
    public async Task SaveAsync_WithUnsafePanelId_UsesEncodedPathSegment()
    {
        await AssertSavedPathAsync(
            UnsafePanelId,
            attachmentPath =>
            {
                attachmentPath.Should().NotContain("unsafe");
                attachmentPath.Should().NotContain("panel:id");
                attachmentPath.Should().NotContain("..");
            });
    }

    [Fact]
    public async Task SaveAsync_WithAttachment_WritesOutsideArtDirectory()
    {
        using PanelAttachmentTestContext context = new();

        PanelAttachmentState state = await SaveImageAsync(context, RawPanelId);

        string attachmentPath = context.GetAttachmentPath(state);
        Path.GetFullPath(attachmentPath).Should().StartWith(
            Path.GetFullPath(context.PathProvider.StateAttachmentsDirectory));
        Path.GetFullPath(attachmentPath).Should().NotStartWith(
            Path.GetFullPath(context.PathProvider.ArtDirectory));
        Directory.Exists(context.PathProvider.ArtDirectory).Should().BeFalse();
    }

    [Fact]
    public void CreateState_WithSourcePathLikeFileName_StoresOnlySafeDisplayName()
    {
        using PanelAttachmentTestContext context = new(@"C:\Users\Name\secret.png");

        PanelAttachmentState state = context.Store.CreateState(context.Image);

        state.FileName.Should().Be("secret.png");
        state.InternalFileName.Should().NotContain("secret");
        state.InternalFileName.Should().NotContain("Users");
    }

    [Fact]
    public async Task SaveAsync_WithRegisteredContentTypeAndArbitraryBytes_SavesWithoutAttachmentRuleValidation()
    {
        using PanelAttachmentTestContext context = new();

        PanelAttachmentState state = await SaveImageAsync(context, RawPanelId);

        state.SizeBytes.Should().Be(ImageBytes.LongLength);
    }

    [Fact]
    public async Task SaveAsync_WithUnsafeInternalFileName_RejectsAndDoesNotWriteOutsidePanelDirectory()
    {
        using PanelAttachmentTestContext context = new();
        PanelAttachmentState state = CreateState(
            "unsafe-attachment",
            "unsafe.png",
            @"..\x.png");

        Func<Task> act = async () => await context.Store.SaveAsync(
            RawPanelId,
            state,
            context.Image,
            CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        string escapedAttachmentPath = Path.Combine(
            context.PathProvider.StateAttachmentsDirectory,
            "x.png");
        File.Exists(escapedAttachmentPath).Should().BeFalse();
        Directory.Exists(Path.Combine(
            context.PathProvider.StateAttachmentsDirectory,
            EncodedPanelId)).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WithMissingManagedFile_LogsWarningAndReturnsNull()
    {
        PanelAttachmentState state = CreateState(
            "missing-attachment",
            "missing.png",
            "missing.png");

        await AssertLoadRejectedAsync(state, "missing.png");
    }

    [Fact]
    public async Task LoadAsync_WithUnsafeInternalFileName_LogsWarningAndReturnsNull()
    {
        PanelAttachmentState state = CreateState(
            "unsafe-attachment",
            "unsafe.png",
            @"..\x.png");

        await AssertLoadRejectedAsync(state, "..", "x.png");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingAttachment_RemovesManagedFile()
    {
        using PanelAttachmentTestContext context = new();
        PanelAttachmentState state = await SaveImageAsync(context, RawPanelId);
        string attachmentPath = context.GetAttachmentPath(state);

        await context.Store.DeleteAsync(RawPanelId, state, CancellationToken.None);

        File.Exists(attachmentPath).Should().BeFalse();
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

    private static AttachedImageDto CreateImage(string fileName = "source.png")
    {
        return new AttachedImageDto(
            fileName,
            GenerationImageContentTypes.Png,
            ImageBytes);
    }

    private static PanelAttachmentState CreateState(
        string id,
        string fileName,
        string internalFileName)
    {
        return new PanelAttachmentState
        {
            Id = id,
            FileName = fileName,
            ContentType = GenerationImageContentTypes.Png,
            SizeBytes = ImageBytes.LongLength,
            InternalFileName = internalFileName
        };
    }

    private static async Task AssertSavedPathAsync(
        string panelId,
        Action<string> assertPath)
    {
        using PanelAttachmentTestContext context = new();

        PanelAttachmentState state = await SaveImageAsync(context, panelId);

        string attachmentPath = context.GetAttachmentPath(state);
        File.Exists(attachmentPath).Should().BeTrue();
        attachmentPath.Should().Contain(EncodedPanelId);
        assertPath(attachmentPath);
    }

    private static async Task AssertLoadRejectedAsync(
        PanelAttachmentState state,
        params string[] sensitiveValues)
    {
        RecordingLogger<PanelAttachmentStore> logger = new RecordingLogger<PanelAttachmentStore>();
        using PanelAttachmentTestContext context = new(logger: logger);

        AttachedImageDto? image = await context.Store.LoadAsync(
            RawPanelId,
            state,
            CancellationToken.None);

        image.Should().BeNull();
        logger.WarningCount.Should().Be(1);
        logger.WarningMessages.Should().Contain(message => message.Contains(
            state.Id,
            StringComparison.Ordinal));

        foreach (string sensitiveValue in sensitiveValues)
        {
            logger.WarningMessages.Should().NotContain(message => message.Contains(
                sensitiveValue,
                StringComparison.Ordinal));
        }
    }

    private static async Task<PanelAttachmentState> SaveImageAsync(
        PanelAttachmentTestContext context,
        string panelId)
    {
        PanelAttachmentState state = context.Store.CreateState(context.Image);

        await context.Store.SaveAsync(
            panelId,
            state,
            context.Image,
            CancellationToken.None);

        return state;
    }

    private sealed class PanelAttachmentTestContext : IDisposable
    {
        public AtomicArtDataPathProvider PathProvider { get; }
        public PanelAttachmentStore Store { get; }
        public AttachedImageDto Image { get; }

        private readonly string _rootDirectory;

        public PanelAttachmentTestContext(
            string fileName = "source.png",
            ILogger<PanelAttachmentStore>? logger = null)
        {
            _rootDirectory = TestDirectories.GetUniqueDirectoryPath(
                typeof(PanelAttachmentStoreTests));
            PathProvider = new AtomicArtDataPathProvider(_rootDirectory);
            Store = CreateStore(PathProvider, logger);
            Image = CreateImage(fileName);
        }

        public string GetAttachmentPath(PanelAttachmentState state)
        {
            return Path.Combine(
                PathProvider.StateAttachmentsDirectory,
                EncodedPanelId,
                state.InternalFileName);
        }

        public void Dispose()
        {
            TestDirectories.DeleteIfExists(_rootDirectory);
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
            new IGenerationImageFormat[] { PngFormat };

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
}
