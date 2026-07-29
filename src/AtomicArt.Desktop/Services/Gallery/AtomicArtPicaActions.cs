using Pica.Protocol;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class AtomicArtPicaActions
{
    public const string AttachId = "atomicart.attach";
    public const string ShowInGalleryId = "atomicart.show-in-gallery";

    public PicaActionDefinition Attach => new(
        AttachId,
        _textProvider.Get(GalleryLocalizationKeys.Actions.Attach),
        AttachIconGeometry,
        AttachIconRotationDegrees,
        PicaActionTargets.CurrentImage | PicaActionTargets.Selection,
        AttachOrder);
    public PicaActionDefinition ShowInGallery => new(
        ShowInGalleryId,
        _textProvider.Get(GalleryLocalizationKeys.Actions.ShowInGallery),
        ShowInGalleryIconGeometry,
        ShowInGalleryIconRotationDegrees,
        PicaActionTargets.CurrentImage,
        ShowInGalleryOrder);

    private const string AttachIconGeometry = "M16,9 L16,4 L17,4 L17,2 L7,2 L7,4 L8,4 L8,9 L6,11 L6,13 L11.2,13 L11.2,21 L12.8,21 L12.8,13 L18,13 L18,11 Z";
    private const double AttachIconRotationDegrees = 45d;
    private const int AttachOrder = 100;
    private const string ShowInGalleryIconGeometry = "M10,3 C6.13,3 3,6.13 3,10 C3,13.87 6.13,17 10,17 C11.57,17 13.02,16.48 14.18,15.61 L20.59,22 L22,20.59 L15.61,14.18 C16.48,13.02 17,11.57 17,10 C17,6.13 13.87,3 10,3 Z M10,5 C12.76,5 15,7.24 15,10 C15,12.76 12.76,15 10,15 C7.24,15 5,12.76 5,10 C5,7.24 7.24,5 10,5 Z";
    private const double ShowInGalleryIconRotationDegrees = 0d;
    private const int ShowInGalleryOrder = 110;

    private readonly ILocalizationTextProvider _textProvider;

    public AtomicArtPicaActions(ILocalizationTextProvider textProvider)
    {
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
    }
}
