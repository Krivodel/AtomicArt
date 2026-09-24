using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using Pica.Viewer.Services;
using SkiaSharp;
using SukiUI.Toasts;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Behaviors;
using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.ViewModels.Dlss5;
using AtomicArt.Desktop.Views.Dlss5;

namespace AtomicArt.Desktop.Tests.ViewModels.Dlss5;

public sealed class Dlss5SessionViewModelTests : DesktopControlTestBase
{
    [Fact]
    public async Task OpenAsync_AfterFreshDownloadAndOkay_DoesNotStartDlssRuntime()
    {
        using Dlss5SessionTestContext context = new(isInstalled: false);
        context.DialogService
            .Setup(service => service.ShowConfirmationAsync(
                It.IsAny<LocalizedConfirmationDialogRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Task<bool> openTask = context.ViewModel.OpenAsync(CancellationToken.None);

        context.ViewModel.IsInstallCompleted.Should().BeTrue();
        openTask.IsCompleted.Should().BeFalse();
        context.NativeEngine.Verify(
            engine => engine.InitializeAsync(
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.ViewModel.CompleteInstallNotification(launch: false);
        bool opened = await openTask;

        opened.Should().BeFalse();
        context.ViewModel.IsOpen.Should().BeFalse();
        context.ViewModel.IsInstallCompleted.Should().BeFalse();
        context.ModuleInstaller.Verify(
            installer => installer.EnsureInstalledAsync(
                It.IsAny<IProgress<Dlss5ModuleInstallProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.NativeEngine.Verify(
            engine => engine.InitializeAsync(
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        context.DialogService.Verify(
            service => service.ShowConfirmationAsync(
                It.IsAny<LocalizedConfirmationDialogRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenAsync_AfterFreshDownloadAndLaunch_StartsDlssRuntime()
    {
        using Dlss5SessionTestContext context = new(isInstalled: false);
        context.DialogService
            .Setup(service => service.ShowConfirmationAsync(
                It.IsAny<LocalizedConfirmationDialogRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Task<bool> openTask = context.ViewModel.OpenAsync(CancellationToken.None);

        context.ViewModel.IsInstallCompleted.Should().BeTrue();
        openTask.IsCompleted.Should().BeFalse();

        context.ViewModel.CompleteInstallNotification(launch: true);
        bool opened = await openTask;

        opened.Should().BeTrue();
        context.ViewModel.IsOpen.Should().BeTrue();
        context.ViewModel.IsInstallCompleted.Should().BeFalse();
        context.ModuleInstaller.Verify(
            installer => installer.EnsureInstalledAsync(
                It.IsAny<IProgress<Dlss5ModuleInstallProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.NativeEngine.Verify(
            engine => engine.InitializeAsync(
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OperationToast_AfterFreshDownload_ShowsOkayAndLaunchActions()
    {
        await DispatchAsync(async () =>
        {
            using Dlss5SessionTestContext context = new(isInstalled: false);
            context.DialogService
                .Setup(service => service.ShowConfirmationAsync(
                    It.IsAny<LocalizedConfirmationDialogRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            using SukiToastManager manager = new();
            Mock<ILocalizationTextProvider> textProvider = new();
            textProvider
                .Setup(provider => provider.Get(It.IsAny<string>()))
                .Returns((string key) => key);
            List<ISukiToast> queuedToasts = [];
            manager.OnToastQueued += (_, eventArgs) => queuedToasts.Add(eventArgs.Toast);
            using Dlss5OperationToastPresenter presenter = new(
                manager,
                textProvider.Object);
            presenter.Attach(context.ViewModel);

            Task<bool> openTask = context.ViewModel.OpenAsync(CancellationToken.None);

            ISukiToast completionToast = queuedToasts.Single(toast =>
                string.Equals(
                    toast.Title,
                    Dlss5LocalizationKeys.InstallCompleted.Title,
                    StringComparison.Ordinal));
            completionToast.Title.Should().Be(Dlss5LocalizationKeys.InstallCompleted.Title);
            completionToast.Content.Should().BeOfType<TextBlock>()
                .Which.Text.Should().Be(Dlss5LocalizationKeys.InstallCompleted.Message);
            Button[] actionButtons = completionToast.ActionButtons.OfType<Button>().ToArray();
            actionButtons.Select(button => button.Content).Should().Equal(
                Dlss5LocalizationKeys.InstallCompleted.Okay,
                Dlss5LocalizationKeys.InstallCompleted.Launch);

            context.ViewModel.CompleteInstallNotification(launch: true);
            bool opened = await openTask;

            opened.Should().BeTrue();
            context.NativeEngine.Verify(
                engine => engine.InitializeAsync(
                    It.IsAny<IProgress<int>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        });
    }

    [Fact]
    public async Task RestoreAsync_WithSavedSource_DoesNotStartDlssRuntime()
    {
        using Dlss5SessionTestContext context = new();

        await context.ViewModel.RestoreAsync(CancellationToken.None);

        context.ViewModel.SourceImage.Should().BeNull();
        context.DisplayImageFactory.Verify(
            factory => factory.Create(It.IsAny<SKBitmap>()),
            Times.Never);
        context.ModuleInstaller.Verify(
            installer => installer.EnsureInstalledAsync(
                It.IsAny<IProgress<Dlss5ModuleInstallProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        context.NativeEngine.Verify(
            engine => engine.InitializeAsync(
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        context.NativeEngine.Verify(
            engine => engine.PrepareAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Dlss5RenderSettings>(),
                It.IsAny<SKBitmap?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CloseCommand_AfterRestoredSourceWasOpened_ReleasesSessionResources()
    {
        using Dlss5SessionTestContext context = new();
        await context.ViewModel.RestoreAsync(CancellationToken.None);
        bool opened = await context.ViewModel.OpenAsync(CancellationToken.None);

        opened.Should().BeTrue();
        context.ViewModel.SourceImage.Should().NotBeNull();

        context.ViewModel.CloseCommand.Execute(null);

        context.ViewModel.IsOpen.Should().BeFalse();
        context.ViewModel.SourceImage.Should().BeNull();
        context.ViewModel.ResultImage.Should().BeNull();
        context.SourceDisplayImage.Verify(image => image.Dispose(), Times.Once);
        context.RenderScheduler.Verify(scheduler => scheduler.Cancel(), Times.Once);
        context.NativeEngine.Verify(engine => engine.Stop(), Times.Once);
    }

    [Fact]
    public async Task ResultPreviewImage_UsesSourceUntilFirstResultIsAvailable()
    {
        using Dlss5SessionTestContext context = new();
        await context.ViewModel.RestoreAsync(CancellationToken.None);
        await context.ViewModel.OpenAsync(CancellationToken.None);

        context.ViewModel.ResultImage.Should().BeNull();
        context.ViewModel.ResultPreviewImage.Should().BeSameAs(context.ViewModel.SourceImage);

        object generatedImage = new();
        context.ViewModel.ResultImage = generatedImage;

        context.ViewModel.ResultPreviewImage.Should().BeSameAs(generatedImage);
        context.ViewModel.CloseCommand.Execute(null);
    }

    [Fact]
    public async Task UseResultAsSourceCommand_WithRenderedImage_ReplacesSourceAndQueuesRender()
    {
        using Dlss5SessionTestContext context = new();
        Mock<IDlss5DisplayImage> resultImage = new();
        resultImage.SetupGet(image => image.Value).Returns(new object());
        Mock<IDlss5DisplayImage> nextSourceImage = new();
        nextSourceImage.SetupGet(image => image.Value).Returns(new object());
        context.DisplayImageFactory.SetupSequence(factory => factory.Create(It.IsAny<SKBitmap>()))
            .Returns(context.SourceDisplayImage.Object)
            .Returns(resultImage.Object)
            .Returns(nextSourceImage.Object);
        await context.ViewModel.RestoreAsync(CancellationToken.None);
        await context.ViewModel.OpenAsync(CancellationToken.None);
        context.ViewModel.UseResultAsSourceCommand.CanExecute(
            context.ViewModel.ResultPreviewImage).Should().BeFalse();
        Moq.IInvocation render = context.RenderScheduler.Invocations.Single(invocation =>
            invocation.Method.Name == "Request");
        Func<long, Dlss5NativeRenderResult, Task> publish =
            (Func<long, Dlss5NativeRenderResult, Task>)render.Arguments[3];
        SKBitmap renderedBitmap = new(2, 1);
        renderedBitmap.SetPixel(0, 0, SKColors.Red);
        await publish((long)render.Arguments[2], new Dlss5NativeRenderResult(
            renderedBitmap, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));
        object draggedImage = context.ViewModel.ResultImage
            ?? throw new InvalidOperationException("The rendered image was not published.");

        await context.ViewModel.UseResultAsSourceCommand.ExecuteAsync(draggedImage);

        context.ViewModel.SourceWidth.Should().Be(2);
        context.ViewModel.SourceHeight.Should().Be(1);
        context.ViewModel.ResultImage.Should().BeNull();
        context.ViewModel.UseResultAsSourceCommand.CanExecute(draggedImage).Should().BeFalse();
        using SKBitmap storedSource = SKBitmap.Decode(
            Path.Combine(context.SessionDirectory, "source.png"));
        storedSource.GetPixel(0, 0).Should().Be(SKColors.Red);
        context.RenderScheduler.Invocations.Count(invocation =>
            invocation.Method.Name == "Request").Should().Be(2);
    }

    [Fact]
    public async Task SessionView_DropResultOnSource_ReplacesSource()
    {
        await DispatchAsync(async () =>
        {
            using Dlss5SessionTestContext context = new();
            using TrackingBitmap resultBitmap = new();
            Mock<IDlss5DisplayImage> resultImage = new();
            resultImage.SetupGet(image => image.Value).Returns(resultBitmap);
            Mock<IDlss5DisplayImage> nextSourceImage = new();
            nextSourceImage.SetupGet(image => image.Value).Returns(new object());
            context.DisplayImageFactory.SetupSequence(factory => factory.Create(It.IsAny<SKBitmap>()))
                .Returns(context.SourceDisplayImage.Object)
                .Returns(resultImage.Object)
                .Returns(nextSourceImage.Object);
            await context.ViewModel.RestoreAsync(CancellationToken.None);
            await context.ViewModel.OpenAsync(CancellationToken.None);
            Moq.IInvocation render = context.RenderScheduler.Invocations.Single(invocation =>
                invocation.Method.Name == "Request");
            Func<long, Dlss5NativeRenderResult, Task> publish =
                (Func<long, Dlss5NativeRenderResult, Task>)render.Arguments[3];
            await publish((long)render.Arguments[2], new Dlss5NativeRenderResult(
                new SKBitmap(2, 1), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));
            Dlss5SessionView view = new() { DataContext = context.ViewModel };
            Window window = Show(view, 1200d, 800d);

            try
            {
                Grid sourceArea = view.FindControl<Grid>("SourceImageDropArea")
                    ?? throw new InvalidOperationException("The DLSS 5 source drop area was not found.");
                ImageDropBehavior.GetTargetKind(sourceArea)
                    .Should().Be(ImageDropTargetKind.Dlss5Result);
                ImageDropBehavior.GetUseDlss5ResultCommand(sourceArea)
                    .Should().BeSameAs(context.ViewModel.UseResultAsSourceCommand);
                DataTransfer dataTransfer = AtomicArtImageDragData.CreateDlss5Result(resultBitmap);

                sourceArea.RaiseEvent(new DragEventArgs(
                    DragDrop.DropEvent,
                    dataTransfer,
                    sourceArea,
                    new Point(20d, 20d),
                    KeyModifiers.None));
                Task operation = context.ViewModel.UseResultAsSourceCommand.ExecutionTask
                    ?? throw new InvalidOperationException("The DLSS 5 source replacement was not started.");
                await operation;

                context.ViewModel.SourceWidth.Should().Be(2);
                context.RenderScheduler.Invocations.Count(invocation =>
                    invocation.Method.Name == "Request").Should().Be(2);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task OpenFromImagePathAsync_WithDifferentExtension_RemovesPreviousSessionSource()
    {
        using Dlss5SessionTestContext context = new();
        string imagePath = Path.Combine(context.ModulesDirectory, "incoming.jpg");
        Dlss5SessionViewModelTests.WriteImage(imagePath, SKEncodedImageFormat.Jpeg);

        bool opened = await context.ViewModel.OpenFromImagePathAsync(
            imagePath,
            CancellationToken.None);

        opened.Should().BeTrue();
        File.Exists(Path.Combine(context.SessionDirectory, "source.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(context.SessionDirectory, "source.png")).Should().BeFalse();
    }

    [Fact]
    public async Task OpenAsync_WithPersistedSource_RemovesOtherSessionSourceExtensions()
    {
        using Dlss5SessionTestContext context = new();
        string staleSourcePath = Path.Combine(context.SessionDirectory, "source.jpg");
        Dlss5SessionViewModelTests.WriteImage(staleSourcePath, SKEncodedImageFormat.Jpeg);
        await context.ViewModel.RestoreAsync(CancellationToken.None);

        bool opened = await context.ViewModel.OpenAsync(CancellationToken.None);

        opened.Should().BeTrue();
        File.Exists(Path.Combine(context.SessionDirectory, "source.png")).Should().BeTrue();
        File.Exists(staleSourcePath).Should().BeFalse();
    }

    [Fact]
    public async Task PrepareForDataRootMigration_WithOpenSession_ReleasesRuntimeAndImages()
    {
        using Dlss5SessionTestContext context = new();
        await context.ViewModel.RestoreAsync(CancellationToken.None);
        await context.ViewModel.OpenAsync(CancellationToken.None);

        context.ViewModel.PrepareForDataRootMigration();

        context.ViewModel.IsOpen.Should().BeFalse();
        context.ViewModel.SourceImage.Should().BeNull();
        context.NativeEngine.Verify(engine => engine.Stop(), Times.Once);
        context.RenderScheduler.Verify(scheduler => scheduler.Cancel(), Times.Once);
        context.SourceDisplayImage.Verify(image => image.Dispose(), Times.Once);
    }

    [Fact]
    public async Task SliderChanges_PrewarmOnlyCommittedValue()
    {
        using Dlss5SessionTestContext context = new();
        await context.ViewModel.RestoreAsync(CancellationToken.None);
        await context.ViewModel.OpenAsync(CancellationToken.None);
        context.NativeEngine.Invocations.Clear();

        context.ViewModel.GeneralIntensity = 1.8f;
        context.ViewModel.GeneralIntensity = 1.6f;
        context.ViewModel.GeneralIntensity = 1.4f;
        context.NativeEngine.Verify(
            engine => engine.PrepareAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Dlss5RenderSettings>(),
                It.IsAny<SKBitmap?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.ViewModel.CommitSliderRenderCommand.Execute(null);
        await Task.Delay(150);

        context.NativeEngine.Verify(
            engine => engine.PrepareAsync(
                1,
                1,
                It.Is<Dlss5RenderSettings>(settings => settings.GeneralIntensity == 1.4f),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.ViewModel.CloseCommand.Execute(null);
    }

    [Fact]
    public async Task Dlss5DisplayImage_KeepsBitmapAliveUntilPicaLeaseIsReleased()
    {
        await DispatchAsync(() =>
        {
            using TrackingBitmap bitmap = new();
            using Dlss5DisplayImage displayImage = new(bitmap);

            IPicaImageBitmapLease lease = displayImage.AcquirePicaLease();
            displayImage.Dispose();

            bitmap.IsDisposed.Should().BeFalse();

            lease.Dispose();

            bitmap.IsDisposed.Should().BeTrue();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ClearSource_DuringInputRead_DiscardsIncomingImageAndKeepsRuntime()
    {
        using Dlss5SessionTestContext context = new();
        await context.ViewModel.OpenAsync(CancellationToken.None);
        TaskCompletionSource<AttachedImageDto?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ImageAttachmentInput input = new("source.png", _ => completion.Task);
        Task load = context.ViewModel.PasteClipboardImageCommand.ExecuteAsync(input);
        context.ViewModel.IsSourceLoading.Should().BeTrue();
        byte[] sourceBytes = await File.ReadAllBytesAsync(
            Path.Combine(context.SessionDirectory, "source.png"));

        context.ViewModel.ClearSourceCommand.Execute(null);
        completion.SetResult(new AttachedImageDto("source.png", "image/png", sourceBytes));
        await load;

        context.ViewModel.HasSource.Should().BeFalse();
        context.ViewModel.IsSourceLoading.Should().BeFalse();
        context.ViewModel.IsOpen.Should().BeTrue();
        context.NativeEngine.Verify(engine => engine.Stop(), Times.Never);
        context.DisplayImageFactory.Verify(factory => factory.Create(It.IsAny<SKBitmap>()), Times.Never);
    }

    [Fact]
    public async Task ClearSource_DuringClipboardRead_DoesNotRestoreSourceAfterRemoval()
    {
        using Dlss5SessionTestContext context = new();
        TaskCompletionSource<ImageAttachmentInput?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Clipboard.Setup(service => service.TryGetImageAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(completion.Task);
        Task load = context.ViewModel.PasteSourceCommand.ExecuteAsync(null);
        context.ViewModel.IsSourceLoading.Should().BeTrue();
        byte[] sourceBytes = await File.ReadAllBytesAsync(
            Path.Combine(context.SessionDirectory, "source.png"));

        context.ViewModel.ClearSourceCommand.Execute(null);
        completion.SetResult(ImageAttachmentInput.FromImage(
            new AttachedImageDto("source.png", "image/png", sourceBytes)));
        await load;

        context.ViewModel.HasSource.Should().BeFalse();
        context.ViewModel.IsSourceLoading.Should().BeFalse();
        context.DisplayImageFactory.Verify(factory => factory.Create(It.IsAny<SKBitmap>()), Times.Never);
    }

    [Fact]
    public async Task PickSourceCommand_UsesImagePickerAndLoadsSelectedImage()
    {
        using Dlss5SessionTestContext context = new();
        byte[] sourceBytes = await File.ReadAllBytesAsync(
            Path.Combine(context.SessionDirectory, "source.png"));
        context.FilePicker
            .Setup(service => service.PickImagesAsync(
                Dlss5FeatureDefinition.MaxSourceImageBytes,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            new List<ImageAttachmentInput>
            {
                ImageAttachmentInput.FromImage(
                    new AttachedImageDto("picked.png", "image/png", sourceBytes))
            });

        await context.ViewModel.PickSourceCommand.ExecuteAsync(null);

        context.ViewModel.HasSource.Should().BeTrue();
        context.ViewModel.SourceWidth.Should().Be(1);
        context.ViewModel.SourceHeight.Should().Be(1);
        context.FilePicker.Verify(service => service.PickImagesAsync(
            Dlss5FeatureDefinition.MaxSourceImageBytes,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("source", 0)]
    [InlineData("result", 1)]
    public async Task OpenComparison_SelectsClickedImage(string selectedImage, int expectedIndex)
    {
        using Dlss5SessionTestContext context = new();
        await context.ViewModel.RestoreAsync(CancellationToken.None);
        await context.ViewModel.OpenAsync(CancellationToken.None);
        Moq.IInvocation render = context.RenderScheduler.Invocations.Single(invocation => invocation.Method.Name == "Request");
        Func<long, Dlss5NativeRenderResult, Task> publish = (Func<long, Dlss5NativeRenderResult, Task>)render.Arguments[3];
        await publish((long)render.Arguments[2], new Dlss5NativeRenderResult(
            new SKBitmap(1, 1), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));

        await context.ViewModel.OpenComparisonCommand.ExecuteAsync(selectedImage);

        context.ImageViewer.Verify(service => service.OpenAsync(
            It.Is<GalleryImageViewerRequest>(request =>
                request.SelectedItemId == request.ItemsSource.GetItems()[expectedIndex].Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearSource_WithPendingRender_ReleasesImagesAndRejectsLateResult()
    {
        using Dlss5SessionTestContext context = new();
        await context.ViewModel.RestoreAsync(CancellationToken.None);
        await context.ViewModel.OpenAsync(CancellationToken.None);
        Moq.IInvocation render = context.RenderScheduler.Invocations.Single(invocation => invocation.Method.Name == "Request");
        Func<long, Dlss5NativeRenderResult, Task> publish = (Func<long, Dlss5NativeRenderResult, Task>)render.Arguments[3];

        context.ViewModel.ClearSourceCommand.Execute(null);
        await publish((long)render.Arguments[2], new Dlss5NativeRenderResult(
            new SKBitmap(1, 1), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));

        context.ViewModel.SourceImage.Should().BeNull();
        context.ViewModel.ResultImage.Should().BeNull();
        context.ViewModel.IsRendering.Should().BeFalse();
        context.ViewModel.IsOpen.Should().BeTrue();
        File.Exists(Path.Combine(context.SessionDirectory, "source.png")).Should().BeFalse();
        context.SourceDisplayImage.Verify(image => image.Dispose(), Times.Once);
        context.NativeEngine.Verify(engine => engine.Stop(), Times.Never);
        context.StateWriter.Verify(writer => writer.ScheduleWrite(
            It.IsAny<Dlss5SessionStateSection>(),
            It.Is<Dlss5SessionState>(state => state.SourceFileName == null), StateWriteMode.Deferred), Times.Once);
    }

    [Fact]
    public async Task ClearSource_WithRenderedImage_ReleasesBothDisplayImages()
    {
        using Dlss5SessionTestContext context = new();
        Mock<IDlss5DisplayImage> resultImage = new();
        resultImage.SetupGet(image => image.Value).Returns(new object());
        context.DisplayImageFactory.SetupSequence(factory => factory.Create(It.IsAny<SKBitmap>()))
            .Returns(context.SourceDisplayImage.Object)
            .Returns(resultImage.Object);
        await context.ViewModel.RestoreAsync(CancellationToken.None);
        await context.ViewModel.OpenAsync(CancellationToken.None);
        Moq.IInvocation render = context.RenderScheduler.Invocations.Single(invocation => invocation.Method.Name == "Request");
        Func<long, Dlss5NativeRenderResult, Task> publish = (Func<long, Dlss5NativeRenderResult, Task>)render.Arguments[3];
        await publish((long)render.Arguments[2], new Dlss5NativeRenderResult(
            new SKBitmap(1, 1), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));

        context.ViewModel.ClearSourceCommand.Execute(null);

        context.SourceDisplayImage.Verify(image => image.Dispose(), Times.Once);
        resultImage.Verify(image => image.Dispose(), Times.Once);
        context.ViewModel.SourceImage.Should().BeNull();
        context.ViewModel.ResultImage.Should().BeNull();
        context.ViewModel.IsOpen.Should().BeTrue();
        File.Exists(Path.Combine(context.SessionDirectory, "source.png")).Should().BeFalse();
        context.NativeEngine.Verify(engine => engine.Stop(), Times.Never);
    }

    [Fact]
    public async Task SessionView_ReservesMatchingImageFramesBeforeGeneration()
    {
        await DispatchAsync(async () =>
        {
            using Dlss5SessionTestContext context = new();
            await context.ViewModel.RestoreAsync(CancellationToken.None);
            await context.ViewModel.OpenAsync(CancellationToken.None);
            Dlss5SessionView view = new() { DataContext = context.ViewModel };
            Show(view, 1200, 800, window =>
            {
                Dlss5ComparisonPanel panel = view.GetVisualDescendants().OfType<Dlss5ComparisonPanel>().Single();
                panel.SplitProgress = 0.1;
                window.CaptureRenderedFrame();
                Dlss5ProgressLine progress = panel.Children
                    .OfType<Dlss5ProgressLine>()
                    .Single();
                progress.IsActive.Should().BeTrue();
                progress.IsVisible.Should().BeTrue();
                progress.Opacity.Should().Be(1);

                panel.SplitProgress = 1;
                window.CaptureRenderedFrame();
                panel.Children[1].Bounds.Size.Should().Be(panel.Children[2].Bounds.Size);
                panel.Children[2].Bounds.Width.Should().BeGreaterThan(0);
                progress.Bounds.Height.Should().Be(Dlss5ComparisonPanel.ProgressHeight);
                progress.Bounds.Width.Should().Be(panel.Children[2].Bounds.Width);
                progress.IsActive.Should().BeTrue();
                progress.IsVisible.Should().BeTrue();
                Button sourceButton = panel.Children[1]
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => string.Equals(button.CommandParameter as string, "source", StringComparison.Ordinal));
                Button resultButton = panel.Children[2]
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => string.Equals(button.CommandParameter as string, "result", StringComparison.Ordinal));
                Button removeButton = panel.Children[1]
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => button.Classes.Contains("dlss5-remove"));
                sourceButton.IsEnabled.Should().BeFalse();
                resultButton.IsEnabled.Should().BeFalse();
                removeButton.IsEnabled.Should().BeFalse();
                view.GetVisualDescendants()
                    .OfType<Dlss5SourceLoadingControl>()
                    .Should()
                    .HaveCount(2);
                context.ViewModel.ResultImage.Should().BeNull();
                context.ViewModel.SourceWidth.Should().Be(1);
                context.ViewModel.SourceHeight.Should().Be(1);
                Dlss5ParameterPanel parameters = view.FindControl<Dlss5ParameterPanel>("ParametersPanel")
                    ?? throw new InvalidOperationException("DLSS 5 parameter panel was not found.");
                Point parameterOrigin = parameters.TranslatePoint(default, view)
                    ?? throw new InvalidOperationException("DLSS 5 parameter panel position was not found.");
                double rightSpace = view.Bounds.Width - parameterOrigin.X - parameters.Bounds.Width;
                parameters.ItemWidth.Should().BeLessThanOrEqualTo(292);
                parameterOrigin.X.Should().BeGreaterThanOrEqualTo(16);
                rightSpace.Should().BeGreaterThanOrEqualTo(16);
                bool hasOnlyButtonsWithoutTooltips = view.GetVisualDescendants()
                    .OfType<Button>()
                    .All(button => ToolTip.GetTip(button) is null);
                hasOnlyButtonsWithoutTooltips.Should().BeTrue();
            });
        });
    }

    [Fact]
    public async Task SessionView_WhenSplitterChangesAvailableHeight_RegroupsParametersWithoutStretching()
    {
        await DispatchAsync(async () =>
        {
            using Dlss5SessionTestContext context = new();
            await context.ViewModel.RestoreAsync(CancellationToken.None);
            await context.ViewModel.OpenAsync(CancellationToken.None);
            Dlss5SessionView view = new() { DataContext = context.ViewModel };
            Show(view, 1200, 800, window =>
            {
                Grid layout = view.Content.Should().BeOfType<Grid>().Subject;
                Dlss5ParameterPanel parameters = view.FindControl<Dlss5ParameterPanel>("ParametersPanel")
                    ?? throw new InvalidOperationException("DLSS 5 parameter panel was not found.");
                layout.RowDefinitions[3].Height = new GridLength(300);
                window.CaptureRenderedFrame();
                double expandedHeightWidth = parameters.Bounds.Width;
                parameters.Rows.Should().Be(4);
                parameters.Columns.Should().Be(2);
                parameters.Children[0].Bounds.Left.Should().Be(parameters.Children[3].Bounds.Left);
                parameters.Children[0].Bounds.Top.Should().BeLessThan(parameters.Children[1].Bounds.Top);
                parameters.Children[1].Bounds.Top.Should().BeLessThan(parameters.Children[2].Bounds.Top);
                parameters.Children[2].Bounds.Top.Should().BeLessThan(parameters.Children[3].Bounds.Top);
                parameters.Children[4].Bounds.Left.Should().BeGreaterThan(parameters.Children[0].Bounds.Left);
                parameters.Children[4].Bounds.Top.Should().Be(parameters.Children[0].Bounds.Top);
                GridSplitter splitter = view.GetVisualDescendants()
                    .OfType<GridSplitter>()
                    .Single();
                splitter.Bounds.Height.Should().Be(24);
                splitter.ZIndex.Should().Be(10);

                foreach (Grid parameter in parameters.Children.OfType<Grid>().Skip(1))
                {
                    Slider slider = parameter.Children.OfType<Slider>().Single();
                    TextBlock value = parameter.Children
                        .OfType<TextBlock>()
                        .Single(text => (Grid.GetRow(text) == 1) && (Grid.GetColumn(text) == 1));
                    (value.Bounds.Left - slider.Bounds.Right).Should().BeApproximately(8, 0.1);
                }

                Grid firstSliderParameter = parameters.Children.OfType<Grid>().Skip(1).First();
                Slider firstSlider = firstSliderParameter.Children.OfType<Slider>().Single();
                TextBlock firstValue = firstSliderParameter.Children
                    .OfType<TextBlock>()
                    .Single(text => (Grid.GetRow(text) == 1) && (Grid.GetColumn(text) == 1));
                double sliderWidth = firstSlider.Bounds.Width;
                firstValue.Text = "-0.12";
                window.CaptureRenderedFrame();
                firstSlider.Bounds.Width.Should().Be(sliderWidth);

                layout.RowDefinitions[3].Height = new GridLength(80);
                window.CaptureRenderedFrame();
                double reducedHeightWidth = parameters.Bounds.Width;

                expandedHeightWidth.Should().BeLessThan(reducedHeightWidth);
                parameters.ItemWidth.Should().BeLessThanOrEqualTo(292);
                Point origin = parameters.TranslatePoint(default, view)
                    ?? throw new InvalidOperationException("DLSS 5 parameter panel position was not found.");
                origin.X.Should().BeGreaterThanOrEqualTo(16);
                (view.Bounds.Width - origin.X - parameters.Bounds.Width).Should().BeGreaterThanOrEqualTo(16);
            });
        });
    }

    [Fact]
    public async Task SessionView_RestoresAndPersistsParameterAreaHeight()
    {
        await DispatchAsync(async () =>
        {
            using Dlss5SessionTestContext context = new(parameterAreaHeight: 276d);
            await context.ViewModel.RestoreAsync(CancellationToken.None);
            Dlss5SessionView view = new() { DataContext = context.ViewModel };
            Show(view, 1200, 800, window =>
            {
                Grid layout = view.Content.Should().BeOfType<Grid>().Subject;
                window.CaptureRenderedFrame();

                layout.RowDefinitions[3].ActualHeight.Should().BeApproximately(276d, 0.1d);

                layout.RowDefinitions[3].Height = new GridLength(312d);
                window.CaptureRenderedFrame();
                GridSplitter splitter = view.GetVisualDescendants()
                    .OfType<GridSplitter>()
                    .Single();
                splitter.RaiseEvent(new VectorEventArgs
                {
                    RoutedEvent = Thumb.DragCompletedEvent,
                    Vector = new Vector(0d, 36d)
                });

                context.ViewModel.ParameterAreaHeight.Should().BeApproximately(312d, 0.1d);
                context.StateWriter.Verify(
                    writer => writer.ScheduleWrite(
                        It.IsAny<Dlss5SessionStateSection>(),
                        It.Is<Dlss5SessionState>(state => state.ParameterAreaHeight == 312d),
                        StateWriteMode.Deferred),
                    Times.Once);
            });
        });
    }

    private static void WriteImage(string path, SKEncodedImageFormat format)
    {
        using SKBitmap source = new(1, 1);
        using SKImage image = SKImage.FromBitmap(source);
        using SKData data = image.Encode(format, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private sealed class Dlss5SessionTestContext : IDisposable
    {
        internal Dlss5SessionViewModel ViewModel { get; }
        internal Mock<IDlss5ModuleInstaller> ModuleInstaller { get; } = new();
        internal Mock<IDlss5NativeEngine> NativeEngine { get; } = new();
        internal Mock<IDlss5RenderScheduler> RenderScheduler { get; } = new();
        internal Mock<IDlss5DisplayImageFactory> DisplayImageFactory { get; } = new();
        internal Mock<IDlss5DisplayImage> SourceDisplayImage { get; } = new();
        internal Mock<IImageViewerService> ImageViewer { get; } = new();
        internal Mock<IFilePickerService> FilePicker { get; } = new();
        internal Mock<IClipboardImageService> Clipboard { get; } = new();
        internal Mock<IStateWriteScheduler> StateWriter { get; } = new();
        internal Mock<IDialogService> DialogService { get; } = new();
        internal string ModulesDirectory => _modulesDirectory;
        internal string SessionDirectory { get; }

        private const string SourceFileName = "source.png";

        private readonly string _modulesDirectory;

        internal Dlss5SessionTestContext(
            bool isInstalled = true,
            double parameterAreaHeight = Dlss5SessionState.DefaultParameterAreaHeight)
        {
            _modulesDirectory = Path.Combine(
                Path.GetTempPath(),
                $"AtomicArt-Dlss5Session-{Guid.NewGuid():N}");
            Mock<IAtomicArtDataPathProvider> pathProvider = new();
            pathProvider
                .SetupGet(provider => provider.ModulesDirectory)
                .Returns(_modulesDirectory);
            Dlss5ModulePaths paths = new(pathProvider.Object);
            SessionDirectory = Path.Combine(paths.ModuleDirectory, "session");
            string sourcePath = Path.Combine(SessionDirectory, SourceFileName);
            Directory.CreateDirectory(SessionDirectory);
            Dlss5SessionViewModelTests.WriteImage(sourcePath, SKEncodedImageFormat.Png);

            ModuleInstaller.SetupGet(installer => installer.IsInstalled).Returns(isInstalled);
            ModuleInstaller
                .Setup(installer => installer.EnsureInstalledAsync(
                    It.IsAny<IProgress<Dlss5ModuleInstallProgress>?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            NativeEngine.SetupGet(engine => engine.IsInitialized).Returns(true);
            NativeEngine
                .Setup(engine => engine.InitializeAsync(
                    It.IsAny<IProgress<int>?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            NativeEngine
                .Setup(engine => engine.PrepareAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<Dlss5RenderSettings>(),
                    It.IsAny<SKBitmap?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Mock<IUiThreadDispatcher> uiThreadDispatcher = new();
            uiThreadDispatcher
                .Setup(dispatcher => dispatcher.InvokeAsync(
                    It.IsAny<Action>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Action action, CancellationToken _) =>
                {
                    action();
                    return Task.CompletedTask;
                });
            SourceDisplayImage.SetupGet(image => image.Value).Returns(new object());
            DisplayImageFactory
                .Setup(factory => factory.Create(It.IsAny<SKBitmap>()))
                .Returns(SourceDisplayImage.Object);
            Mock<IDataRootAccessCoordinator> accessCoordinator = new();
            accessCoordinator
                .Setup(coordinator => coordinator.AcquireAccessAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DataRootAccessLease(static () => { }));
            ViewModel = new Dlss5SessionViewModel(
                FilePicker.Object,
                Clipboard.Object,
                DialogService.Object,
                ModuleInstaller.Object,
                NativeEngine.Object,
                RenderScheduler.Object,
                paths,
                uiThreadDispatcher.Object,
                new StubAppStateStore(new Dlss5SessionState
                {
                    SourceFileName = SourceFileName,
                    ParameterAreaHeight = parameterAreaHeight
                }),
                StateWriter.Object,
                new Dlss5SessionStateSection(),
                ImageViewer.Object,
                accessCoordinator.Object,
                DisplayImageFactory.Object,
                new Mock<IViewModelErrorHandler>().Object,
                new Mock<ILogger<Dlss5SessionViewModel>>().Object);
        }

        public void Dispose()
        {
            Directory.Delete(_modulesDirectory, recursive: true);
        }
    }

    private sealed class TrackingBitmap : Bitmap
    {
        internal bool IsDisposed { get; private set; }

        internal TrackingBitmap()
            : base(TrackingBitmap.CreatePixelStream())
        {
        }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

        private static Stream CreatePixelStream()
        {
            using SkiaSharp.SKBitmap source = new(1, 1);
            using SkiaSharp.SKImage image = SkiaSharp.SKImage.FromBitmap(source);
            using SkiaSharp.SKData data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return new MemoryStream(data.ToArray(), writable: false);
        }
    }
}
