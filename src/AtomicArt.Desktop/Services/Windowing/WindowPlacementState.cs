namespace AtomicArt.Desktop.Services.Windowing;

public sealed class WindowPlacementState
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public bool IsMaximized { get; set; }

    internal WindowPlacementState CreateNormalized()
    {
        bool hasValidPosition = X is not null && Y is not null;
        bool hasValidSize = IsValidDimension(Width)
            && IsValidDimension(Height);

        return new WindowPlacementState
        {
            X = hasValidPosition ? X : null,
            Y = hasValidPosition ? Y : null,
            Width = hasValidSize ? Width : null,
            Height = hasValidSize ? Height : null,
            IsMaximized = IsMaximized
        };
    }

    private static bool IsValidDimension(double? value)
    {
        return value is not null
            && double.IsFinite(value.Value)
            && value.Value > 0d;
    }
}
