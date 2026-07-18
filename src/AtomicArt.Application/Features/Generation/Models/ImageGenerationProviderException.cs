namespace AtomicArt.Application.Features.Generation.Models;

public class ImageGenerationProviderException : InvalidOperationException
{
    public ImageGenerationProviderFailureKind FailureKind { get; }

    public ImageGenerationProviderException(
        ImageGenerationProviderFailureKind failureKind,
        string message)
        : base(message)
    {
        FailureKind = failureKind;
    }
}
