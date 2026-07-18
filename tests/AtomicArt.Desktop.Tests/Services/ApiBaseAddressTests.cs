using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ApiBaseAddressTests
{
    [Theory]
    [InlineData("http://localhost:5000", "http://localhost:5000/")]
    [InlineData(" https://atomicart.test ", "https://atomicart.test/")]
    [InlineData("https://atomicart.test/api", "https://atomicart.test/api/")]
    [InlineData("http://127.0.0.1:5123/base/", "http://127.0.0.1:5123/base/")]
    public void TryCreate_WithSupportedAddress_NormalizesValue(
        string value,
        string expectedValue)
    {
        bool created = ApiBaseAddress.TryCreate(value, out ApiBaseAddress? baseAddress);

        created.Should().BeTrue();
        baseAddress.Should().NotBeNull();
        baseAddress?.ToString().Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("localhost:5000")]
    [InlineData("/api")]
    [InlineData("ftp://atomicart.test/")]
    [InlineData("https://user:password@atomicart.test/")]
    [InlineData("https://atomicart.test/?key=value")]
    [InlineData("https://atomicart.test/#fragment")]
    public void TryCreate_WithUnsupportedAddress_ReturnsFalse(string? value)
    {
        bool created = ApiBaseAddress.TryCreate(value, out ApiBaseAddress? baseAddress);

        created.Should().BeFalse();
        baseAddress.Should().BeNull();
    }
}
