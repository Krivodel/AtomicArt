using Microsoft.Extensions.Configuration;

namespace AtomicArt.Desktop.Services;

public sealed class ApiEndpointService : IApiEndpointService
{
    public ApiBaseAddress BaseAddress
    {
        get
        {
            lock (_syncRoot)
            {
                return _baseAddress;
            }
        }
    }

    public long Revision
    {
        get
        {
            lock (_syncRoot)
            {
                return _revision;
            }
        }
    }

    public event EventHandler? BaseAddressChanged;

    private const string BaseAddressConfigurationKey = "Api:BaseAddress";

    private readonly object _syncRoot = new();
    private ApiBaseAddress _baseAddress;
    private long _revision;

    public ApiEndpointService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string configuredAddress = configuration[BaseAddressConfigurationKey]
            ?? throw new InvalidOperationException("API base address is not configured.");

        if (!ApiBaseAddress.TryCreate(configuredAddress, out ApiBaseAddress? baseAddress)
            || baseAddress is null)
        {
            throw new InvalidOperationException("Configured API base address is invalid.");
        }

        _baseAddress = baseAddress;
    }

    public Uri CreateRequestUri(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ApiBaseAddress baseAddress = BaseAddress;

        return new Uri(baseAddress.Value, relativePath);
    }

    public void SetBaseAddress(ApiBaseAddress baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        bool changed;

        lock (_syncRoot)
        {
            changed = !Equals(_baseAddress.Value, baseAddress.Value);

            if (changed)
            {
                _baseAddress = baseAddress;
                _revision++;
            }
        }

        if (changed)
        {
            BaseAddressChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
