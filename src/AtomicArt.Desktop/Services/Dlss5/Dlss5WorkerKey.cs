namespace AtomicArt.Desktop.Services.Dlss5;

internal readonly record struct Dlss5WorkerKey
{
    public int Width { get; }
    public int Height { get; }
    public Dlss5RenderSettings Settings { get; }

    public Dlss5WorkerKey(int width, int height, Dlss5RenderSettings settings)
    {
        Width = width;
        Height = height;
        Settings = (settings ?? throw new ArgumentNullException(nameof(settings))).NormalizeForNative();
    }

    public bool Matches(int width, int height, Dlss5RenderSettings settings)
    {
        return this == new Dlss5WorkerKey(width, height, settings);
    }
}
