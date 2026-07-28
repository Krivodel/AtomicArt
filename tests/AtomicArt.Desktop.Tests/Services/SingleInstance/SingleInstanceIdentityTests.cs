using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.SingleInstance;

namespace AtomicArt.Desktop.Tests.Services.SingleInstance;

public sealed class SingleInstanceIdentityTests
{
    [Fact]
    public void CreateDefault_WithCurrentSession_UsesStableApplicationIdentity()
    {
        SingleInstanceIdentity identity =
            SingleInstanceIdentity.CreateDefault();
        string identitySuffix = Path.GetFileNameWithoutExtension(
            identity.LockFilePath);

        Path.GetDirectoryName(identity.LockFilePath)
            .Should()
            .EndWith(Path.Combine("AtomicArt", "Instance"));
        identitySuffix.Should().HaveLength(24);
        identity.PipeName.Should().Be($"AtomicArt-{identitySuffix}");
    }
}
