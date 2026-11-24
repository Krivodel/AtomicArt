using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.ViewModels;

namespace AtomicArt.Desktop.Tests.ViewModels;

public sealed class AsyncOperationViewModelTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationFails_LogsFailureAndShowsSafeError()
    {
        InvalidOperationException failure = new("Internal path and credentials");
        Mock<IViewModelErrorHandler> errors = new();
        errors.Setup(handler => handler.GetUserMessage(failure)).Returns("Safe error");
        Mock<IDialogService> dialogs = new();
        OperationViewModel viewModel = new(errors.Object, dialogs.Object);

        await viewModel.RunAsync(_ => Task.FromException(failure), CancellationToken.None);

        viewModel.IsLoading.Should().BeFalse();
        viewModel.ErrorMessage.Should().Be("Safe error");
        viewModel.HasErrorMessage.Should().BeTrue();
        errors.Verify(handler => handler.Log(failure, nameof(OperationViewModel.RunAsync)), Times.Once);
        dialogs.Verify(service => service.ShowLocalizedErrorAsync(
            CommonLocalizationKeys.UnknownError, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsRequested_KeepsErrorStateClear()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Mock<IViewModelErrorHandler> errors = new();
        Mock<IDialogService> dialogs = new();
        OperationViewModel viewModel = new(errors.Object, dialogs.Object);

        await viewModel.RunAsync(token => Task.FromCanceled(token), cancellation.Token);

        viewModel.IsLoading.Should().BeFalse();
        viewModel.ErrorMessage.Should().BeNull();
        viewModel.HasErrorMessage.Should().BeFalse();
        errors.VerifyNoOtherCalls();
        dialogs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_WithOverlappingOperations_KeepsLoadingUntilBothFinishAndShowsOneError()
    {
        InvalidOperationException failure = new("Failure");
        Mock<IViewModelErrorHandler> errors = new();
        errors.Setup(handler => handler.GetUserMessage(failure)).Returns("Safe error");
        Mock<IDialogService> dialogs = new();
        OperationViewModel viewModel = new(errors.Object, dialogs.Object);
        TaskCompletionSource first = new();
        TaskCompletionSource second = new();

        Task failing = viewModel.RunAsync(_ => first.Task, CancellationToken.None);
        Task succeeding = viewModel.RunAsync(_ => second.Task, CancellationToken.None);
        viewModel.IsLoading.Should().BeTrue();
        first.SetException(failure);
        await failing;
        viewModel.IsLoading.Should().BeTrue();
        second.SetResult();
        await succeeding;

        viewModel.IsLoading.Should().BeFalse();
        dialogs.Verify(service => service.ShowLocalizedErrorAsync(
            CommonLocalizationKeys.UnknownError, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenErrorDialogFails_LogsDialogFailureAndReleasesLoadingState()
    {
        InvalidOperationException operationFailure = new("Operation failed");
        InvalidOperationException dialogFailure = new("Dialog failed");
        Mock<IViewModelErrorHandler> errors = new();
        errors.Setup(handler => handler.GetUserMessage(It.IsAny<Exception>())).Returns("Safe error");
        Mock<IDialogService> dialogs = new();
        dialogs.Setup(service => service.ShowLocalizedErrorAsync(
            CommonLocalizationKeys.UnknownError, CancellationToken.None))
            .ThrowsAsync(dialogFailure);
        OperationViewModel viewModel = new(errors.Object, dialogs.Object);

        await viewModel.RunAsync(_ => Task.FromException(operationFailure), CancellationToken.None);

        viewModel.IsLoading.Should().BeFalse();
        viewModel.ErrorMessage.Should().Be("Safe error");
        errors.Verify(handler => handler.Log(dialogFailure, nameof(OperationViewModel.RunAsync)), Times.Once);
    }

    private sealed class OperationViewModel : AsyncOperationViewModel
    {
        public OperationViewModel(IViewModelErrorHandler errorHandler, IDialogService dialogs)
                    : base(errorHandler, dialogs)
        {
        }

        public Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct)
        {
            return ExecuteAsync(action, nameof(RunAsync), ct);
        }
    }
}
