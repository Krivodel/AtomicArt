using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.Logging;

public sealed class DesktopFileLoggingOptions
{
    public const string SectionName = "Logging:File";

    public LogLevel MinimumLevel { get; }
    public long MaxFileSizeBytes { get; }
    public int MaximumExceptionDepth { get; }
    public int MaximumMessageCharacters { get; }
    public int MaximumPausedBufferBytes { get; }
    public int MaximumSanitizedMessageCharacters { get; }
    public int MaximumSanitizerInputMessageCharacters { get; }
    public int MaximumStackFrameCount { get; }
    public int RetainedFileCount { get; }
    public int RetentionDays { get; }

    private const int MaximumAllowedRetainedFileCount = 90;
    private const int MaximumAllowedRetentionDays = 365;
    private const long MaximumAllowedFileSizeBytes = 100 * 1024 * 1024;
    private const long MinimumAllowedFileSizeBytes = 64 * 1024;

    public DesktopFileLoggingOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration
            .GetRequiredSection(SectionName);
        MinimumLevel = GetRequiredValue<LogLevel>(section, nameof(MinimumLevel));
        MaxFileSizeBytes = GetRequiredValue<long>(section, nameof(MaxFileSizeBytes));
        MaximumExceptionDepth = GetRequiredValue<int>(
            section,
            nameof(MaximumExceptionDepth));
        MaximumMessageCharacters = GetRequiredValue<int>(
            section,
            nameof(MaximumMessageCharacters));
        MaximumPausedBufferBytes = GetRequiredValue<int>(
            section,
            nameof(MaximumPausedBufferBytes));
        MaximumSanitizedMessageCharacters = GetRequiredValue<int>(
            section,
            nameof(MaximumSanitizedMessageCharacters));
        MaximumSanitizerInputMessageCharacters = GetRequiredValue<int>(
            section,
            nameof(MaximumSanitizerInputMessageCharacters));
        MaximumStackFrameCount = GetRequiredValue<int>(
            section,
            nameof(MaximumStackFrameCount));
        RetainedFileCount = GetRequiredValue<int>(
            section,
            nameof(RetainedFileCount));
        RetentionDays = GetRequiredValue<int>(section, nameof(RetentionDays));

        Validate();
    }

    private static T GetRequiredValue<T>(
        IConfigurationSection section,
        string key)
        where T : struct
    {
        return section.GetValue<T?>(key)
            ?? throw new InvalidOperationException(
                $"Logging configuration value '{SectionName}:{key}' is missing.");
    }

    private void Validate()
    {
        if (MaxFileSizeBytes is < MinimumAllowedFileSizeBytes
            or > MaximumAllowedFileSizeBytes
            || MaximumExceptionDepth <= 0
            || MaximumMessageCharacters <= 0
            || MaximumPausedBufferBytes <= 0
            || MaximumSanitizedMessageCharacters <= 0
            || MaximumSanitizerInputMessageCharacters
                < MaximumSanitizedMessageCharacters
            || MaximumStackFrameCount <= 0
            || RetainedFileCount is < 1 or > MaximumAllowedRetainedFileCount
            || RetentionDays is < 1 or > MaximumAllowedRetentionDays)
        {
            throw new InvalidOperationException(
                "File logging configuration contains values outside the allowed range.");
        }
    }
}
