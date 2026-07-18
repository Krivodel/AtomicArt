using Microsoft.Extensions.Configuration;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

internal static class TestApiEndpointServiceFactory
{
    public static IApiEndpointService Create(string baseAddress = "https://atomicart.test/")
    {
        Dictionary<string, string?> values = new(StringComparer.Ordinal)
        {
            ["Api:BaseAddress"] = baseAddress
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new ApiEndpointService(configuration);
    }
}
