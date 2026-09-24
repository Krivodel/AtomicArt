using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using SukiUI.Toasts;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Views.Gallery;

public sealed class GallerySaveToastPresenter : IGallerySaveNotificationService
{
    private const double FileNameMaxWidth = 400d;

    private readonly ISukiToastManager _manager;
    private readonly ILocalizationTextProvider _textProvider;
    private ISukiToast? _toast;

    public GallerySaveToastPresenter(
        ISukiToastManager manager,
        ILocalizationTextProvider textProvider)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    public void ShowSaving(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        Dismiss();

        _toast = _manager.CreateToast()
            .WithTitle(_textProvider.Get(GalleryLocalizationKeys.Notifications.SavingTitle))
            .WithContent(new TextBlock
            {
                Text = fileName,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = FileNameMaxWidth
            })
            .WithLoadingState(true)
            .OfType(NotificationType.Information)
            .Queue();
    }

    public void Dismiss()
    {
        if (_toast is not null && !_manager.IsDismissed(_toast))
        {
            _manager.Dismiss(_toast);
        }

        _toast = null;
    }
}
