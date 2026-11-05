using System.Reflection;

using FluentAssertions;
using Pica.Viewer.Services;
using SukiUI.Controls;
using Xunit;

namespace AtomicArt.Desktop.Tests;

public sealed class DesktopDependencyCompatibilityTests
{
    [Fact]
    public void SukiUiAssembly_WithPicaReference_SatisfiesRequiredVersion()
    {
        AssemblyName loadedAssembly = typeof(SukiWindow).Assembly.GetName();
        AssemblyName requiredAssembly = typeof(IPicaImageBitmapSource).Assembly
            .GetReferencedAssemblies()
            .Single(reference => string.Equals(reference.Name, loadedAssembly.Name, StringComparison.Ordinal));
        Version loadedVersion = loadedAssembly.Version
            ?? throw new InvalidOperationException("The loaded SukiUI assembly has no version.");
        Version requiredVersion = requiredAssembly.Version
            ?? throw new InvalidOperationException("The Pica SukiUI reference has no version.");

        int comparison = loadedVersion.CompareTo(requiredVersion);

        comparison.Should().BeGreaterThanOrEqualTo(0,
            "SukiUI {0} must satisfy the Pica reference to {1}", loadedVersion, requiredVersion);
    }
}
