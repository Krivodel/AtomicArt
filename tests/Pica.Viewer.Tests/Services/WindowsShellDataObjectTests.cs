using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;

namespace Pica.Viewer.Tests.Services;

public sealed class WindowsShellDataObjectTests
{
    [Fact]
    public void Create_WithExistingFile_ReturnsSystemDataObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"Pica-shell-data-object-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(filePath, new byte[] { 1, 2, 3, 4 });

        try
        {
            using WindowsShellDataObject dataObject = WindowsShellDataObject.Create(filePath);

            dataObject.Pointer.Should().NotBe(nint.Zero);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
