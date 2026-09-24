using Pica.Protocol;
using Pica.Viewer.Resources;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class AtomicArtPicaActions
{
    public const string AttachId = "atomicart.attach";
    public const string ImbaId = "atomicart.imba";
    public const string OpenDlss5Id = "atomicart.open-dlss5";
    public const string ShowInGalleryId = "atomicart.show-in-gallery";

    public PicaActionDefinition Attach => new(
        AttachId,
        _textProvider.Get(GalleryLocalizationKeys.Actions.Attach),
        AttachIconGeometry,
        NoIconRotationDegrees,
        PicaActionTargets.CurrentImage | PicaActionTargets.Selection,
        AttachOrder);
    public PicaActionDefinition Imba => new(
        ImbaId,
        GetImbaDisplayName(isFavorite: false),
        ViewerActionIconGeometry.Star,
        NoIconRotationDegrees,
        PicaActionTargets.CurrentImage,
        ImbaOrder)
    {
        UseOutlineIcon = true
    };
    public PicaActionDefinition ShowInGallery => new(
        ShowInGalleryId,
        _textProvider.Get(GalleryLocalizationKeys.Actions.ShowInGallery),
        ShowInGalleryIconGeometry,
        NoIconRotationDegrees,
        PicaActionTargets.CurrentImage,
        ShowInGalleryOrder);
    public PicaActionDefinition OpenDlss5 => new(
        OpenDlss5Id,
        _textProvider.Get(GalleryLocalizationKeys.Actions.OpenDlss5),
        Dlss5FeatureDefinition.NvidiaIconGeometry,
        NoIconRotationDegrees,
        PicaActionTargets.CurrentImage | PicaActionTargets.Selection,
        OpenDlss5Order)
    {
        SelectionPlacement = PicaSelectionActionPlacement.AfterSave
    };

    public string GetImbaDisplayName(bool isFavorite)
    {
        return _textProvider.Get(
            GalleryLocalizationKeys.Actions.GetImbaActionKey(isFavorite));
    }

    private const string AttachIconGeometry = "M16.24,2.93 L21.07,7.76 C22.4,9.08 22.03,11.32 20.35,12.16 L15.48,14.6 C15.31,14.69 15.17,14.84 15.11,15.02 L13.67,19.19 C13.37,20.06 12.26,20.32 11.6,19.67 L8.5,16.56 L4.06,21 H3 V19.94 L7.44,15.5 L4.33,12.4 C3.68,11.74 3.94,10.63 4.81,10.33 L8.98,8.89 C9.16,8.83 9.32,8.69 9.4,8.52 L11.84,3.65 C12.68,1.97 14.92,1.6 16.24,2.93 Z M20.01,8.82 L15.18,3.99 C14.58,3.39 13.56,3.55 13.18,4.32 L10.74,9.19 C10.48,9.71 10.02,10.12 9.47,10.31 L5.68,11.62 L12.38,18.32 L13.69,14.53 C13.88,13.98 14.29,13.52 14.81,13.26 L19.68,10.82 C20.45,10.44 20.61,9.42 20.01,8.82 Z";
    private const int AttachOrder = 100;
    private const int ImbaOrder = 101;
    private const int OpenDlss5Order = 102;
    private const double NoIconRotationDegrees = 0d;
    private const string ShowInGalleryIconGeometry = "M10,3 C6.13,3 3,6.13 3,10 C3,13.87 6.13,17 10,17 C11.57,17 13.02,16.48 14.18,15.61 L20.59,22 L22,20.59 L15.61,14.18 C16.48,13.02 17,11.57 17,10 C17,6.13 13.87,3 10,3 Z M10,5 C12.76,5 15,7.24 15,10 C15,12.76 12.76,15 10,15 C7.24,15 5,12.76 5,10 C5,7.24 7.24,5 10,5 Z";
    private const int ShowInGalleryOrder = 110;

    private readonly ILocalizationTextProvider _textProvider;

    public AtomicArtPicaActions(ILocalizationTextProvider textProvider)
    {
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
    }
}
