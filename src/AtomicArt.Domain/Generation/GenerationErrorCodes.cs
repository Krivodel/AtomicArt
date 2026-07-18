namespace AtomicArt.Domain.Generation;

public static class GenerationErrorCodes
{
    public const string ModelNotFound = "ERR-GEN-001";
    public const string UnsupportedResolution = "ERR-GEN-002";
    public const string UnsupportedAspectRatio = "ERR-GEN-003";
    public const string ModelRequestValidation = "ERR-GEN-004";
    public const string InvalidModelId = "ERR-GEN-100";
    public const string InvalidConstraintLimit = "ERR-GEN-101";
    public const string MissingConstraintValues = "ERR-GEN-102";
    public const string InvalidAttachmentTotalLimit = "ERR-GEN-103";
    public const string InvalidTemperatureBounds = "ERR-GEN-104";
    public const string InvalidTemperatureDefault = "ERR-GEN-105";
    public const string InvalidTemperatureStep = "ERR-GEN-106";
    public const string InvalidThinkingMetadata = "ERR-GEN-111";
}
