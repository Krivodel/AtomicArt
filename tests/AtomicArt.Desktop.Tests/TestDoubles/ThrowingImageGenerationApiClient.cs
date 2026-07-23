using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class ThrowingImageGenerationApiClient : IImageGenerationApiClient
{
    private const string GenerationApiFailureMessage = "Generation API failed.";
    private const string UnavailableMessage = "Unavailable";

    private readonly Action<string>? _providerCredentialValidator;
    private readonly string _exceptionMessage;

    public ThrowingImageGenerationApiClient()
    {
        _exceptionMessage = GenerationApiFailureMessage;
    }

    private ThrowingImageGenerationApiClient(
        string exceptionMessage,
        Action<string> providerCredentialValidator)
    {
        _exceptionMessage = exceptionMessage;
        _providerCredentialValidator = providerCredentialValidator;
    }

    public static ThrowingImageGenerationApiClient CreateUnavailableWithRequiredProviderCredential()
    {
        return new ThrowingImageGenerationApiClient(
            UnavailableMessage,
            ValidateRequiredProviderCredential);
    }

    public Task<GenerationBatchDto> CreateGenerationAsync(
        ImageGenerationRequestDto request,
        Guid logicalGenerationId,
        int attemptNumber,
        string providerCredential,
        CancellationToken ct = default)
    {
        _providerCredentialValidator?.Invoke(providerCredential);

        throw new HttpRequestException(_exceptionMessage);
    }

    private static void ValidateRequiredProviderCredential(string providerCredential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCredential);
    }
}
