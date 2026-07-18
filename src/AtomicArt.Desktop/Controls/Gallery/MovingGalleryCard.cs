using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed record MovingGalleryCard(
    Guid Id,
    Control Control,
    Rect First,
    Rect Last,
    double Dx,
    double Dy,
    int Row,
    int Column);
