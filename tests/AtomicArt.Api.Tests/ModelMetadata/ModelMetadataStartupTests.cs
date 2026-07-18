using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using FluentAssertions;
using Xunit;

using AtomicArt.Infrastructure.Generation;
using AtomicArt.Tests.Common;

namespace AtomicArt.Api.Tests.ModelMetadata;

public sealed class ModelMetadataStartupTests
{
    [Fact]
    public void Startup_WithInvalidModelMetadataJson_ThrowsInvalidOperationException()
    {
        string contentRootPath = TestDirectories.GetUniqueAssemblyDirectoryPath(
            typeof(ModelMetadataStartupTests));
        using TemporaryDirectory contentRoot = new(contentRootPath);
        ApiContentRootTestFiles.WriteModelMetadata(contentRoot.DirectoryPath, "{");
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseContentRoot(contentRoot.DirectoryPath));

        Action action = () => factory.CreateClient();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*malformed JSON*");
    }
}
