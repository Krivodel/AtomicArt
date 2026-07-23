namespace AtomicArt.Contracts.Generation;

public static class GenerationApiRoutes
{
    public const string Generations = "api/v2/generations";
    public const string MetadataPartName = "metadata";
    public const string AttachmentPartNamePrefix = "attachment-";
    public const string ProviderResponsePartName = "provider-response";
    public const string GenerationMetadataPartName = "generation-metadata";
    public const string ProblemDetailsErrorCodeExtensionName = "code";
    public const string ProblemDetailsProviderFailureKindExtensionName = "providerFailureKind";
    public const string ProblemDetailsRetryableExtensionName = "retryable";
    public const string ProblemDetailsLogicalGenerationIdExtensionName = "logicalGenerationId";
    public const string ProblemDetailsAttemptNumberExtensionName = "attemptNumber";
    public const string ProviderApiKeyHeaderName = "X-AtomicArt-Provider-Api-Key";
    public const string Models = "api/v1/generation-models";
}
