namespace AtomicArt.Desktop.Services.Updates;

public sealed class ApplicationUpdateOptions
{
    public const string SectionName = "Updates";

    public int CheckIntervalMinutes { get; init; }
    public string RepositoryUrl { get; init; } = string.Empty;

    public static bool IsValid(ApplicationUpdateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.CheckIntervalMinutes > 0
            && Uri.TryCreate(
                options.RepositoryUrl,
                UriKind.Absolute,
                out Uri? repositoryUri)
            && repositoryUri.Scheme == Uri.UriSchemeHttps;
    }
}
