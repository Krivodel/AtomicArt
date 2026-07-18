using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GallerySceneTopLevelContext
{
    internal TopLevel TopLevel =>
        _topLevel ?? throw new InvalidOperationException("Gallery scene top level was not configured.");

    private TopLevel? _topLevel;

    internal void Attach(TopLevel topLevel)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
    }
}
