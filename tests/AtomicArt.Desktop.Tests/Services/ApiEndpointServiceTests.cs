using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ApiEndpointServiceTests
{
    [Fact]
    public void CreateRequestUri_WithConfiguredBasePath_AppendsRelativeRoute()
    {
        IApiEndpointService service = TestApiEndpointServiceFactory.Create(
            "https://atomicart.test/root/");

        Uri requestUri = service.CreateRequestUri("api/v1/generations");

        requestUri.Should().Be(new Uri("https://atomicart.test/root/api/v1/generations"));
    }

    [Fact]
    public void SetBaseAddress_WithChangedAddress_IncrementsRevisionAndRaisesEvent()
    {
        IApiEndpointService service = TestApiEndpointServiceFactory.Create();
        int eventCount = 0;
        service.BaseAddressChanged += (_, _) => eventCount++;
        ApiBaseAddress.TryCreate(
            "https://second.atomicart.test/",
            out ApiBaseAddress? secondAddress).Should().BeTrue();

        service.SetBaseAddress(secondAddress
            ?? throw new InvalidOperationException("Second address is required."));

        service.Revision.Should().Be(1);
        service.BaseAddress.Should().Be(secondAddress);
        eventCount.Should().Be(1);
    }

    [Fact]
    public void SetBaseAddress_WithSameAddress_DoesNotChangeRevisionOrRaiseEvent()
    {
        IApiEndpointService service = TestApiEndpointServiceFactory.Create();
        int eventCount = 0;
        service.BaseAddressChanged += (_, _) => eventCount++;

        service.SetBaseAddress(service.BaseAddress);

        service.Revision.Should().Be(0);
        eventCount.Should().Be(0);
    }
}
