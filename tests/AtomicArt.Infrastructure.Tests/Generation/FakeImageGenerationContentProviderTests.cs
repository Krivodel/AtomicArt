using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation;

public sealed class FakeImageGenerationContentProviderTests
{
    [Fact]
    public async Task GetContentAsync_WithMultipleCalls_ReturnsContentEveryTime()
    {
        byte[] attachedContent = [0x01, 0x02, 0x03];
        TestPlaceholderImageProvider placeholderImageProvider = new();
        FakeImageGenerationContentProvider provider = new(placeholderImageProvider);
        ImageGenerationRequestDto request = CreateRequest(attachedContent);

        ImageGenerationContentResult firstContent = await provider.GetContentAsync(
            CreateContext(request, 0),
            CancellationToken.None);
        ImageGenerationContentResult secondContent = await provider.GetContentAsync(
            CreateContext(request, 1),
            CancellationToken.None);

        firstContent.ContentType.Should().Be("image/png");
        firstContent.Base64Data.Should().NotBeNullOrWhiteSpace();
        secondContent.ContentType.Should().Be("image/png");
        secondContent.Base64Data.Should().NotBeNullOrWhiteSpace();
        Convert.FromBase64String(firstContent.Base64Data).Should().Equal(CreatePlaceholderContent());
        Convert.FromBase64String(secondContent.Base64Data).Should().Equal(CreatePlaceholderContent());
        placeholderImageProvider.ItemIndexes.Should().Equal([0, 1]);
        request.ModelId.Should().Be("nano-banana-2");
        request.AttachedImages.Should().ContainSingle();
        request.AttachedImages.Single().Content.Should().Equal(attachedContent);
    }

    [Fact]
    public async Task GetContentAsync_WithCancellation_StopsWithoutSwallowingCancellation()
    {
        TestPlaceholderImageProvider placeholderImageProvider = new();
        FakeImageGenerationContentProvider provider = new(placeholderImageProvider);
        ImageGenerationRequestDto request = CreateRequest([]);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => provider.GetContentAsync(CreateContext(request, 0), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        placeholderImageProvider.ItemIndexes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetContentAsync_WithOutputRootConfigured_DoesNotCreateFiles()
    {
        string outputRoot = CreateCleanDirectory(nameof(GetContentAsync_WithOutputRootConfigured_DoesNotCreateFiles));
        string currentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(outputRoot);
            TestPlaceholderImageProvider placeholderImageProvider = new();
            FakeImageGenerationContentProvider provider = new(placeholderImageProvider);
            ImageGenerationRequestDto request = CreateRequest([]);

            ImageGenerationContentResult result = await provider.GetContentAsync(
                CreateContext(request, 0),
                CancellationToken.None);

            result.Base64Data.Should().NotBeNullOrWhiteSpace();
            Directory
                .EnumerateFileSystemEntries(outputRoot, "*", SearchOption.AllDirectories)
                .Should()
                .BeEmpty();
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
            DeleteDirectoryIfExists(outputRoot);
        }
    }

    private static ImageGenerationRequestDto CreateRequest(byte[] attachedContent)
    {
        AttachedImageDto attachedImage = new(
            "source.png",
            "image/png",
            attachedContent);

        return new ImageGenerationRequestDto(
            "nano-banana-2",
            "Prompt",
            "Авто",
            "1k",
            1d,
            1,
            [attachedImage]);
    }

    private static ImageGenerationContentProviderContext CreateContext(
        ImageGenerationRequestDto request,
        int itemIndex)
    {
        return new ImageGenerationContentProviderContext(
            request,
            GenerationProviderIds.Google,
            "test-provider-model",
            CreatePricing(),
            itemIndex,
            "test-provider-key");
    }

    private static GenerationModelPricingMetadataDto CreatePricing()
    {
        return new GenerationModelPricingMetadataDto(
            "USD",
            0.50m,
            3.00m,
            60.00m,
            1120,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["1k"] = 1120
            });
    }

    private static byte[] CreatePlaceholderContent()
    {
        return GenerationImageFileSignatures.Png.ToArray();
    }

    private static string CreateCleanDirectory(string testName)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.Infrastructure.Tests",
            testName);

        DeleteDirectoryIfExists(directoryPath);
        Directory.CreateDirectory(directoryPath);

        return directoryPath;
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
        }
    }

    private sealed class TestPlaceholderImageProvider : IPlaceholderImageProvider
    {
        private readonly List<int> _itemIndexes = [];

        public IReadOnlyList<int> ItemIndexes => _itemIndexes;

        public Task<PlaceholderImage> GetNextAsync(
            string modelId,
            int itemIndex,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _itemIndexes.Add(itemIndex);
            PlaceholderImage image = new(
                "image/png",
                CreatePlaceholderContent());

            return Task.FromResult(image);
        }
    }
}
