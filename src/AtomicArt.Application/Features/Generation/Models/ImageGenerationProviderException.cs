namespace AtomicArt.Application.Features.Generation.Models;

public class ImageGenerationProviderException : InvalidOperationException
{
    public ImageGenerationProviderFailureKind FailureKind { get; }
    public bool Retryable { get; }

    public ImageGenerationProviderException(
        ImageGenerationProviderFailureKind failureKind,
        string message)
        : this(failureKind, message, false)
    {
    }

    public ImageGenerationProviderException(
        ImageGenerationProviderFailureKind failureKind,
        string message,
        bool retryable)
        : base(message)
    {
        FailureKind = failureKind;
        Retryable = retryable;
    }

    public ImageGenerationProviderException(
        ImageGenerationProviderFailureKind failureKind,
        string message,
        bool retryable,
        Exception innerException)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        Retryable = retryable;
    }
}
