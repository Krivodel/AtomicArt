using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.ViewModels.Dialogs;

namespace AtomicArt.Desktop.Tests.ViewModels.Dialogs;

public sealed class ConfirmationDialogViewModelTests
{
    private const string Title = "Delete generations?";
    private const string Message = "Selected generations will be deleted.";
    private const string ConfirmActionText = "Delete";

    [Fact]
    public async Task ConfirmCommand_WhenDialogIsOpen_CompletesWithTrue()
    {
        ConfirmationDialogViewModel viewModel = new();
        Task<bool> resultTask = viewModel.OpenAsync(
            Title,
            Message,
            ConfirmActionText,
            CancellationToken.None);

        viewModel.ConfirmCommand.Execute(null);
        bool result = await resultTask;

        result.Should().BeTrue();
        viewModel.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task CancelCommand_WhenDialogIsOpen_CompletesWithFalse()
    {
        ConfirmationDialogViewModel viewModel = new();
        Task<bool> resultTask = viewModel.OpenAsync(
            Title,
            Message,
            ConfirmActionText,
            CancellationToken.None);

        viewModel.CancelCommand.Execute(null);
        bool result = await resultTask;

        result.Should().BeFalse();
        viewModel.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task OpenAsync_WhenCanceled_ClosesDialogAndThrowsCancellation()
    {
        ConfirmationDialogViewModel viewModel = new();
        using CancellationTokenSource cancellationTokenSource = new();
        Task<bool> resultTask = viewModel.OpenAsync(
            Title,
            Message,
            ConfirmActionText,
            cancellationTokenSource.Token);

        await cancellationTokenSource.CancelAsync();
        Func<Task> act = async () => await resultTask;

        await act.Should().ThrowAsync<OperationCanceledException>();
        viewModel.IsOpen.Should().BeFalse();
    }
}
