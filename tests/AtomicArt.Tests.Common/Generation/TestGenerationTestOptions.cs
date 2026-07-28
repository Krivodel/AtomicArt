using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Tests.Common.Generation;

public static class TestGenerationTestOptions
{
    public const string DisplayName = "Test";
    public const string ModelId = "test";
    public const string ProviderModelId = "test-folder";

    public static TestGenerationOptions Create(
        bool enabled = false,
        string imagesDirectory = "",
        long maxImageBytes = 500L * 1024L * 1024L)
    {
        return new TestGenerationOptions
        {
            Base64InputBufferSize = 48,
            Base64OutputBufferSize = 64,
            Enabled = enabled,
            FileStreamBufferSize = 4096,
            GenerationDelayMilliseconds = 0,
            ImagesDirectory = imagesDirectory,
            MaxImageBytes = maxImageBytes
        };
    }

    public static Dictionary<string, string?> CreateConfiguration(
        bool enabled = false,
        string imagesDirectory = "")
    {
        TestGenerationOptions options = Create(
            enabled,
            imagesDirectory);
        string sectionName = TestGenerationOptions.SectionName;

        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{sectionName}:{nameof(options.Base64InputBufferSize)}"] =
                options.Base64InputBufferSize.ToString(),
            [$"{sectionName}:{nameof(options.Base64OutputBufferSize)}"] =
                options.Base64OutputBufferSize.ToString(),
            [$"{sectionName}:{nameof(options.Enabled)}"] =
                options.Enabled.ToString(),
            [$"{sectionName}:{nameof(options.FileStreamBufferSize)}"] =
                options.FileStreamBufferSize.ToString(),
            [$"{sectionName}:{nameof(options.GenerationDelayMilliseconds)}"] =
                options.GenerationDelayMilliseconds.ToString(),
            [$"{sectionName}:{nameof(options.ImagesDirectory)}"] =
                options.ImagesDirectory,
            [$"{sectionName}:{nameof(options.MaxImageBytes)}"] =
                options.MaxImageBytes.ToString()
        };
    }

    public static TestGenerationModelMetadata CreateModelMetadata()
    {
        return new TestGenerationModelMetadata
        {
            AspectRatios = ["Авто", "1:1", "16:9"],
            DisplayName = DisplayName,
            Id = ModelId,
            ProviderModelId = ProviderModelId,
            Resolutions = ["1K"]
        };
    }
}
