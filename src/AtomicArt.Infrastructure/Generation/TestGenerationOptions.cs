namespace AtomicArt.Infrastructure.Generation;

public sealed class TestGenerationOptions
{
    public const string SectionName = "TestGeneration";

    public int Base64InputBufferSize { get; init; }
    public int Base64OutputBufferSize { get; init; }
    public bool Enabled { get; set; }
    public int FileStreamBufferSize { get; init; }
    public int GenerationDelayMilliseconds { get; init; }
    public string ImagesDirectory { get; set; } = string.Empty;
    public long MaxImageBytes { get; init; }

    public static bool IsValid(TestGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Base64InputBufferSize > 0
            && options.Base64InputBufferSize % 3 == 0
            && options.Base64OutputBufferSize
                >= options.Base64InputBufferSize / 3 * 4
            && options.FileStreamBufferSize > 0
            && options.GenerationDelayMilliseconds >= 0
            && options.MaxImageBytes > 0;
    }
}
