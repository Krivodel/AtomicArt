using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Settings;

public sealed class GpuResourceCacheStartupSettingsReaderTests
{
    [Fact]
    public void LoadMaxGpuResourceSizeBytes_WithCustomDataRoot_ReadsSettingsFromCustomRoot()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(GpuResourceCacheStartupSettingsReaderTests));

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            Directory.CreateDirectory(pathProvider.StateDirectory);
            string settingsPath = Path.Combine(
                pathProvider.StateDirectory,
                SettingsStateSection.SectionFileName);
            File.WriteAllText(
                settingsPath,
                $$"""
                {
                  "schemaVersion": 1,
                  "savedAtUtc": "2026-07-25T00:00:00Z",
                  "payload": {
                    "values": {
                      "{{GpuResourceCacheSettingDefinition.SettingKey}}": "256mb"
                    }
                  }
                }
                """);

            long sizeBytes =
                GpuResourceCacheStartupSettingsReader.LoadMaxGpuResourceSizeBytes(
                    pathProvider);

            sizeBytes.Should().Be(256L * 1024L * 1024L);
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }
}
