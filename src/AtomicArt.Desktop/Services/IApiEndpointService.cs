namespace AtomicArt.Desktop.Services;

public interface IApiEndpointService
{
    ApiBaseAddress BaseAddress { get; }
    long Revision { get; }

    event EventHandler? BaseAddressChanged;

    Uri CreateRequestUri(string relativePath);
    void SetBaseAddress(ApiBaseAddress baseAddress);
}
