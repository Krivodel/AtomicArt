using System.Globalization;

using FluentAssertions;
using Moq;
using Xunit;

using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Dialogs;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class DialogServiceTests
{
    private const string ErrorMessage = "The action could not be completed.";
    private const string TitleKey = "Dialog.Title";
    private const string MessageKey = "Dialog.Message";
    private const string ConfirmKey = "Dialog.Confirm";
    private const string CancelKey = "Dialog.Cancel";

    [Fact]
    public void ShowError_WithMessage_OpensDialog()
    {
        ErrorDialogViewModel viewModel = CreateViewModel();
        DialogService dialogService = new(
            viewModel,
            new RecordingConfirmationDialogPresenter(),
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
            new RecordingConfirmationDialogPresenter(),
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
        RecordingConfirmationDialogPresenter confirmationPresenter = new()
        {
            Result = false
        };
        DialogService dialogService = new(
            CreateViewModel(),
            confirmationPresenter,
            TestLocalizationTextProvider.Default,
            new WeakReferenceMessenger());
        object?[] messageArguments = [2];
        LocalizedConfirmationDialogRequest request = new(
            GalleryLocalizationKeys.DeletionConfirmationTitle,
            GalleryLocalizationKeys.DeletionConfirmationMessage,
            GalleryLocalizationKeys.ConfirmDeletion,
            CommonLocalizationKeys.Cancel,
            ConfirmationDialogKind.Destructive,
            ConfirmationDialogBackgroundClickBehavior.Dismiss,
            messageArguments);
        bool result = await dialogService.ShowConfirmationAsync(
            request,
            CancellationToken.None);

        ConfirmationDialogPresentation presentation = confirmationPresenter
            .Presentations
            .Single();
        presentation.Title.Should().Be(
            TestLocalizationTextProvider.Default.Get(
                GalleryLocalizationKeys.DeletionConfirmationTitle));
        presentation.Message.Should().Contain("2");
        presentation.ConfirmActionText.Should().Be(
            TestLocalizationTextProvider.Default.Get(
                GalleryLocalizationKeys.ConfirmDeletion));
        presentation.CancelActionText.Should().Be(
            TestLocalizationTextProvider.Default.Get(
                CommonLocalizationKeys.Cancel));
        presentation.Kind.Should().Be(ConfirmationDialogKind.Destructive);
        presentation.BackgroundClickBehavior.Should().Be(
            ConfirmationDialogBackgroundClickBehavior.Dismiss);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Receive_WhenConfirmationIsOpen_UpdatesLocalizedText()
    {
        Dictionary<string, string> texts = new()
        {
            [TitleKey] = "Confirmation",
            [MessageKey] = "Delete {0}?",
            [ConfirmKey] = "Delete",
            [CancelKey] = "Cancel"
        };
        Mock<ILocalizationTextProvider> textProviderMock = new();
        textProviderMock
            .Setup(provider => provider.Get(It.IsAny<string>()))
            .Returns((string key) => texts[key]);
        textProviderMock
            .Setup(provider => provider.Format(
                It.IsAny<string>(),
                It.IsAny<object?[]>()))
            .Returns((string key, object?[] arguments) => string.Format(
                CultureInfo.InvariantCulture,
                texts[key],
                arguments));
        RecordingConfirmationDialogPresenter confirmationPresenter = new()
        {
            WaitForCompletion = true
        };
        WeakReferenceMessenger messenger = new();
        DialogService dialogService = new(
            CreateViewModel(),
            confirmationPresenter,
            textProviderMock.Object,
            messenger);
        object?[] messageArguments = [2];
        LocalizedConfirmationDialogRequest request = new(
            TitleKey,
            MessageKey,
            ConfirmKey,
            CancelKey,
            ConfirmationDialogKind.Destructive,
            ConfirmationDialogBackgroundClickBehavior.Dismiss,
            messageArguments);
        Task<bool> resultTask = dialogService.ShowConfirmationAsync(
            request,
            CancellationToken.None);

        texts[TitleKey] = "Подтверждение";
        texts[MessageKey] = "Удалить {0}?";
        texts[ConfirmKey] = "Удалить";
        texts[CancelKey] = "Отмена";
        messenger.Send(new LocalizationChangedMessage());

        ConfirmationDialogPresentation update = confirmationPresenter
            .Updates
            .Single();
        update.Title.Should().Be("Подтверждение");
        update.Message.Should().Be("Удалить 2?");
        update.ConfirmActionText.Should().Be("Удалить");
        update.CancelActionText.Should().Be("Отмена");
        confirmationPresenter.Complete(false);
        await resultTask;
    }

    private static ErrorDialogViewModel CreateViewModel()
    {
        return new ErrorDialogViewModel(
            new RecordingTextClipboardService(),
            new TestViewModelErrorHandler());
    }
}
