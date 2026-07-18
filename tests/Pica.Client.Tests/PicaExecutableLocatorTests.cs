using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;
using Pica.Protocol;

namespace Pica.Client.Tests;

public sealed class PicaExecutableLocatorTests
{
    [Fact]
    public void FindExecutablePath_WhenCandidateExists_ReturnsFullPath()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string executablePath = Path.Combine(directoryPath, PicaProtocolConstants.ExecutableName);
        Directory.CreateDirectory(directoryPath);
        File.WriteAllBytes(executablePath, Array.Empty<byte>());
        MakeExecutableOnUnix(executablePath);
        FixedPicaExecutableSource source = new(executablePath);
        PicaExecutableLocator locator = new(
            new List<IPicaExecutableSource> { source },
            NullLogger<PicaExecutableLocator>.Instance);

        try
        {
            string? result = locator.FindExecutablePath();

            result.Should().Be(Path.GetFullPath(executablePath));
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public void FindExecutablePath_WhenCandidatesDoNotExist_ReturnsNull()
    {
        FixedPicaExecutableSource source = new(@"Z:\Missing\Pica.exe");
        PicaExecutableLocator locator = new(
            new List<IPicaExecutableSource> { source },
            NullLogger<PicaExecutableLocator>.Instance);

        string? result = locator.FindExecutablePath();

        result.Should().BeNull();
    }

    private static void MakeExecutableOnUnix(string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            filePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
