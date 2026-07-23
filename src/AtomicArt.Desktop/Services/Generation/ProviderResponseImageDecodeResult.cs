namespace AtomicArt.Desktop.Services.Generation;

public sealed class ProviderResponseImageDecodeResult
{
    public bool HasImage { get; private set; }

    internal void SetHasImage()
    {
        HasImage = true;
    }
}
