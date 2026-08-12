using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using FluentAssertions;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Enums;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Views.Dialogs;

namespace AtomicArt.Desktop.Tests.Views.Dialogs;

public sealed class ConfirmationDialogPresenterTests : DesktopControlTestBase
{
    private const string Title = "Confirmation";
    private const string Message = "Continue?";
    private const string ConfirmActionText = "Yes";
    private const string CancelActionText = "Not now";

    [Fact]
    public async Task ShowAsync_WithStandardDialog_UsesConfirmFirstAndAccentStyle()
    {
        await DispatchAsync(async () =>
        {
            SukiDialogManager manager = new();
            ISukiDialog? dialog = null;
            manager.OnDialogShown += (_, args) => dialog = args.Dialog;
            ConfirmationDialogPresenter presenter = CreatePresenter(manager);
            ConfirmationDialogPresentation presentation = CreatePresentation(
                ConfirmationDialogKind.Standard);

            Task<bool> resultTask = presenter.ShowAsync(
                presentation,
                CancellationToken.None);

            ISukiDialog shownDialog = GetShownDialog(dialog);
            shownDialog.CanDismissWithBackgroundClick.Should().BeTrue();
            Button[] buttons = shownDialog.ActionButtons
                .OfType<Button>()
                .ToArray();
            buttons.Select(button => button.Content).Should().Equal(
                ConfirmActionText,
                CancelActionText);
            buttons[0].Classes.Should().Contain(
                SukiButtonStyles.Accent.ToString());
            buttons[1].Classes.Should().Contain(
                SukiButtonStyles.Basic.ToString());

            buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bool result = await resultTask;

            result.Should().BeTrue();
            presenter.IsOpen.Should().BeFalse();
        });
    }

    [Fact]
    public async Task ShowAsync_WithIgnoredBackgroundClick_DisablesBackgroundDismissal()
    {
        await DispatchAsync(async () =>
        {
            SukiDialogManager manager = new();
            ISukiDialog? dialog = null;
            manager.OnDialogShown += (_, args) => dialog = args.Dialog;
            ConfirmationDialogPresenter presenter = CreatePresenter(manager);
            ConfirmationDialogPresentation presentation = new(
                Title,
                Message,
                ConfirmActionText,
                CancelActionText,
                ConfirmationDialogKind.Standard,
                ConfirmationDialogBackgroundClickBehavior.Ignore);

            Task<bool> resultTask = presenter.ShowAsync(
                presentation,
                CancellationToken.None);

            ISukiDialog shownDialog = GetShownDialog(dialog);
            shownDialog.CanDismissWithBackgroundClick.Should().BeFalse();
            presenter.Dismiss();
            bool result = await resultTask;

            result.Should().BeFalse();
        });
    }

    [Fact]
    public async Task ShowAsync_WithDestructiveDialog_UsesCancelFirstAndDangerStyle()
    {
        await DispatchAsync(async () =>
        {
            SukiDialogManager manager = new();
            ISukiDialog? dialog = null;
            manager.OnDialogShown += (_, args) => dialog = args.Dialog;
            ConfirmationDialogPresenter presenter = CreatePresenter(manager);
            ConfirmationDialogPresentation presentation = CreatePresentation(
                ConfirmationDialogKind.Destructive);

            Task<bool> resultTask = presenter.ShowAsync(
                presentation,
                CancellationToken.None);

            ISukiDialog shownDialog = GetShownDialog(dialog);
            Button[] buttons = shownDialog.ActionButtons
                .OfType<Button>()
                .ToArray();
            buttons.Select(button => button.Content).Should().Equal(
                CancelActionText,
                ConfirmActionText);
            buttons[0].Classes.Should().Contain(
                SukiButtonStyles.Basic.ToString());
            buttons[1].Classes.Should().Contain(
                SukiButtonStyles.Danger.ToString());

            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bool result = await resultTask;

            result.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Dismiss_WhenDialogIsOpen_CompletesWithFalse()
    {
        await DispatchAsync(async () =>
        {
            ConfirmationDialogPresenter presenter = CreatePresenter(
                new SukiDialogManager());
            Task<bool> resultTask = presenter.ShowAsync(
                CreatePresentation(ConfirmationDialogKind.Standard),
                CancellationToken.None);

            presenter.Dismiss();
            bool result = await resultTask;

            result.Should().BeFalse();
            presenter.IsOpen.Should().BeFalse();
        });
    }

    [Fact]
    public async Task ShowAsync_WhenCanceled_DismissesAndThrowsCancellation()
    {
        await DispatchAsync(async () =>
        {
            ConfirmationDialogPresenter presenter = CreatePresenter(
                new SukiDialogManager());
            using CancellationTokenSource cancellationTokenSource = new();
            Task<bool> resultTask = presenter.ShowAsync(
                CreatePresentation(ConfirmationDialogKind.Standard),
                cancellationTokenSource.Token);

            await cancellationTokenSource.CancelAsync();
            Func<Task> act = async () => await resultTask;

            await act.Should().ThrowAsync<OperationCanceledException>();
            presenter.IsOpen.Should().BeFalse();
        });
    }

    [Fact]
    public async Task ShowAsync_WhenAnotherConfirmationIsOpen_ThrowsInvalidOperationException()
    {
        await DispatchAsync(async () =>
        {
            ConfirmationDialogPresenter presenter = CreatePresenter(
                new SukiDialogManager());
            ConfirmationDialogPresentation presentation = CreatePresentation(
                ConfirmationDialogKind.Standard);
            Task<bool> firstResultTask = presenter.ShowAsync(
                presentation,
                CancellationToken.None);

            Func<Task> act = async () => await presenter.ShowAsync(
                presentation,
                CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            presenter.Dismiss();
            await firstResultTask;
        });
    }

    [Fact]
    public async Task Update_WhenDialogIsOpen_UpdatesVisibleText()
    {
        await DispatchAsync(async () =>
        {
            SukiDialogManager manager = new();
            ISukiDialog? dialog = null;
            manager.OnDialogShown += (_, args) => dialog = args.Dialog;
            ConfirmationDialogPresenter presenter = CreatePresenter(manager);
            Task<bool> resultTask = presenter.ShowAsync(
                CreatePresentation(ConfirmationDialogKind.Standard),
                CancellationToken.None);
            ConfirmationDialogPresentation updatedPresentation = new(
                "Подтверждение",
                "Продолжить?",
                "Да",
                "Не сейчас",
                ConfirmationDialogKind.Standard,
                ConfirmationDialogBackgroundClickBehavior.Dismiss);

            presenter.Update(updatedPresentation);

            ISukiDialog shownDialog = GetShownDialog(dialog);
            shownDialog.Title.Should().Be(updatedPresentation.Title);
            shownDialog.Content
                .Should()
                .BeOfType<TextBlock>()
                .Which.Text.Should().Be(updatedPresentation.Message);
            shownDialog.ActionButtons
                .OfType<Button>()
                .Select(button => button.Content)
                .Should()
                .Equal(
                    updatedPresentation.ConfirmActionText,
                    updatedPresentation.CancelActionText);
            presenter.Dismiss();
            await resultTask;
        });
    }

    [Theory]
    [InlineData(ConfirmationDialogKind.Standard, 0.6d)]
    [InlineData(ConfirmationDialogKind.Standard, 1d)]
    [InlineData(ConfirmationDialogKind.Standard, 1.5d)]
    [InlineData(ConfirmationDialogKind.Destructive, 0.6d)]
    [InlineData(ConfirmationDialogKind.Destructive, 1d)]
    [InlineData(ConfirmationDialogKind.Destructive, 1.5d)]
    public async Task ShowAsync_WithUiScale_AppliesScaleToEntireDialog(
        ConfirmationDialogKind kind,
        double uiScale)
    {
        await DispatchAsync(async () =>
        {
            SukiDialogManager manager = new();
            ISukiDialog? dialog = null;
            manager.OnDialogShown += (_, args) => dialog = args.Dialog;
            RecordingUiScaleService uiScaleService = new(uiScale);
            ConfirmationDialogPresenter presenter = CreatePresenter(
                manager,
                uiScaleService);

            Task<bool> resultTask = presenter.ShowAsync(
                CreatePresentation(kind),
                CancellationToken.None);

            SukiDialog shownDialog = GetShownDialog(dialog)
                .Should()
                .BeOfType<SukiDialog>()
                .Subject;
            ScaleTransform scaleTransform = shownDialog.RenderTransform
                .Should()
                .BeOfType<ScaleTransform>()
                .Subject;
            scaleTransform.ScaleX.Should().Be(uiScale);
            scaleTransform.ScaleY.Should().Be(uiScale);
            shownDialog.RenderTransformOrigin.Should().Be(RelativePoint.Center);
            presenter.Dismiss();
            await resultTask;
        });
    }

    [Fact]
    public async Task ShowAsync_WhenUiScaleChanges_UpdatesOpenDialogScale()
    {
        await DispatchAsync(async () =>
        {
            const double InitialScale = 0.6d;
            const double UpdatedScale = 1.5d;
            SukiDialogManager manager = new();
            ISukiDialog? dialog = null;
            manager.OnDialogShown += (_, args) => dialog = args.Dialog;
            RecordingUiScaleService uiScaleService = new(InitialScale);
            ConfirmationDialogPresenter presenter = CreatePresenter(
                manager,
                uiScaleService);
            Task<bool> resultTask = presenter.ShowAsync(
                CreatePresentation(ConfirmationDialogKind.Standard),
                CancellationToken.None);
            SukiDialog shownDialog = GetShownDialog(dialog)
                .Should()
                .BeOfType<SukiDialog>()
                .Subject;

            uiScaleService.SetScale(UpdatedScale);

            ScaleTransform scaleTransform = shownDialog.RenderTransform
                .Should()
                .BeOfType<ScaleTransform>()
                .Subject;
            scaleTransform.ScaleX.Should().Be(UpdatedScale);
            scaleTransform.ScaleY.Should().Be(UpdatedScale);
            presenter.Dismiss();
            await resultTask;
        });
    }

    private static ConfirmationDialogPresentation CreatePresentation(
        ConfirmationDialogKind kind)
    {
        return new ConfirmationDialogPresentation(
            Title,
            Message,
            ConfirmActionText,
            CancelActionText,
            kind,
            ConfirmationDialogBackgroundClickBehavior.Dismiss);
    }

    private static ConfirmationDialogPresenter CreatePresenter(
        ISukiDialogManager manager,
        IUiScaleService? uiScaleService = null)
    {
        return new ConfirmationDialogPresenter(
            manager,
            uiScaleService ?? new RecordingUiScaleService());
    }

    private static ISukiDialog GetShownDialog(ISukiDialog? dialog)
    {
        return dialog ?? throw new InvalidOperationException(
            "The SukiUI dialog was not shown.");
    }
}
