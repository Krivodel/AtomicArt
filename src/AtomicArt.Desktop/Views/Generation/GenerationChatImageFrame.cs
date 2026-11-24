using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AtomicArt.Desktop.Views.Generation;

internal sealed class GenerationChatImageFrame : Decorator
{
    public IImage? Preview
    {
        get => GetValue(PreviewProperty);
        set => SetValue(PreviewProperty, value);
    }
    public double PreviewAspectRatio
    {
        get => GetValue(PreviewAspectRatioProperty);
        set => SetValue(PreviewAspectRatioProperty, value);
    }

    public static readonly StyledProperty<IImage?> PreviewProperty =
        AvaloniaProperty.Register<GenerationChatImageFrame, IImage?>(nameof(Preview));
    public static readonly StyledProperty<double> PreviewAspectRatioProperty =
        AvaloniaProperty.Register<GenerationChatImageFrame, double>(
            nameof(PreviewAspectRatio), defaultValue: 1d, validate: value => double.IsFinite(value) && value > 0d);

    private Size ImageSize => Preview?.Size ?? new Size(PreviewAspectRatio, 1d);

    static GenerationChatImageFrame()
    {
        AffectsMeasure<GenerationChatImageFrame>(PreviewProperty, PreviewAspectRatioProperty);
    }

    internal static Size Fit(Size available, Size image)
    {
        double ratio = image.Width > 0d && image.Height > 0d ? image.AspectRatio : 1d;
        double width = Math.Min(available.Width, available.Height * ratio);

        if (double.IsPositiveInfinity(width))
        {
            width = image.Width;
        }

        return new Size(width, width / ratio);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size size = GenerationChatImageFrame.Fit(availableSize, ImageSize);
        Child?.Measure(size);

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size size = GenerationChatImageFrame.Fit(finalSize, ImageSize);
        Child?.Arrange(new Rect((finalSize.Width - size.Width) / 2d, 0d, size.Width, size.Height));

        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PreviewProperty && Preview?.Size is { Width: > 0d, Height: > 0d } size)
        {
            SetCurrentValue(PreviewAspectRatioProperty, size.AspectRatio);
        }
    }
}
