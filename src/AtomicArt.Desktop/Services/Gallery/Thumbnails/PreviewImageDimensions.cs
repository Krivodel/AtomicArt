namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal sealed record PreviewImageDimensions(int Width, int Height)
{
    public double AspectRatio => (double)Width / Height;
}
