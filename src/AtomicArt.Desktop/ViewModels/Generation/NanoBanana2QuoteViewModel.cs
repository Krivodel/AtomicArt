using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class NanoBanana2QuoteViewModel : ObservableObject, IGenerationModelViewModel
{
    private readonly GenerationPricePreviewEstimator _pricePreviewEstimator;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenerateButtonText))]
    private decimal _estimatedPrice;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenerateButtonText))]
    private string? _estimatedPriceCurrency;

    public string GenerateButtonText => NanoBanana2PanelTextFormatter.FormatGenerateButtonText(
        EstimatedPrice,
        EstimatedPriceCurrency);

    public NanoBanana2QuoteViewModel(GenerationPricePreviewEstimator pricePreviewEstimator)
    {
        ArgumentNullException.ThrowIfNull(pricePreviewEstimator);

        _pricePreviewEstimator = pricePreviewEstimator;
    }

    public void Refresh(NanoBanana2GenerationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        GenerationPriceDto? price = _pricePreviewEstimator.Estimate(parameters);
        ApplyPricePreview(price);
    }

    private void ApplyPricePreview(GenerationPriceDto? price)
    {
        EstimatedPrice = price?.Amount ?? 0m;
        EstimatedPriceCurrency = price?.CurrencyCode;
    }
}
