namespace AtomicArt.Desktop.Resources;

public static class GenerationUiLocalizationKeys
{
    public const string ImageDropLabel = "Generation.ImageDropLabel";

    public static class Prompt
    {
        public const string Placeholder = "Generation.Prompt.Placeholder";
    }

    public static class Actions
    {
        public const string Generate = "Generation.Actions.Generate";
        public const string GenerateWithPrice = "Generation.Actions.GenerateWithPrice";
        public const string PickImagesTitle = "Generation.Actions.PickImagesTitle";
    }

    public static class Attachments
    {
        public const string CounterFormat = "Generation.Attachments.CounterFormat";
        public const string Failed = "Generation.Attachments.Failed";
        public const string NoSlots = "Generation.Attachments.NoSlots";
    }

    public static class Temperature
    {
        public const string ValueFormat = "Generation.Temperature.ValueFormat";
    }

    public static class Errors
    {
        public const string ApiUnavailable = "Generation.Errors.ApiUnavailable";
        public const string Failed = "Generation.Errors.Failed";
        public const string AuthenticationFailed =
            "Generation.Errors.AuthenticationFailed";
        public const string AuthorizationFailed =
            "Generation.Errors.AuthorizationFailed";
        public const string RateLimited = "Generation.Errors.RateLimited";
        public const string InvalidResponse = "Generation.Errors.InvalidResponse";
        public const string TimedOut = "Generation.Errors.TimedOut";
        public const string ProviderUnavailable =
            "Generation.Errors.ProviderUnavailable";
        public const string RequestRejected = "Generation.Errors.RequestRejected";
        public const string ResourceNotFound = "Generation.Errors.ResourceNotFound";
        public const string ProviderInternalError =
            "Generation.Errors.ProviderInternalError";
        public const string ConcurrencyLimitReached =
            "Generation.Errors.ConcurrencyLimitReached";
        public const string InvalidRequest = "Generation.Errors.InvalidRequest";
        public const string InvalidAttempt = "Generation.Errors.InvalidAttempt";
        public const string InvalidParameters = "Generation.Errors.InvalidParameters";
        public const string ResponseTooLarge = "Generation.Errors.ResponseTooLarge";
        public const string TransportInterrupted =
            "Generation.Errors.TransportInterrupted";
        public const string ModelNotFound = "Generation.Errors.ModelNotFound";
        public const string UnsupportedResolution =
            "Generation.Errors.UnsupportedResolution";
        public const string UnsupportedAspectRatio =
            "Generation.Errors.UnsupportedAspectRatio";
        public const string ModelRequestValidation =
            "Generation.Errors.ModelRequestValidation";
        public const string GoogleApiKeyMissing =
            "Generation.Errors.GoogleApiKeyMissing";
        public const string ModelCatalogLoadFailed =
            "Generation.Errors.ModelCatalogLoadFailed";
    }

    public static class Status
    {
        public const string Generated = "Generation.Status.Generated";
        public const string Generating = "Generation.Status.Generating";
    }
}
