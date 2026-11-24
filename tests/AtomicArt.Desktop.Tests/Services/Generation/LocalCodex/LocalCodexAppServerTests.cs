using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Generation.LocalCodex;

public sealed class LocalCodexAppServerTests
{
    private const string ThreadId = "11111111-1111-1111-1111-111111111111";
    private const string OtherThreadId = "22222222-2222-2222-2222-222222222222";

    [Theory]
    [InlineData(ThreadId, false)]
    [InlineData(OtherThreadId, true)]
    public async Task DeleteGeneratedImage_WithImportedResult_DeletesOnlyTheCurrentThreadFile(string fileThreadId, bool expectedExists)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(LocalCodexAppServerTests));

        try
        {
            string directory = LocalCodexAppServer.GetGeneratedImageDirectory(root, fileThreadId);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "image.png");
            await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3 });
            string otherPath = Path.Combine(directory, "other.png");
            await File.WriteAllBytesAsync(otherPath, new byte[] { 4, 5, 6 });
            LocalCodexAppServer server = new(NullLogger<LocalCodexAppServer>.Instance);
            JsonElement image = JsonSerializer.SerializeToElement(new { savedPath = path });

            server.DeleteGeneratedImage(root, ThreadId, image);

            File.Exists(path).Should().Be(expectedExists);
            File.Exists(otherPath).Should().BeTrue();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Fact]
    public void DeleteGeneratedImage_WithMalformedOptionalPath_DoesNotFailTheImportedGeneration()
    {
        LocalCodexAppServer server = new(NullLogger<LocalCodexAppServer>.Instance);
        JsonElement image = JsonSerializer.SerializeToElement(new { savedPath = Path.Combine(Path.GetTempPath(), "invalid\0.png") });

        Action delete = () => server.DeleteGeneratedImage(Path.GetTempPath(), ThreadId, image);

        delete.Should().NotThrow();
    }
}
