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
        ApiEndpointChangeTestContext context = new();
        ApiBaseAddress.TryCreate(
            "https://second.atomicart.test/",
            out ApiBaseAddress? secondAddress).Should().BeTrue();

        context.Service.SetBaseAddress(secondAddress
            ?? throw new InvalidOperationException("Second address is required."));

        context.Service.Revision.Should().Be(1);
        context.Service.BaseAddress.Should().Be(secondAddress);
        context.EventCount.Should().Be(1);
    }

    [Fact]
    public void SetBaseAddress_WithSameAddress_DoesNotChangeRevisionOrRaiseEvent()
    {
        ApiEndpointChangeTestContext context = new();

        context.Service.SetBaseAddress(context.Service.BaseAddress);

        context.Service.Revision.Should().Be(0);
        context.EventCount.Should().Be(0);
    }

    private sealed class ApiEndpointChangeTestContext
    {
        public IApiEndpointService Service { get; }
        public int EventCount { get; private set; }

        public ApiEndpointChangeTestContext()
        {
            Service = TestApiEndpointServiceFactory.Create();
            Service.BaseAddressChanged += OnBaseAddressChanged;
        }

        private void OnBaseAddressChanged(object? sender, EventArgs e)
        {
            EventCount++;
        }
    }
}
