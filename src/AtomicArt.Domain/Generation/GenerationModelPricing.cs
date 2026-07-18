using AtomicArt.Domain.Exceptions;

namespace AtomicArt.Domain.Generation;

public sealed record GenerationModelPricing
{
    private const decimal TokenPriceUnit = 1_000_000m;

    public string ModelId { get; }
    public string CurrencyCode { get; }
    public decimal InputTokenUsdPerMillion { get; }
    public decimal TextOutputTokenUsdPerMillion { get; }
    public decimal ImageOutputTokenUsdPerMillion { get; }
    public int InputImageTokens { get; }
    public IReadOnlyDictionary<string, int> OutputImageTokensByResolution { get; }

    public GenerationModelPricing(
        string modelId,
        string currencyCode,
        decimal inputTokenUsdPerMillion,
        decimal textOutputTokenUsdPerMillion,
        decimal imageOutputTokenUsdPerMillion,
        int inputImageTokens,
        IReadOnlyDictionary<string, int> outputImageTokensByResolution)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new DomainException(
                GenerationErrorCodes.ModelRequestValidation,
                "The pricing model identifier must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new DomainException(
                GenerationErrorCodes.ModelRequestValidation,
                "The pricing currency must not be empty.");
        }

        ModelId = modelId.Trim();
        CurrencyCode = currencyCode.Trim();
        InputTokenUsdPerMillion = RequirePositive(inputTokenUsdPerMillion, nameof(inputTokenUsdPerMillion));
        TextOutputTokenUsdPerMillion = RequirePositive(textOutputTokenUsdPerMillion, nameof(textOutputTokenUsdPerMillion));
        ImageOutputTokenUsdPerMillion = RequirePositive(imageOutputTokenUsdPerMillion, nameof(imageOutputTokenUsdPerMillion));
        InputImageTokens = RequirePositive(inputImageTokens, nameof(inputImageTokens));
        OutputImageTokensByResolution = CreateOutputImageTokensSnapshot(outputImageTokensByResolution);
    }

    public decimal CalculateUsagePrice(
        int inputTokens,
        int textOutputTokens,
        int imageOutputTokens)
    {
        if (inputTokens < 0)
        {
            throw new DomainException(
                GenerationErrorCodes.ModelRequestValidation,
                "The input token count must not be negative.");
        }

        if (textOutputTokens < 0)
        {
            throw new DomainException(
                GenerationErrorCodes.ModelRequestValidation,
                "The text output token count must not be negative.");
        }

        if (imageOutputTokens < 0)
        {
            throw new DomainException(
                GenerationErrorCodes.ModelRequestValidation,
                "The image output token count must not be negative.");
        }

        return CalculateTokenPrice(inputTokens, InputTokenUsdPerMillion)
            + CalculateTokenPrice(textOutputTokens, TextOutputTokenUsdPerMillion)
            + CalculateTokenPrice(imageOutputTokens, ImageOutputTokenUsdPerMillion);
    }

    private static decimal RequirePositive(decimal value, string parameterName)
    {
        if (value <= 0m)
        {
            throw CreateInvalidPositiveParameterException(parameterName);
        }

        return value;
    }

    private static decimal CalculateTokenPrice(decimal tokens, decimal usdPerMillion)
    {
        return tokens * usdPerMillion / TokenPriceUnit;
    }

    private static int RequirePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw CreateInvalidPositiveParameterException(parameterName);
        }

        return value;
    }

    private static DomainException CreateInvalidPositiveParameterException(string parameterName)
    {
        return new DomainException(
            GenerationErrorCodes.ModelRequestValidation,
            $"Pricing parameter '{parameterName}' must be positive.");
    }

    private static IReadOnlyDictionary<string, int> CreateOutputImageTokensSnapshot(
        IReadOnlyDictionary<string, int> outputImageTokensByResolution)
    {
        if (outputImageTokensByResolution is null || outputImageTokensByResolution.Count == 0)
        {
            throw new DomainException(
                GenerationErrorCodes.ModelRequestValidation,
                "Model pricing must define image output tokens by resolution.");
        }

        Dictionary<string, int> snapshot = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, int> value in outputImageTokensByResolution)
        {
            if (string.IsNullOrWhiteSpace(value.Key))
            {
                throw new DomainException(
                    GenerationErrorCodes.ModelRequestValidation,
                    "A model pricing resolution key must not be empty.");
            }

            string resolution = value.Key.Trim();
            int imageOutputTokens = RequirePositive(value.Value, nameof(outputImageTokensByResolution));

            if (!snapshot.TryAdd(resolution, imageOutputTokens))
            {
                throw new DomainException(
                    GenerationErrorCodes.ModelRequestValidation,
                    $"Model pricing contains duplicate resolution '{resolution}'.");
            }
        }

        return snapshot;
    }
}
