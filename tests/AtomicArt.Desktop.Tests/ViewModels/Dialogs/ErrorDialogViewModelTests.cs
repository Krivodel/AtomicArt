using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels;
using AtomicArt.Desktop.ViewModels.Dialogs;

namespace AtomicArt.Desktop.Tests.ViewModels.Dialogs;

public sealed class ErrorDialogViewModelTests
{
    private const string ErrorMessage = "The action could not be completed.";

    [Fact]
    public void Open_WithMessage_OpensDialog()
    {
        ErrorDialogViewModel viewModel = CreateViewModel();

        viewModel.Open(ErrorMessage);

        viewModel.Message.Should().Be(ErrorMessage);
        viewModel.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void CloseCommand_WhenDialogIsOpen_ClosesDialog()
    {
        ErrorDialogViewModel viewModel = CreateViewModel();
        viewModel.Open(ErrorMessage);

        viewModel.CloseCommand.Execute(null);

        viewModel.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task CopyCommand_WhenDialogIsOpen_CopiesMessageWithoutClosingDialog()
    {
        RecordingTextClipboardService clipboardService = new();
        ErrorDialogViewModel viewModel = CreateViewModel(clipboardService);
        viewModel.Open(ErrorMessage);

        await viewModel.CopyCommand.ExecuteAsync(null);

        clipboardService.Text.Should().Be(ErrorMessage);
        viewModel.IsOpen.Should().BeTrue();
    }

    private static ErrorDialogViewModel CreateViewModel(
        RecordingTextClipboardService? clipboardService = null)
    {
        return new ErrorDialogViewModel(
            clipboardService ?? new RecordingTextClipboardService(),
            new TestViewModelErrorHandler());
    }
}
