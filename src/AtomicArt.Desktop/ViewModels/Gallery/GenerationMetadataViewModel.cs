using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed class GenerationMetadataViewModel : ObservableObject, IDisposable
{
    public GenerationItemViewModel Item { get; }
    public string CreatedDate => Item.CreatedAtUtc
        .ToLocalTime()
        .ToString("d MMM yyyy 'г.'", RussianCulture);
    public string CreatedTime => Item.CreatedAtUtc
        .ToLocalTime()
        .ToString("HH:mm", RussianCulture);
    public string Prompt => Item.Prompt;
    public string ModelDisplayName => Item.ModelDisplayName;
    public string Resolution => Item.Resolution;
    public string AspectRatio => Item.AspectRatio;
    public string AttachedImagesCount => Item.AttachedImagesCount.ToString(CultureInfo.InvariantCulture);
    public string PriceCurrency => Item.Price is GenerationPriceDto price
        ? _priceFormatter.FormatCurrency(price)
        : string.Empty;
    public string PriceAmount => Item.Price is GenerationPriceDto price
        ? _priceFormatter.FormatAmount(price)
        : UiStrings.MetadataUnavailable;
    public string GenerationDuration => _durationFormatter.Format(Item.GenerationDuration)
        ?? UiStrings.MetadataUnavailable;
    public string ImagePath => Item.ImagePath ?? UiStrings.MetadataNoFilePath;
    public string Status => Item.Status;
    public bool IsGenerated => Item.IsGenerated;
    public bool IsGenerating => Item.IsGenerating;
    public bool IsFailed => Item.IsFailed;
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand OpenViewerCommand { get; }
    public IRelayCommand RepeatCommand { get; }
    public IAsyncRelayCommand CopyPromptCommand { get; }
    public IAsyncRelayCommand CopyImagePathCommand { get; }

    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    private readonly ITextClipboardService _textClipboardService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly GenerationPriceFormatter _priceFormatter;
    private readonly GenerationDurationFormatter _durationFormatter;

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
        Item = item;
        CloseCommand = closeCommand;
        OpenViewerCommand = openViewerCommand;
        RepeatCommand = repeatCommand;
        _textClipboardService = textClipboardService;
        _errorHandler = errorHandler;
        _priceFormatter = priceFormatter;
        _durationFormatter = durationFormatter;
        CopyPromptCommand = new AsyncRelayCommand(CopyPromptAsync);
        CopyImagePathCommand = new AsyncRelayCommand(CopyImagePathAsync, CanCopyImagePath);
        Item.PropertyChanged += OnItemPropertyChanged;
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

    public void Dispose()
    {
        Item.PropertyChanged -= OnItemPropertyChanged;
    }

    private Task CopyPromptAsync(CancellationToken ct)
    {
        return CopyTextAsync(Prompt, nameof(CopyPromptAsync), ct);
    }

    private Task CopyImagePathAsync(CancellationToken ct)
    {
        string? imagePath = Item.ImagePath;

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return Task.CompletedTask;
        }

        return CopyTextAsync(imagePath, nameof(CopyImagePathAsync), ct);
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

    private bool CanCopyImagePath()
    {
        return !string.IsNullOrWhiteSpace(Item.ImagePath);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            OnPropertyChanged(string.Empty);
            CopyImagePathCommand.NotifyCanExecuteChanged();
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(GenerationItemViewModel.CreatedAtUtc):
                OnPropertyChanged(nameof(CreatedDate));
                OnPropertyChanged(nameof(CreatedTime));
                break;
            case nameof(GenerationItemViewModel.Price):
                OnPropertyChanged(nameof(PriceCurrency));
                OnPropertyChanged(nameof(PriceAmount));
                break;
            case nameof(GenerationItemViewModel.ImagePath):
                OnPropertyChanged(nameof(ImagePath));
                CopyImagePathCommand.NotifyCanExecuteChanged();
                break;
            case nameof(GenerationItemViewModel.Prompt):
            case nameof(GenerationItemViewModel.ModelDisplayName):
            case nameof(GenerationItemViewModel.Resolution):
            case nameof(GenerationItemViewModel.AspectRatio):
            case nameof(GenerationItemViewModel.AttachedImagesCount):
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
