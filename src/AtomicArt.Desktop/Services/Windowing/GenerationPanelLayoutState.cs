namespace AtomicArt.Desktop.Services.Windowing;

public sealed class GenerationPanelLayoutState
{
    public double? Width { get; set; }
    public double? Height { get; set; }

    internal GenerationPanelLayoutState CreateNormalized()
    {
        return new GenerationPanelLayoutState
        {
            Width = LayoutDimension.Normalize(Width),
            Height = LayoutDimension.Normalize(Height)
        };
    }
}
