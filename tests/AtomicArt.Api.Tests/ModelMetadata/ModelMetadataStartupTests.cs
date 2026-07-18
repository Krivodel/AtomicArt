using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using FluentAssertions;
using Xunit;

using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Api.Tests.ModelMetadata;

public sealed class ModelMetadataStartupTests
{
    [Fact]
    public void Startup_WithInvalidModelMetadataJson_ThrowsInvalidOperationException()
    {
        string contentRoot = CreateContentRoot("{");

        try
        {
            using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseContentRoot(contentRoot));

            Action action = () => factory.CreateClient();

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*malformed JSON*");
        }
        finally
        {
            DeleteDirectoryIfExists(contentRoot);
        }
    }

    private static string CreateContentRoot(string metadataJson)
    {
        string contentRoot = Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.Api.Tests",
            nameof(ModelMetadataStartupTests),
            Guid.NewGuid().ToString("N"));
        string metadataPath = Path.Combine(
            contentRoot,
            GenerationModelCatalogDefaults.RelativePath);
        string metadataDirectory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidOperationException("Model metadata directory was not found.");

        Directory.CreateDirectory(metadataDirectory);
        File.WriteAllText(
            metadataPath,
            metadataJson,
            Encoding.UTF8);

        return contentRoot;
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }
    }
}
