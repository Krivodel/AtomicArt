using System.Globalization;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed class GenerationMetadataViewModel
{
    public GenerationItemViewModel Item { get; }
    public string CreatedAtUtc { get; }
    public string CreatedDate { get; }
    public string CreatedTime { get; }
    public string Prompt { get; }
    public string ModelDisplayName { get; }
    public string Resolution { get; }
    public string AspectRatio { get; }
    public string AttachedImagesCount { get; }
    public string Price { get; }
    public string PriceCurrency { get; }
    public string PriceAmount { get; }
    public string GenerationDuration { get; }
    public string ImagePath { get; }
    public string Status { get; }
    public bool IsGenerated { get; }
    public bool IsGenerating { get; }
    public bool IsFailed { get; }
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand RequestCloseCommand { get; }
    public IRelayCommand OpenViewerCommand { get; }
    public IRelayCommand RepeatCommand { get; }
    public IRelayCommand RequestRepeatCommand { get; }
    public IAsyncRelayCommand CopyPromptCommand { get; }
    public IAsyncRelayCommand CopyImagePathCommand { get; }

    public event EventHandler<GenerationMetadataActionRequestedEventArgs>? ActionRequested;

    private const string UsdCurrencyCode = "USD";

    private readonly ITextClipboardService _textClipboardService;
    private readonly IViewModelErrorHandler _errorHandler;

    private GenerationMetadataViewModel(
        GenerationItemViewModel item,
        IRelayCommand closeCommand,
        IRelayCommand openViewerCommand,
        IRelayCommand repeatCommand,
        ITextClipboardService textClipboardService,
        IViewModelErrorHandler errorHandler,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter)
    {
        DateTime createdAt = item.CreatedAtUtc.ToLocalTime();
        CultureInfo russianCulture = CultureInfo.GetCultureInfo("ru-RU");
        GenerationPriceDto? price = item.Price;

        Item = item;
        CreatedAtUtc = item.CreatedAtUtc.ToString("u", CultureInfo.InvariantCulture);
        CreatedDate = createdAt.ToString("d MMM yyyy 'г.'", russianCulture);
        CreatedTime = createdAt.ToString("HH:mm", russianCulture);
        Prompt = item.Prompt;
        ModelDisplayName = item.ModelDisplayName;
        Resolution = item.Resolution;
        AspectRatio = item.AspectRatio;
        AttachedImagesCount = item.AttachedImagesCount.ToString(CultureInfo.InvariantCulture);
        Price = priceFormatter.Format(price) ?? UiStrings.MetadataUnavailable;
        PriceCurrency = GetPriceCurrency(price);
        PriceAmount = price?.Amount.ToString("0.####", CultureInfo.InvariantCulture)
            ?? UiStrings.MetadataUnavailable;
        GenerationDuration = durationFormatter.Format(item.GenerationDuration)
            ?? UiStrings.MetadataUnavailable;
        ImagePath = item.ImagePath ?? UiStrings.MetadataNoFilePath;
        Status = item.Status;
        IsGenerated = item.IsGenerated;
        IsGenerating = item.IsGenerating;
        IsFailed = item.IsFailed;
        CloseCommand = closeCommand;
        RequestCloseCommand = new RelayCommand(RequestClose);
        OpenViewerCommand = openViewerCommand;
        RepeatCommand = repeatCommand;
        RequestRepeatCommand = new RelayCommand(RequestRepeat);
        _textClipboardService = textClipboardService;
        _errorHandler = errorHandler;
        CopyPromptCommand = new AsyncRelayCommand(CopyPromptAsync);
        CopyImagePathCommand = new AsyncRelayCommand(CopyImagePathAsync);
    }

    public static GenerationMetadataViewModel FromItem(
        GenerationItemViewModel item,
        IRelayCommand closeCommand,
        IRelayCommand openViewerCommand,
        IRelayCommand repeatCommand,
        ITextClipboardService textClipboardService,
        IViewModelErrorHandler errorHandler,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(closeCommand);
        ArgumentNullException.ThrowIfNull(openViewerCommand);
        ArgumentNullException.ThrowIfNull(repeatCommand);
        ArgumentNullException.ThrowIfNull(textClipboardService);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(priceFormatter);
        ArgumentNullException.ThrowIfNull(durationFormatter);

        return new GenerationMetadataViewModel(
            item,
            closeCommand,
            openViewerCommand,
            repeatCommand,
            textClipboardService,
            errorHandler,
            priceFormatter,
            durationFormatter);
    }

    private static string GetPriceCurrency(GenerationPriceDto? price)
    {
        if (price is null)
        {
            return string.Empty;
        }

        if (string.Equals(
            price.CurrencyCode,
            UsdCurrencyCode,
            StringComparison.OrdinalIgnoreCase))
        {
            return "$";
        }

        return price.CurrencyCode;
    }

    private Task CopyPromptAsync(CancellationToken ct)
    {
        return CopyTextAsync(Prompt, nameof(CopyPromptAsync), ct);
    }

    private Task CopyImagePathAsync(CancellationToken ct)
    {
        return CopyTextAsync(ImagePath, nameof(CopyImagePathAsync), ct);
    }

    private async Task CopyTextAsync(string text, string operationName, CancellationToken ct)
    {
        try
        {
            await _textClipboardService.SetTextAsync(text, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, operationName);
        }
    }

    private void RequestClose()
    {
        ActionRequested?.Invoke(
            this,
            new GenerationMetadataActionRequestedEventArgs(CloseCommand, null));
    }

    private void RequestRepeat()
    {
        ActionRequested?.Invoke(
            this,
            new GenerationMetadataActionRequestedEventArgs(RepeatCommand, Item));
    }
}
