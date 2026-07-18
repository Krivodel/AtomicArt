using System.Globalization;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed class GenerationMetadataViewModel
{
    public string CreatedAtUtc { get; }
    public string Prompt { get; }
    public string ModelDisplayName { get; }
    public string Resolution { get; }
    public string AspectRatio { get; }
    public string AttachedImagesCount { get; }
    public string Price { get; }
    public string GenerationDuration { get; }
    public string ImagePath { get; }
    public string Status { get; }
    public IRelayCommand CloseCommand { get; }

    private GenerationMetadataViewModel(
        GenerationItemViewModel item,
        IRelayCommand closeCommand,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter)
    {
        CreatedAtUtc = item.CreatedAtUtc.ToString("u", CultureInfo.InvariantCulture);
        Prompt = item.Prompt;
        ModelDisplayName = item.ModelDisplayName;
        Resolution = item.Resolution;
        AspectRatio = item.AspectRatio;
        AttachedImagesCount = item.AttachedImagesCount.ToString(CultureInfo.InvariantCulture);
        Price = priceFormatter.Format(item.Price) ?? UiStrings.MetadataUnavailable;
        GenerationDuration = durationFormatter.Format(item.GenerationDuration) ?? UiStrings.MetadataUnavailable;
        ImagePath = item.ImagePath ?? UiStrings.MetadataNoFilePath;
        Status = item.Status;
        CloseCommand = closeCommand;
    }

    public static GenerationMetadataViewModel FromItem(
        GenerationItemViewModel item,
        IRelayCommand closeCommand,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(closeCommand);
        ArgumentNullException.ThrowIfNull(priceFormatter);
        ArgumentNullException.ThrowIfNull(durationFormatter);

        return new GenerationMetadataViewModel(item, closeCommand, priceFormatter, durationFormatter);
    }
}
