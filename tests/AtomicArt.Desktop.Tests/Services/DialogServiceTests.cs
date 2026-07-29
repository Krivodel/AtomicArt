using FluentAssertions;
using Xunit;

using CommunityToolkit.Mvvm.Messaging;

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

    private static ErrorDialogViewModel CreateViewModel()
    {
        return new ErrorDialogViewModel(
            new RecordingTextClipboardService(),
            new TestViewModelErrorHandler());
    }
}
