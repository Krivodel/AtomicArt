using System.ComponentModel;

using Avalonia.Controls;
using Avalonia.Controls.Notifications;

using SukiUI.Enums;
using SukiUI.Toasts;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.ViewModels.Updates;

namespace AtomicArt.Desktop.Views.Updates;

public sealed class ApplicationUpdateToastPresenter : IDisposable
{
    public ISukiToastManager Manager => _manager;

    private readonly ISukiToastManager _manager;
    private ApplicationUpdateViewModel? _viewModel;
    private ISukiToast? _toast;
    private TextBlock? _messageText;
    private TextBlock? _errorText;
    private ProgressBar? _progressBar;
    private Button? _updateButton;
    private ApplicationUpdateState _presentedState;
    private bool _isDisposed;

    public ApplicationUpdateToastPresenter(ISukiToastManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public void Attach(ApplicationUpdateViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (_viewModel is not null)
        {
            throw new InvalidOperationException("The update notification presenter is already attached.");
        }

        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RefreshNotification();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        DismissCurrentToast();
    }

    private StackPanel CreateContent()
    {
        _messageText = new TextBlock
        {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        _errorText = new TextBlock
        {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        _progressBar = new ProgressBar
        {
            Maximum = 100d,
            MinWidth = 280d,
            ShowProgressText = true
        };

        StackPanel content = new()
        {
            Spacing = 8d,
            Children =
            {
                _messageText,
                _progressBar,
                _errorText
            }
        };

        return content;
    }

    private void ShowAvailableToast()
    {
        ApplicationUpdateViewModel? viewModel = _viewModel;

        if (viewModel is null)
        {
            return;
        }

        DismissCurrentToast();
        StackPanel content = CreateContent();
        _toast = _manager
            .CreateToast()
            .WithTitle(UiStrings.UpdateTitle)
            .WithContent(content)
            .OfType(NotificationType.Information)
            .WithActionButton(
                UiStrings.UpdateLater,
                OnUpdateLaterRequested,
                true,
                SukiButtonStyles.Basic)
            .WithActionButton(
                viewModel.UpdateActionText,
                OnUpdateRequested,
                true)
            .Queue();
        _updateButton = _toast.ActionButtons
            .OfType<Button>()
            .LastOrDefault();
        _presentedState = ApplicationUpdateState.Available;
        RefreshContent(viewModel);
    }

    private void ShowProgressToast()
    {
        ApplicationUpdateViewModel? viewModel = _viewModel;

        if (viewModel is null)
        {
            return;
        }

        DismissCurrentToast();
        StackPanel content = CreateContent();
        _toast = _manager
            .CreateToast()
            .WithTitle(UiStrings.UpdateTitle)
            .WithContent(content)
            .WithLoadingState(true)
            .OfType(NotificationType.Information)
            .Queue();
        _presentedState = viewModel.State;
        RefreshContent(viewModel);
    }

    private void RefreshNotification()
    {
        ApplicationUpdateViewModel? viewModel = _viewModel;

        if (viewModel is null)
        {
            return;
        }

        if (viewModel.State == ApplicationUpdateState.Hidden)
        {
            DismissCurrentToast();
            _presentedState = ApplicationUpdateState.Hidden;
            return;
        }

        if (viewModel.State == ApplicationUpdateState.Available)
        {
            if ((_toast is null) || (_presentedState != ApplicationUpdateState.Available))
            {
                ShowAvailableToast();
                return;
            }

            RefreshContent(viewModel);
            return;
        }

        if ((_toast is null) || (_presentedState == ApplicationUpdateState.Available))
        {
            ShowProgressToast();
            return;
        }

        _presentedState = viewModel.State;
        RefreshContent(viewModel);
    }

    private void RefreshContent(ApplicationUpdateViewModel viewModel)
    {
        if (_messageText is not null)
        {
            _messageText.Text = viewModel.Message;
        }

        if (_errorText is not null)
        {
            _errorText.Text = viewModel.ErrorMessage;
            _errorText.IsVisible = viewModel.HasErrorMessage;
        }

        if (_progressBar is not null)
        {
            _progressBar.IsVisible = viewModel.IsProgressVisible;
            _progressBar.IsIndeterminate = viewModel.IsWaitingForGeneration;
            _progressBar.Value = viewModel.DownloadProgress;
        }

        if (_updateButton is not null)
        {
            _updateButton.Content = viewModel.UpdateActionText;
        }

        if (_toast is not null)
        {
            _toast.LoadingState = viewModel.IsProgressVisible;
        }
    }

    private void DismissCurrentToast()
    {
        if ((_toast is not null) && !_manager.IsDismissed(_toast))
        {
            _manager.Dismiss(_toast);
        }

        _toast = null;
        _messageText = null;
        _errorText = null;
        _progressBar = null;
        _updateButton = null;
    }

    private void OnUpdateRequested(ISukiToast toast)
    {
        _ = toast;

        if (_viewModel is not null)
        {
            _ = _viewModel.UpdateCommand.ExecuteAsync(null);
        }
    }

    private void OnUpdateLaterRequested(ISukiToast toast)
    {
        _ = toast;
        _viewModel?.UpdateLaterCommand.Execute(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshNotification();
    }
}
