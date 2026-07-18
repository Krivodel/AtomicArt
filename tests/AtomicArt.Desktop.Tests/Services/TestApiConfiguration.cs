using Microsoft.Extensions.Configuration;

namespace AtomicArt.Desktop.Tests.Services;

internal static class TestApiConfiguration
{
    public const string BaseAddress = "https://atomicart.test/";

    public static IConfiguration Create(string baseAddress = BaseAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);

        Dictionary<string, string?> values = new(StringComparer.Ordinal)
        {
            ["Api:BaseAddress"] = baseAddress
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
