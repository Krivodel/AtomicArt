using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed class GenerationMetadataViewModel : ObservableObject, IDisposable
{
    public GenerationItemViewModel Item { get; }
    public DateTime CreatedAt => Item.CreatedAtUtc.ToLocalTime();
    public string Prompt => Item.Prompt;
    public string ModelDisplayName => Item.ModelDisplayName;
    public string Resolution => Item.Resolution;
    public string AspectRatio => GenerationAspectRatios.IsAuto(Item.AspectRatio)
        ? _textProvider.Get(GenerationLocalizationKeys.OptionsAuto)
        : Item.AspectRatio;
    public string PriceCurrency => Item.Price is GenerationPriceDto price
        ? _priceFormatter.FormatCurrency(price)
        : string.Empty;
    public string PriceAmount => Item.Price is GenerationPriceDto price
        ? _priceFormatter.FormatAmount(price)
        : _textProvider.Get(GalleryLocalizationKeys.Metadata.Unavailable);
    public string GenerationDuration => _durationFormatter.Format(Item.GenerationDuration)
        ?? _textProvider.Get(GalleryLocalizationKeys.Metadata.Unavailable);
    public string Status => Item.Status;
    public bool IsGenerated => Item.IsGenerated;
    public bool IsGenerating => Item.IsGenerating;
    public bool IsFailed => Item.IsFailed;
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand OpenViewerCommand { get; }
    public IRelayCommand ShowFailureDetailsCommand { get; }
    public IRelayCommand RepeatCommand { get; }
    public IAsyncRelayCommand CopyPromptCommand { get; }

    private readonly ITextClipboardService _textClipboardService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly GenerationPriceFormatter _priceFormatter;
    private readonly GenerationDurationFormatter _durationFormatter;
    private readonly ILocalizationTextProvider _textProvider;

    private GenerationMetadataViewModel(
        GenerationItemViewModel item,
        IRelayCommand closeCommand,
        IRelayCommand openViewerCommand,
        IRelayCommand showFailureDetailsCommand,
        IRelayCommand repeatCommand,
        ITextClipboardService textClipboardService,
        IViewModelErrorHandler errorHandler,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter,
        ILocalizationTextProvider textProvider)
    {
        Item = item;
        CloseCommand = closeCommand;
        OpenViewerCommand = openViewerCommand;
        ShowFailureDetailsCommand = showFailureDetailsCommand;
        RepeatCommand = repeatCommand;
        _textClipboardService = textClipboardService;
        _errorHandler = errorHandler;
        _priceFormatter = priceFormatter;
        _durationFormatter = durationFormatter;
        _textProvider = textProvider;
        CopyPromptCommand = new AsyncRelayCommand(CopyPromptAsync);
        Item.PropertyChanged += OnItemPropertyChanged;
    }

    public static GenerationMetadataViewModel FromItem(
        GenerationItemViewModel item,
        IRelayCommand closeCommand,
        IRelayCommand openViewerCommand,
        IRelayCommand showFailureDetailsCommand,
        IRelayCommand repeatCommand,
        ITextClipboardService textClipboardService,
        IViewModelErrorHandler errorHandler,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter,
        ILocalizationTextProvider textProvider)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(closeCommand);
        ArgumentNullException.ThrowIfNull(openViewerCommand);
        ArgumentNullException.ThrowIfNull(showFailureDetailsCommand);
        ArgumentNullException.ThrowIfNull(repeatCommand);
        ArgumentNullException.ThrowIfNull(textClipboardService);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(priceFormatter);
        ArgumentNullException.ThrowIfNull(durationFormatter);
        ArgumentNullException.ThrowIfNull(textProvider);

        return new GenerationMetadataViewModel(
            item,
            closeCommand,
            openViewerCommand,
            showFailureDetailsCommand,
            repeatCommand,
            textClipboardService,
            errorHandler,
            priceFormatter,
            durationFormatter,
            textProvider);
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(AspectRatio));
        OnPropertyChanged(nameof(PriceAmount));
        OnPropertyChanged(nameof(GenerationDuration));
        OnPropertyChanged(nameof(Status));
    }

    public void Dispose()
    {
        Item.PropertyChanged -= OnItemPropertyChanged;
    }

    private async Task CopyPromptAsync(CancellationToken ct)
    {
        try
        {
            await _textClipboardService.SetTextAsync(Prompt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(CopyPromptAsync));
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            OnPropertyChanged(string.Empty);
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(GenerationItemViewModel.CreatedAtUtc):
                OnPropertyChanged(nameof(CreatedAt));
                break;
            case nameof(GenerationItemViewModel.Price):
                OnPropertyChanged(nameof(PriceCurrency));
                OnPropertyChanged(nameof(PriceAmount));
                break;
            case nameof(GenerationItemViewModel.Prompt):
            case nameof(GenerationItemViewModel.ModelDisplayName):
            case nameof(GenerationItemViewModel.Resolution):
            case nameof(GenerationItemViewModel.AspectRatio):
            case nameof(GenerationItemViewModel.GenerationDuration):
            case nameof(GenerationItemViewModel.Status):
            case nameof(GenerationItemViewModel.IsGenerated):
            case nameof(GenerationItemViewModel.IsGenerating):
            case nameof(GenerationItemViewModel.IsFailed):
                OnPropertyChanged(e.PropertyName);
                break;
        }
    }
}
