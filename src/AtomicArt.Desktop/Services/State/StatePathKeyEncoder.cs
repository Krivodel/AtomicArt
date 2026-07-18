using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.State;

public sealed class StatePathKeyEncoder : IStatePathKeyEncoder
{
    public string Encode(string key)
    {
        return SafeFileNameKeyEncoder.EncodeSha256Hex(key);
    }
}
