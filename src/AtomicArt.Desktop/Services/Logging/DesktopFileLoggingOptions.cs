using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.Logging;

public sealed class DesktopFileLoggingOptions
{
    private const int DefaultRetainedFileCount = 14;
    private const int DefaultRetentionDays = 14;
    private const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;
    private const int MaxRetainedFileCount = 90;
    private const int MaxRetentionDays = 365;
    private const long MaxAllowedFileSizeBytes = 100 * 1024 * 1024;
    private const long MinAllowedFileSizeBytes = 64 * 1024;

    public LogLevel MinimumLevel { get; }
    public long MaxFileSizeBytes { get; }
    public int RetainedFileCount { get; }
    public int RetentionDays { get; }

    public DesktopFileLoggingOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection("Logging:File");
        MinimumLevel = section.GetValue("MinimumLevel", LogLevel.Information);
        MaxFileSizeBytes = Math.Clamp(
            section.GetValue("MaxFileSizeBytes", DefaultMaxFileSizeBytes),
            MinAllowedFileSizeBytes,
            MaxAllowedFileSizeBytes);
        RetainedFileCount = Math.Clamp(
            section.GetValue("RetainedFileCount", DefaultRetainedFileCount),
            1,
            MaxRetainedFileCount);
        RetentionDays = Math.Clamp(
            section.GetValue("RetentionDays", DefaultRetentionDays),
            1,
            MaxRetentionDays);
    }
}
