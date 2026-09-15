using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using SukiUI.Enums;
using SukiUI.Toasts;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.ViewModels.Dlss5;

namespace AtomicArt.Desktop.Views.Dlss5;

public sealed class Dlss5OperationToastPresenter : IDisposable
{
    private const double ProgressMinimum = 0d;
    private const double ProgressMaximum = 100d;
    private const double ProgressMinimumWidth = 280d;
    private const double ContentSpacing = 8d;

    private readonly ISukiToastManager _manager;
    private readonly ILocalizationTextProvider _textProvider;
    private Dlss5SessionViewModel? _viewModel;
    private ISukiToast? _toast;
    private TextBlock? _message;
    private ProgressBar? _progress;
    private bool _isInstallCompletedToast;

    public Dlss5OperationToastPresenter(
        ISukiToastManager manager,
        ILocalizationTextProvider textProvider)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    public void Attach(Dlss5SessionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (_viewModel is not null)
        {
            throw new InvalidOperationException("The DLSS 5 notification presenter is already attached.");
        }

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Refresh();
    }

    public void Dispose()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        Dismiss();
        _viewModel = null;
    }

    private void Refresh()
    {
        Dlss5SessionViewModel? viewModel = _viewModel;

        if (viewModel is null)
        {
            Dismiss();
            return;
        }

        if (viewModel.IsInstallCompleted)
        {
            if ((_toast is null) || (!_isInstallCompletedToast))
            {
                ShowInstallCompletedToast();
            }

            return;
        }

        if ((!viewModel.IsLoading) && (!viewModel.IsStartingWorker))
        {
            Dismiss();
            return;
        }

        if ((_toast is null) || (_isInstallCompletedToast))
        {
            ShowProgressToast();
        }

        if (_message is not null)
        {
            _message.Text = _textProvider.Get(viewModel.OperationLocalizationKey);
        }

        if (_progress is not null)
        {
            _progress.IsIndeterminate = viewModel.IsOperationProgressIndeterminate;
            _progress.Value = viewModel.OperationProgress;
        }
    }

    private void ShowProgressToast()
    {
        Dismiss();
        _message = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        _progress = new ProgressBar
        {
            Minimum = ProgressMinimum,
            Maximum = ProgressMaximum,
            MinWidth = ProgressMinimumWidth,
            ShowProgressText = true
        };
        StackPanel content = new()
        {
            Spacing = ContentSpacing,
            Children = { _message, _progress }
        };
        _toast = _manager.CreateToast()
            .WithTitle(_textProvider.Get(Dlss5LocalizationKeys.Title))
            .WithContent(content)
            .WithLoadingState(true)
            .OfType(NotificationType.Information)
            .Queue();
        _isInstallCompletedToast = false;
    }

    private void ShowInstallCompletedToast()
    {
        Dismiss();
        TextBlock content = new()
        {
            Text = _textProvider.Get(Dlss5LocalizationKeys.InstallCompleted.Message),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        _toast = _manager.CreateToast()
            .WithTitle(_textProvider.Get(Dlss5LocalizationKeys.InstallCompleted.Title))
            .WithContent(content)
            .WithActionButton(
                _textProvider.Get(Dlss5LocalizationKeys.InstallCompleted.Okay),
                OnInstallCompletedOkayRequested,
                true,
                SukiButtonStyles.Basic)
            .WithActionButton(
                _textProvider.Get(Dlss5LocalizationKeys.InstallCompleted.Launch),
                OnInstallCompletedLaunchRequested,
                true)
            .OnDismissed(OnInstallCompletedToastDismissed)
            .OfType(NotificationType.Success)
            .Queue();
        _isInstallCompletedToast = true;
    }

    private void Dismiss()
    {
        if ((_toast is not null) && (!_manager.IsDismissed(_toast)))
        {
            _manager.Dismiss(_toast);
        }

        _toast = null;
        _message = null;
        _progress = null;
        _isInstallCompletedToast = false;
    }

    private void OnInstallCompletedOkayRequested(ISukiToast toast)
    {
        _ = toast;
        _viewModel?.CompleteInstallNotification(launch: false);
    }

    private void OnInstallCompletedLaunchRequested(ISukiToast toast)
    {
        _ = toast;
        _viewModel?.CompleteInstallNotification(launch: true);
    }

    private void OnInstallCompletedToastDismissed(
        ISukiToast toast,
        SukiToastDismissSource source)
    {
        _ = toast;
        _ = source;
        _viewModel?.CompleteInstallNotification(launch: false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if ((string.Equals(eventArgs.PropertyName, nameof(Dlss5SessionViewModel.IsLoading), StringComparison.Ordinal))
            || (string.Equals(eventArgs.PropertyName, nameof(Dlss5SessionViewModel.OperationProgress), StringComparison.Ordinal))
            || (string.Equals(eventArgs.PropertyName, nameof(Dlss5SessionViewModel.OperationLocalizationKey), StringComparison.Ordinal))
            || (string.Equals(eventArgs.PropertyName, nameof(Dlss5SessionViewModel.IsOperationProgressIndeterminate), StringComparison.Ordinal))
            || (string.Equals(eventArgs.PropertyName, nameof(Dlss5SessionViewModel.IsStartingWorker), StringComparison.Ordinal))
            || (string.Equals(eventArgs.PropertyName, nameof(Dlss5SessionViewModel.IsInstallCompleted), StringComparison.Ordinal)))
        {
            Refresh();
        }
    }
}
