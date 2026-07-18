using Pica.Protocol;

namespace AtomicArt.Desktop.Services.Gallery;

public static class AtomicArtPicaActions
{
    public const string AttachId = "atomicart.attach";

    public static PicaActionDefinition Attach { get; } = new(
        AttachId,
        "Прикрепить",
        AttachIconGeometry,
        AttachIconRotationDegrees,
        PicaActionTargets.CurrentImage | PicaActionTargets.Selection,
        AttachOrder);

    private const string AttachIconGeometry = "M16,9 L16,4 L17,4 L17,2 L7,2 L7,4 L8,4 L8,9 L6,11 L6,13 L11.2,13 L11.2,21 L12.8,21 L12.8,13 L18,13 L18,11 Z";
    private const double AttachIconRotationDegrees = 45d;
    private const int AttachOrder = 100;
}
