using FluentAssertions;
using Xunit;

using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Dialogs;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class DialogServiceTests
{
    private const string ErrorMessage = "The action could not be completed.";

    [Fact]
    public void ShowError_WithMessage_OpensDialog()
    {
        ErrorDialogViewModel viewModel = CreateViewModel();
        DialogService dialogService = new(
            viewModel,
            new ConfirmationDialogViewModel(),
            TestLocalizationTextProvider.Default,
            new WeakReferenceMessenger());

        dialogService.ShowError(ErrorMessage);

        viewModel.Message.Should().Be(ErrorMessage);
        viewModel.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task ShowErrorAsync_WhenCanceled_DoesNotOpenDialog()
    {
        ErrorDialogViewModel viewModel = CreateViewModel();
        DialogService dialogService = new(
            viewModel,
            new ConfirmationDialogViewModel(),
            TestLocalizationTextProvider.Default,
            new WeakReferenceMessenger());
        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        Func<Task> act = async () =>
            await dialogService.ShowErrorAsync(
                ErrorMessage,
                cancellationTokenSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        viewModel.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task ShowConfirmationAsync_WithLocalizedRequest_OpensLocalizedDialog()
    {
        ConfirmationDialogViewModel confirmationDialog = new();
        DialogService dialogService = new(
            CreateViewModel(),
            confirmationDialog,
            TestLocalizationTextProvider.Default,
            new WeakReferenceMessenger());
        object?[] messageArguments = [2];
        LocalizedConfirmationDialogRequest request = new(
            GalleryLocalizationKeys.DeletionConfirmationTitle,
            GalleryLocalizationKeys.DeletionConfirmationMessage,
            GalleryLocalizationKeys.ConfirmDeletion,
            messageArguments);
        Task<bool> resultTask = dialogService.ShowConfirmationAsync(
            request,
            CancellationToken.None);

        confirmationDialog.CancelCommand.Execute(null);
        bool result = await resultTask;

        confirmationDialog.Title.Should().Be(
            TestLocalizationTextProvider.Default.Get(
                GalleryLocalizationKeys.DeletionConfirmationTitle));
        confirmationDialog.Message.Should().Contain("2");
        confirmationDialog.ConfirmActionText.Should().Be(
            TestLocalizationTextProvider.Default.Get(
                GalleryLocalizationKeys.ConfirmDeletion));
        result.Should().BeFalse();
    }

    private static ErrorDialogViewModel CreateViewModel()
    {
        return new ErrorDialogViewModel(
            new RecordingTextClipboardService(),
            new TestViewModelErrorHandler());
    }
}
