namespace AtomicArt.Infrastructure.Generation;

public sealed class TestGenerationOptions
{
    public const string SectionName = "TestGeneration";
    public const long DefaultMaxImageBytes = 500L * 1024L * 1024L;

    public bool Enabled { get; set; }
    public string ImagesDirectory { get; set; } = string.Empty;
    public long MaxImageBytes { get; set; } = DefaultMaxImageBytes;

    public static bool IsValid(TestGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaxImageBytes > 0;
    }
}
