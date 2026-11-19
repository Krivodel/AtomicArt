using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Moq;
using Pica.Protocol;
using Pica.Viewer.Resources;
using Pica.Viewer.Services;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class PicaViewerSessionTests : DesktopControlTestBase
{
    private static readonly byte[] PngContent = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task PrepareAsync_WithFileThumbnail_PassesThumbnailToPica()
    {
        const string ModelId = "model";
        const string ImagePath = "images/source.png";
        const string ThumbnailPath = "images/thumbnail.webp";
        const string TrustedImagePath = "trusted/source.png";
        const string TrustedThumbnailPath = "trusted/thumbnail.webp";
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(ImagePath, ModelId))
            .Returns(TrustedImagePath);
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePathOrDefault(ThumbnailPath, ModelId))
            .Returns(TrustedThumbnailPath);
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryFileImageViewerSource(ModelId, ImagePath, ThumbnailPath),
            null);

        await using PicaViewerSession session = dependencies.CreateSession();
        await session.PrepareAsync(request, CancellationToken.None);

        PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
        preparedRequest.Items.Should().ContainSingle();
        preparedRequest.Items[0].FilePath.Should().Be(TrustedImagePath);
        preparedRequest.Items[0].IsTemporary.Should().BeFalse();
        preparedRequest.Items[0].PreviewFilePath.Should().Be(TrustedThumbnailPath);
        preparedRequest.Actions.Should().NotContain(action =>
            string.Equals(action.Id, AtomicArtPicaActions.ImbaId, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrepareAsync_WithSavedAttachment_UsesExistingFileAndKeepsItOnClose(bool fromHistory)
    {
        using TemporaryDirectory files = new(typeof(PicaViewerSessionTests), nameof(PrepareAsync_WithSavedAttachment_UsesExistingFileAndKeepsItOnClose));
        string path = Path.Combine(files.DirectoryPath, "stored-content.png");
        await File.WriteAllBytesAsync(path, PngContent);
        PicaViewerSessionTestDependencies dependencies = new();
        AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachCommand = new(
            _ => Task.CompletedTask);
        const string FileName = "reference.png";
        GalleryImageViewerSource source = fromHistory
            ? CreateReferenceSource(dependencies, path, FileName)
            : CreatePanelAttachmentSource(dependencies, path, FileName);
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(source, attachCommand);

        await using (PicaViewerSession session = dependencies.CreateSession())
        {
            await session.PrepareAsync(request, CancellationToken.None);

            PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
            PicaImageItem item = preparedRequest.Items.Should().ContainSingle().Subject;
            item.FilePath.Should().Be(path);
            item.FileName.Should().Be(FileName);
            item.IsTemporary.Should().BeFalse();
            preparedRequest.Actions.Should().Contain(action =>
                string.Equals(action.Id, AtomicArtPicaActions.AttachId, StringComparison.Ordinal));
            preparedRequest.Actions.Should().Contain(action =>
                string.Equals(action.Id, AtomicArtPicaActions.OpenDlss5Id, StringComparison.Ordinal));
            preparedRequest.Actions.Should().NotContain(action =>
                string.Equals(action.Id, AtomicArtPicaActions.ImbaId, StringComparison.Ordinal));
            preparedRequest.Actions.Should().NotContain(action =>
                string.Equals(action.Id, AtomicArtPicaActions.ShowInGalleryId, StringComparison.Ordinal));
        }

        byte[] storedContent = await File.ReadAllBytesAsync(path);
        storedContent.Should().Equal(PngContent);
    }

    [Fact]
    public async Task PrepareAsync_WithMissingNeighbour_KeepsSelectedReference()
    {
        const string StoredPath = "saved.png";
        PicaViewerSessionTestDependencies dependencies = new();
        GalleryImageViewerItem missing = new(
            Guid.NewGuid(), new GalleryReferenceImageViewerSource(
                new GenerationReference("missing", "missing.png", GenerationImageContentTypes.Png)));
        GalleryImageViewerItem selected = new(ItemId, CreateReferenceSource(dependencies, StoredPath, "reference.png"));
        GalleryImageViewerRequest request = new(
            new GalleryStaticImageViewerItemsSource(new GalleryImageViewerItem[] { missing, selected }),
            ItemId, AttachImagesCommand: null);
        await using PicaViewerSession session = dependencies.CreateSession();

        await session.PrepareAsync(request, CancellationToken.None);

        PicaViewerRequest prepared = PicaViewerSessionTests.GetPreparedRequest(session);
        prepared.Items.Should().ContainSingle().Which.Id.Should().Be(ItemId);
    }

    [Fact]
    public async Task PrepareAsync_WithMissingSelectedReference_ReportsUnavailableFile()
    {
        PicaViewerSessionTestDependencies dependencies = new();
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryReferenceImageViewerSource(
                new GenerationReference("missing", "missing.png", GenerationImageContentTypes.Png)),
            null);
        await using PicaViewerSession session = dependencies.CreateSession();

        Func<Task> prepare = () => session.PrepareAsync(request, CancellationToken.None);

        await prepare.Should().ThrowAsync<FileNotFoundException>();
        session.Request.Should().BeNull();
    }

    [Fact]
    public async Task PrepareAsync_WithGalleryFiles_AddsGalleryActionsInExpectedOrder()
    {
        const string ModelId = "model";
        const string ImagePath = "images/source.png";
        const string TrustedImagePath = "trusted/source.png";
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(ImagePath, ModelId))
            .Returns(TrustedImagePath);
        AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachCommand = new(
            _ => Task.CompletedTask);
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryFileImageViewerSource(ModelId, ImagePath, null),
            attachCommand,
            _ => Task.CompletedTask);

        await using PicaViewerSession session = dependencies.CreateSession();
        await session.PrepareAsync(request, CancellationToken.None);

        PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
        preparedRequest.Actions.Select(action => action.Id).Should().Equal(
            AtomicArtPicaActions.AttachId,
            AtomicArtPicaActions.ImbaId,
            AtomicArtPicaActions.OpenDlss5Id,
            AtomicArtPicaActions.ShowInGalleryId);
        PicaActionDefinition imbaAction = preparedRequest.Actions[1];
        imbaAction.DisplayName.Should().Be(
            TestLocalizationTextProvider.Default.Get(GalleryLocalizationKeys.Actions.Imba));
        imbaAction.IconGeometry.Should().Be(ViewerActionIconGeometry.Star);
        imbaAction.UseOutlineIcon.Should().BeTrue();
        imbaAction.Targets.Should().Be(PicaActionTargets.CurrentImage);
        imbaAction.Order.Should().Be(preparedRequest.Actions[0].Order + 1);
    }

    [Fact]
    public async Task GetCurrentImageActionDisplayName_WhenFavoriteChanges_UsesCurrentState()
    {
        const string ModelId = "model";
        const string ImagePath = "images/source.png";
        const string TrustedImagePath = "trusted/source.png";
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(ImagePath, ModelId))
            .Returns(TrustedImagePath);
        bool isFavorite = true;
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryFileImageViewerSource(ModelId, ImagePath, null),
            null,
            _ => Task.CompletedTask,
            () => isFavorite);

        await using PicaViewerSession session = dependencies.CreateSession();
        await session.PrepareAsync(request, CancellationToken.None);
        PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
        PicaActionDefinition action = preparedRequest.Actions.Single(candidate =>
            string.Equals(candidate.Id, AtomicArtPicaActions.ImbaId, StringComparison.Ordinal));
        PicaImageItem item = preparedRequest.Items.Single();

        string selectedDisplayName = session.GetCurrentImageActionDisplayName(action, item);
        isFavorite = false;
        string unselectedDisplayName = session.GetCurrentImageActionDisplayName(action, item);

        selectedDisplayName.Should().Be(TestLocalizationTextProvider.Default.Get(
            GalleryLocalizationKeys.Actions.RemoveImba));
        unselectedDisplayName.Should().Be(TestLocalizationTextProvider.Default.Get(
            GalleryLocalizationKeys.Actions.Imba));
    }

    [Fact]
    public async Task PrepareAsync_WithTemporaryFileSource_UsesFilePathWithoutGalleryAction()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            nameof(PicaViewerSessionTests),
            Guid.NewGuid().ToString("N"));
        string imagePath = Path.Combine(directoryPath, "comparison.png");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(imagePath, "dlss5"))
            .Returns(imagePath);
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryFileImageViewerSource(
                "dlss5",
                imagePath,
                DeleteImageWhenClosed: true),
            null);

        try
        {
            await using (PicaViewerSession session = dependencies.CreateSession())
            {
                await session.PrepareAsync(request, CancellationToken.None);

                PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
                preparedRequest.Items.Single().FilePath.Should().Be(imagePath);
                preparedRequest.Items.Single().IsTemporary.Should().BeTrue();
                preparedRequest.Actions.Should().NotContain(action =>
                    string.Equals(action.Id, AtomicArtPicaActions.ShowInGalleryId, StringComparison.Ordinal));
                preparedRequest.Actions.Should().NotContain(action =>
                    string.Equals(action.Id, AtomicArtPicaActions.ImbaId, StringComparison.Ordinal));
            }

            File.Exists(imagePath).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
    }

    [Fact]
    public async Task PrepareAsync_WithBitmapSource_UsesInProcessSourceWithoutMaterializingFile()
    {
        PicaViewerSessionTestDependencies dependencies = new();
        TestBitmapSource bitmapSource = new();
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryBitmapImageViewerSource(
                "dlss5",
                "dlss5-result.png",
                bitmapSource),
            null);

        await using PicaViewerSession session = dependencies.CreateSession();
        await session.PrepareAsync(request, CancellationToken.None);

        PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
        PicaImageItem item = preparedRequest.Items.Single();
        File.Exists(item.FilePath).Should().BeFalse();
        session.BitmapSources.Should().ContainKey(PicaViewerSessionTests.ItemId);
        session.BitmapSources[PicaViewerSessionTests.ItemId].Should().BeSameAs(bitmapSource);
        preparedRequest.Actions.Should().NotContain(action =>
            string.Equals(action.Id, AtomicArtPicaActions.ShowInGalleryId, StringComparison.Ordinal));
        preparedRequest.Actions.Should().NotContain(action =>
            string.Equals(action.Id, AtomicArtPicaActions.ImbaId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DispatchBitmapAsync_WithAttachAction_UsesDeferredBitmapInput()
    {
        await DispatchAsync(async () =>
        {
            PicaViewerSessionTestDependencies dependencies = new();
            dependencies.UiThreadDispatcher
                .Setup(dispatcher => dispatcher.InvokeAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Func<Task> action, CancellationToken _) => action());
            ImageAttachmentInput? receivedInput = null;
            AsyncRelayCommand<IReadOnlyList<ImageAttachmentInput>?> inputCommand = new(inputs =>
            {
                receivedInput = inputs?.Single();

                return Task.CompletedTask;
            });
            AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> imageCommand = new(_ =>
                Task.CompletedTask);
            GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
                new GalleryBitmapImageViewerSource(
                    "dlss5",
                    "result.png",
                    new TestBitmapSource()),
                imageCommand) with
            {
                AttachImageInputsCommand = inputCommand
            };
            using MemoryStream stream = new(PngContent, writable: false);
            using Bitmap bitmap = new(stream);

            await using PicaViewerSession session = dependencies.CreateSession();
            await session.PrepareAsync(request, CancellationToken.None);
            PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
            PicaImageItem item = preparedRequest.Items.Single();
            PicaActionDefinition action = preparedRequest.Actions.Single(candidate =>
                string.Equals(candidate.Id, AtomicArtPicaActions.AttachId, StringComparison.Ordinal));
            session.CanDispatchBitmapWithoutEncoding(action, item).Should().BeTrue();

            await session.DispatchBitmapAsync(
                action,
                item,
                bitmap,
                "result.png",
                CancellationToken.None);

            receivedInput.Should().NotBeNull();
            receivedInput?.FileName.Should().Be("result.png");
            Func<Task> readEncodedImage = () => receivedInput?.ReadAsync(CancellationToken.None)
                ?? throw new InvalidOperationException("The bitmap input was not received.");
            await readEncodedImage.Should().ThrowAsync<InvalidOperationException>();
        });
    }

    [Fact]
    public async Task DispatchCurrentImageAsync_WithImbaAction_ExecutesGalleryFavoriteAction()
    {
        const string ModelId = "model";
        const string ImagePath = "images/source.png";
        const string TrustedImagePath = "trusted/source.png";
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(ImagePath, ModelId))
            .Returns(TrustedImagePath);
        dependencies.UiThreadDispatcher
            .Setup(dispatcher => dispatcher.InvokeAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());
        int invocationCount = 0;
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryFileImageViewerSource(ModelId, ImagePath, null),
            null,
            _ =>
            {
                invocationCount++;
                return Task.CompletedTask;
            });

        await using PicaViewerSession session = dependencies.CreateSession();
        await session.PrepareAsync(request, CancellationToken.None);
        PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
        PicaActionDefinition imbaAction = preparedRequest.Actions.Single(action =>
            string.Equals(action.Id, AtomicArtPicaActions.ImbaId, StringComparison.Ordinal));

        await session.DispatchCurrentImageAsync(
            imbaAction,
            preparedRequest.Items.Single(),
            CancellationToken.None);

        invocationCount.Should().Be(1);
        dependencies.UiThreadDispatcher.Verify(dispatcher => dispatcher.InvokeAsync(
            It.IsAny<Func<Task>>(),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task DispatchCurrentImageAsync_WithAttachAction_ExecutesAtomicArtCommand()
    {
        const string ModelId = "model";
        const string SourceImagePath = "images/source.png";
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            nameof(PicaViewerSessionTests),
            Guid.NewGuid().ToString("N"));
        string trustedImagePath = Path.Combine(directoryPath, "source.png");
        byte[] imageContent = [1, 2, 3, 4];
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(trustedImagePath, imageContent);
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(SourceImagePath, ModelId))
            .Returns(trustedImagePath);
        dependencies.UiThreadDispatcher
            .Setup(dispatcher => dispatcher.InvokeAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());
        IReadOnlyList<AttachedImageDto>? attachedImages = null;
        AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachCommand = new(images =>
        {
            attachedImages = images;
            return Task.CompletedTask;
        });
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryFileImageViewerSource(ModelId, SourceImagePath, null),
            attachCommand);
        IGenerationImageFormatRegistry formatRegistry =
            GenerationImageFormatRegistryTestFactory.Create();

        try
        {
            await using PicaViewerSession session = dependencies.CreateSession(formatRegistry);
            await session.PrepareAsync(request, CancellationToken.None);
            PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
            PicaActionDefinition attachAction = preparedRequest.Actions.Single(action =>
                string.Equals(action.Id, AtomicArtPicaActions.AttachId, StringComparison.Ordinal));

            await session.DispatchCurrentImageAsync(
                attachAction,
                preparedRequest.Items.Single(),
                CancellationToken.None);

            attachedImages.Should().ContainSingle();
            attachedImages?[0].FileName.Should().Be("source.png");
            attachedImages?[0].ContentType.Should().Be(GenerationImageContentTypes.Png);
            attachedImages?[0].Content.Should().Equal(imageContent);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DispatchDerivedImageAsync_WithMemoryOnlyBitmap_AttachesPng(
        bool closeBeforeDispatch)
    {
        await DispatchAsync(async () =>
        {
            PicaViewerSessionTestDependencies dependencies = new();
            TestBitmapSource bitmapSource = new();
            dependencies.UiThreadDispatcher
                .Setup(dispatcher => dispatcher.InvokeAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Func<Task> action, CancellationToken _) => action());
            IReadOnlyList<AttachedImageDto>? attachedImages = null;
            AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachCommand = new(images =>
            {
                attachedImages = images;
                return Task.CompletedTask;
            });
            GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
                new GalleryBitmapImageViewerSource(
                    "dlss5",
                    "memory-only.jpg",
                    bitmapSource),
                attachCommand);

            await using PicaViewerSession session = dependencies.CreateSession();
            await session.PrepareAsync(request, CancellationToken.None);
            PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
            PicaActionDefinition attachAction = preparedRequest.Actions.Single(action =>
                string.Equals(action.Id, AtomicArtPicaActions.AttachId, StringComparison.Ordinal));

            if (closeBeforeDispatch)
            {
                await session.DisposeAsync();
            }

            await session.DispatchDerivedImageAsync(
                attachAction,
                preparedRequest.Items.Single(),
                "memory-only.png",
                PicaViewerSessionTests.PngContent,
                CancellationToken.None);

            attachedImages.Should().ContainSingle();
            attachedImages?[0].FileName.Should().Be("memory-only.png");
            attachedImages?[0].ContentType.Should().Be(GenerationImageContentTypes.Png);
            attachedImages?[0].Content.Should().Equal(PicaViewerSessionTests.PngContent);
            bitmapSource.AcquireCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task DispatchDerivedImageAsync_WithMemoryOnlyBitmap_OpensDlss5UsingTemporaryPng()
    {
        await DispatchAsync(async () =>
        {
            PicaViewerSessionTestDependencies dependencies = new();
            TestBitmapSource bitmapSource = new();
            string? stagingPath = null;
            dependencies.UiThreadDispatcher
                .Setup(dispatcher => dispatcher.InvokeAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task> action, CancellationToken _) => action());
            dependencies.Dlss5SourceOpener
                .Setup(opener => opener.OpenFromImagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((path, _) =>
                {
                    stagingPath = path;
                    using Bitmap image = new(path);
                    image.PixelSize.Should().Be(new PixelSize(1, 1));
                })
                .ReturnsAsync(true);
            GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
                new GalleryBitmapImageViewerSource("dlss5", "dlss5-result.png", bitmapSource), null);
            await using PicaViewerSession session = dependencies.CreateSession();
            await session.PrepareAsync(request, CancellationToken.None);
            PicaViewerRequest prepared = PicaViewerSessionTests.GetPreparedRequest(session);
            PicaActionDefinition action = prepared.Actions.Single(candidate =>
                string.Equals(candidate.Id, AtomicArtPicaActions.OpenDlss5Id, StringComparison.Ordinal));

            await session.DispatchDerivedImageAsync(
                action,
                prepared.Items.Single(),
                "derived-image.png",
                PicaViewerSessionTests.PngContent,
                CancellationToken.None);

            bitmapSource.AcquireCount.Should().Be(0);
            stagingPath.Should().NotBeNull();
            File.Exists(stagingPath).Should().BeFalse();
            dependencies.Dlss5SourceOpener.Verify(
                opener => opener.OpenFromImagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("canceled")]
    public async Task DispatchSelectionAsync_WithDlss5Action_CleansTemporaryPng(string outcome)
    {
        using CancellationTokenSource cancellation = new();
        PicaViewerSessionTestDependencies dependencies = new();
        string? stagingPath = null;
        dependencies.UiThreadDispatcher
            .Setup(dispatcher => dispatcher.InvokeAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());
        dependencies.Dlss5SourceOpener
            .Setup(opener => opener.OpenFromImagePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string path, CancellationToken _) =>
            {
                stagingPath = path;
                File.ReadAllBytes(path).Should().Equal(PicaViewerSessionTests.PngContent);

                if (string.Equals(outcome, "canceled", StringComparison.Ordinal))
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<bool>(cancellation.Token);
                }

                return string.Equals(outcome, "failure", StringComparison.Ordinal)
                    ? Task.FromException<bool>(new InvalidOperationException("Cannot open DLSS image."))
                    : Task.FromResult(true);
            });
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryBitmapImageViewerSource("dlss5", "result.png", new TestBitmapSource()), null);
        await using PicaViewerSession session = dependencies.CreateSession();
        await session.PrepareAsync(request, CancellationToken.None);
        PicaViewerRequest prepared = PicaViewerSessionTests.GetPreparedRequest(session);
        PicaActionDefinition action = prepared.Actions.Single(candidate => string.Equals(candidate.Id, AtomicArtPicaActions.OpenDlss5Id, StringComparison.Ordinal));
        Func<Task> dispatch = () => session.DispatchSelectionAsync(action, prepared.Items.Single(), PicaViewerSessionTests.PngContent, cancellation.Token);

        if (string.Equals(outcome, "canceled", StringComparison.Ordinal))
        {
            await dispatch.Should().ThrowAsync<OperationCanceledException>();
        }
        else if (string.Equals(outcome, "failure", StringComparison.Ordinal))
        {
            await dispatch.Should().ThrowAsync<InvalidOperationException>();
        }
        else
        {
            await dispatch();
        }

        action.Targets.HasFlag(PicaActionTargets.Selection).Should().BeTrue();
        action.SelectionPlacement.Should().Be(PicaSelectionActionPlacement.AfterSave);
        stagingPath.Should().NotBeNull();
        File.Exists(stagingPath).Should().BeFalse();
    }

    [Fact]
    public async Task DispatchCurrentImageAsync_WithDlss5Action_OpensCurrentImage()
    {
        const string ModelId = "model";
        const string SourceImagePath = "images/source.png";
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            nameof(PicaViewerSessionTests),
            Guid.NewGuid().ToString("N"));
        string trustedImagePath = Path.Combine(directoryPath, "source.png");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(trustedImagePath, [1, 2, 3, 4]);
        PicaViewerSessionTestDependencies dependencies = new();
        bool insideUiDispatcher = false;
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(SourceImagePath, ModelId))
            .Returns(trustedImagePath);
        dependencies.UiThreadDispatcher
            .Setup(dispatcher => dispatcher.InvokeAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (Func<Task> action, CancellationToken _) =>
            {
                insideUiDispatcher = true;
                try
                {
                    await action();
                }
                finally
                {
                    insideUiDispatcher = false;
                }
            });
        dependencies.UiThreadDispatcher
            .Setup(dispatcher => dispatcher.InvokeAsync(
                It.IsAny<Action>(),
                It.IsAny<CancellationToken>()))
            .Returns((Action action, CancellationToken _) =>
            {
                action();
                return Task.CompletedTask;
            });
        dependencies.Dlss5SourceOpener
            .Setup(opener => opener.OpenFromImagePathAsync(
                It.IsAny<string>(),
                CancellationToken.None))
            .Callback<string, CancellationToken>((path, _) =>
            {
                insideUiDispatcher.Should().BeTrue();
                File.ReadAllBytes(path).Should().Equal([1, 2, 3, 4]);
                using FileStream source = new(trustedImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                source.Length.Should().Be(4);
            })
            .ReturnsAsync(true);
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryFileImageViewerSource(ModelId, SourceImagePath, null),
            null);

        try
        {
            await using PicaViewerSession session = dependencies.CreateSession();
            await session.PrepareAsync(request, CancellationToken.None);
            PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
            PicaActionDefinition action = preparedRequest.Actions.Single(candidate =>
                string.Equals(candidate.Id, AtomicArtPicaActions.OpenDlss5Id, StringComparison.Ordinal));

            await session.DispatchCurrentImageAsync(
                action,
                preparedRequest.Items.Single(),
                CancellationToken.None);

            dependencies.Dlss5SourceOpener.Verify(opener => opener.OpenFromImagePathAsync(
                It.Is<string>(path => !string.Equals(path, trustedImagePath, StringComparison.Ordinal)),
                CancellationToken.None), Times.Once);
            dependencies.WindowStateService.Verify(service => service.ShowAndActivate(), Times.Once);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public async Task DispatchDerivedImageAsync_WithAttachAction_PreservesDerivedFileName()
    {
        const string DerivedFileName = "red-channel.png";
        byte[] derivedContent = [4, 3, 2, 1];
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.UiThreadDispatcher
            .Setup(dispatcher => dispatcher.InvokeAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());
        IReadOnlyList<AttachedImageDto>? attachedImages = null;
        AsyncRelayCommand<IReadOnlyList<AttachedImageDto>?> attachCommand = new(images =>
        {
            attachedImages = images;
            return Task.CompletedTask;
        });
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            CreatePanelAttachmentSource(dependencies, "stored.png", "reference.png"),
            attachCommand);

        await using PicaViewerSession session = dependencies.CreateSession();
        await session.PrepareAsync(request, CancellationToken.None);
        PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
        PicaActionDefinition attachAction = preparedRequest.Actions.Single(action =>
            string.Equals(action.Id, AtomicArtPicaActions.AttachId, StringComparison.Ordinal));

        await session.DispatchDerivedImageAsync(
            attachAction,
            preparedRequest.Items.Single(),
            DerivedFileName,
            derivedContent,
            CancellationToken.None);

        attachedImages.Should().ContainSingle();
        attachedImages?[0].FileName.Should().Be(DerivedFileName);
        attachedImages?[0].ContentType.Should().Be(GenerationImageContentTypes.Png);
        attachedImages?[0].Content.Should().Equal(derivedContent);
    }

    [Fact]
    public async Task DispatchCurrentImageAsync_WithShowInGalleryAction_RevealsItemAndActivatesWindow()
    {
        const string ModelId = "model";
        const string ImagePath = "images/source.png";
        const string TrustedImagePath = "trusted/source.png";
        PicaViewerSessionTestDependencies dependencies = new();
        dependencies.TrustedImageFileService
            .Setup(service => service.GetTrustedImagePath(ImagePath, ModelId))
            .Returns(TrustedImagePath);
        dependencies.UiThreadDispatcher
            .Setup(dispatcher => dispatcher.InvokeAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());
        GalleryImageViewerRequest request = PicaViewerSessionTests.CreateRequest(
            new GalleryFileImageViewerSource(ModelId, ImagePath, null),
            null);

        await using PicaViewerSession session = dependencies.CreateSession();
        await session.PrepareAsync(request, CancellationToken.None);
        PicaViewerRequest preparedRequest = PicaViewerSessionTests.GetPreparedRequest(session);
        PicaActionDefinition showInGalleryAction =
            preparedRequest.Actions.Single(action =>
                string.Equals(action.Id, AtomicArtPicaActions.ShowInGalleryId, StringComparison.Ordinal));

        await session.DispatchCurrentImageAsync(
            showInGalleryAction,
            preparedRequest.Items.Single(),
            CancellationToken.None);

        dependencies.WindowStateService.Verify(
            service => service.ShowAndActivate(),
            Times.Once);
        dependencies.GalleryOperations.Verify(
            operations => operations.RevealAsync(PicaViewerSessionTests.ItemId, CancellationToken.None),
            Times.Once);
    }

    private static GalleryReferenceImageViewerSource CreateReferenceSource(
        PicaViewerSessionTestDependencies dependencies, string path, string fileName)
    {
        GenerationReference reference = new("content-hash", fileName, GenerationImageContentTypes.Png);
        dependencies.References
            .Setup(store => store.ResolveExistingFilePath(reference))
            .Returns(path);

        return new GalleryReferenceImageViewerSource(reference);
    }

    private static GalleryAttachedImageViewerSource CreatePanelAttachmentSource(
        PicaViewerSessionTestDependencies dependencies, string path, string fileName)
    {
        const string PanelId = "panel";
        PanelAttachmentState attachment = new() { FileName = fileName };
        dependencies.PanelAttachments
            .Setup(store => store.ResolveExistingFilePath(PanelId, attachment))
            .Returns(path);

        return new GalleryAttachedImageViewerSource(PanelId, attachment);
    }

    private static GalleryImageViewerRequest CreateRequest(
        GalleryImageViewerSource source,
        IAsyncRelayCommand<IReadOnlyList<AttachedImageDto>?>? attachCommand,
        Func<CancellationToken, Task>? toggleFavoriteAsync = null,
        Func<bool>? getIsFavorite = null)
    {
        List<GalleryImageViewerItem> items =
        [
            new GalleryImageViewerItem(
                PicaViewerSessionTests.ItemId,
                source,
                toggleFavoriteAsync,
                getIsFavorite)
        ];

        return new GalleryImageViewerRequest(
            new GalleryStaticImageViewerItemsSource(items),
            PicaViewerSessionTests.ItemId,
            attachCommand);
    }

    private static PicaViewerRequest GetPreparedRequest(PicaViewerSession session)
    {
        return session.Request
            ?? throw new InvalidOperationException("The Pica request was not prepared.");
    }

    private sealed class TestBitmapSource : IPicaImageBitmapSource
    {
        public bool IsFileBacked => false;
        public int AcquireCount { get; private set; }

        public ValueTask<IPicaImageBitmapLease> AcquireAsync(
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            AcquireCount++;
            return ValueTask.FromResult<IPicaImageBitmapLease>(
                new TestBitmapLease());
        }

        private sealed class TestBitmapLease : IPicaImageBitmapLease
        {
            public Bitmap Bitmap { get; } = TestBitmapLease.CreateBitmap();

            public void Dispose()
            {
                Bitmap.Dispose();
            }

            private static Bitmap CreateBitmap()
            {
                using MemoryStream stream = new(PicaViewerSessionTests.PngContent, writable: false);
                return new Bitmap(stream);
            }
        }
    }
}
