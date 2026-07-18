using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

internal static class TestApiEndpointServiceFactory
{
    public static IApiEndpointService Create(string baseAddress = TestApiConfiguration.BaseAddress)
    {
        return new ApiEndpointService(TestApiConfiguration.Create(baseAddress));
    }
}
