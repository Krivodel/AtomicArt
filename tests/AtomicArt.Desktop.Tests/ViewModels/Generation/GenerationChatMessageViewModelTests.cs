using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

public sealed class GenerationChatMessageViewModelTests
{
    [Fact]
    public async Task Cancel_WithAttachmentPreparing_DiscardsLateImageAndRestoresCommandState()
    {
        Mock<IGenerationReferenceStore> references = new();
        Mock<IAttachedImagePreparationService> preparation = new();
        TaskCompletionSource<AttachedImageDto?> prepared = new(TaskCreationOptions.RunContinuationsAsynchronously);
        preparation.Setup(service => service.PrepareAsync(It.IsAny<AttachedImageDto>(),
                It.IsAny<ImageModelOption>(), It.IsAny<CancellationToken>()))
            .Returns(prepared.Task);
        GenerationChatMessage message = new(false, "Original", new DateTime(2026, 11, 24), Array.Empty<GenerationReference>());
        GenerationChatMediaService media = GenerationChatMediaServiceTestFactory.Create(
            references.Object, Mock.Of<IAtomicArtDataPathProvider>(), preparation: preparation.Object);
        using GenerationChatMessageViewModel viewModel = new(
            message, media, references.Object,
            new NanoBanana2PanelTextFormatter(TestLocalizationTextProvider.Default), Mock.Of<ITextClipboardService>(),
            (_, _, _) => Task.CompletedTask, new TestViewModelErrorHandler(), Mock.Of<IDialogService>());
        AttachedImageDto image = new("draft.png", "image/png", new byte[] { 1, 2, 3 });
        viewModel.EditCommand.Execute(null);
        Task attachment = viewModel.AttachImageInputsCommand.ExecuteAsync(
            new ImageAttachmentInput[] { ImageAttachmentInput.FromImage(image) });
        viewModel.IsLoading.Should().BeTrue();
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();

        viewModel.CancelCommand.Execute(null);
        prepared.SetResult(image);
        await attachment;

        viewModel.IsEditing.Should().BeFalse();
        viewModel.IsLoading.Should().BeFalse();
        viewModel.Images.Should().BeEmpty();
        references.Verify(store => store.SaveAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<AttachedImageDto>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        viewModel.EditCommand.Execute(null);
        viewModel.PickImagesCommand.CanExecute(null).Should().BeTrue();
        viewModel.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_WithSaveStillRunning_ReleasesEditLeaseAfterSaveEnds()
    {
        Mock<IGenerationReferenceStore> references = new();
        TaskCompletionSource saving = new();
        GenerationChatMessage message = new(
            false, "Original", new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc), Array.Empty<GenerationReference>());
        GenerationChatMediaService media = GenerationChatMediaServiceTestFactory.Create(
            references.Object, Mock.Of<IAtomicArtDataPathProvider>());
        using GenerationChatMessageViewModel viewModel = new(
            message, media, references.Object,
            new NanoBanana2PanelTextFormatter(TestLocalizationTextProvider.Default), Mock.Of<ITextClipboardService>(),
            (_, _, _) => saving.Task, new TestViewModelErrorHandler(), Mock.Of<IDialogService>());
        viewModel.EditCommand.Execute(null);
        Task save = viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.Dispose();
        references.Verify(store => store.Release(It.IsAny<Guid>()), Times.Never);
        saving.SetException(new OperationCanceledException());
        await save;

        viewModel.IsEditing.Should().BeFalse();
        viewModel.IsLoading.Should().BeFalse();
        references.Verify(store => store.Release(It.Is<Guid>(id => id != Guid.Empty)), Times.Once);
    }
}
