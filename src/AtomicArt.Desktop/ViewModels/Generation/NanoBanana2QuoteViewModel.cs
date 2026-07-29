using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class NanoBanana2QuoteViewModel : ObservableObject, IGenerationModelViewModel
{
    private readonly GenerationPricePreviewEstimator _pricePreviewEstimator;
    private readonly NanoBanana2PanelTextFormatter _textFormatter;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenerateButtonText))]
    private decimal _estimatedPrice;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenerateButtonText))]
    private string? _estimatedPriceCurrency;

    public string GenerateButtonText => _textFormatter.FormatGenerateButtonText(
        EstimatedPrice,
        EstimatedPriceCurrency);

    public NanoBanana2QuoteViewModel(
        GenerationPricePreviewEstimator pricePreviewEstimator,
        NanoBanana2PanelTextFormatter textFormatter)
    {
        ArgumentNullException.ThrowIfNull(pricePreviewEstimator);
        ArgumentNullException.ThrowIfNull(textFormatter);

        _pricePreviewEstimator = pricePreviewEstimator;
        _textFormatter = textFormatter;
    }

    public void Refresh(NanoBanana2GenerationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        GenerationPriceDto? price = _pricePreviewEstimator.Estimate(parameters);
        ApplyPricePreview(price);
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(GenerateButtonText));
    }

    private void ApplyPricePreview(GenerationPriceDto? price)
    {
        EstimatedPrice = price?.Amount ?? 0m;
        EstimatedPriceCurrency = price?.CurrencyCode;
    }
}
