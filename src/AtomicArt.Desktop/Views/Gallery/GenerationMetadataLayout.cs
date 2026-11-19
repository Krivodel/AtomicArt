using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationMetadataLayout : Grid
{
    private double _startHeight;
    private double _progress = 1d;

    internal void SetHeightTransition(double startHeight, double progress)
    {
        _startHeight = startHeight;
        _progress = progress;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size naturalSize = base.MeasureOverride(availableSize);
        if (_progress == 1d)
        {
            return naturalSize;
        }

        double height = Math.Min(availableSize.Height, _startHeight + ((naturalSize.Height - _startHeight) * _progress));
        if (height < naturalSize.Height)
        {
            base.MeasureOverride(availableSize.WithHeight(height));
        }

        return naturalSize.WithHeight(height);
    }
}
