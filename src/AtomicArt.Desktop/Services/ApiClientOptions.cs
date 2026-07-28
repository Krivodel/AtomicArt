namespace AtomicArt.Desktop.Services;

public sealed class ApiClientOptions
{
    public const string SectionName = "Api";

    public int ModelCatalogTimeoutSeconds { get; init; }
    public int MaximumProblemDetailsErrorCodeCharacters { get; init; }
    public int MaximumProblemDetailsResponseBytes { get; init; }

    public static bool IsValid(ApiClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.ModelCatalogTimeoutSeconds > 0
            && options.MaximumProblemDetailsErrorCodeCharacters > 0
            && options.MaximumProblemDetailsResponseBytes > 0;
    }
}
