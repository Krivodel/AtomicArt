using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleInteractionsException : ImageGenerationProviderException
{
    public GoogleInteractionsNoImageDiagnostics? NoImageDiagnostics { get; }

    public GoogleInteractionsException(
        ImageGenerationProviderFailureKind failureKind,
        string message)
        : base(failureKind, message)
    {
    }

    public GoogleInteractionsException(
        ImageGenerationProviderFailureKind failureKind,
        string message,
        bool retryable)
        : base(failureKind, message, retryable)
    {
    }

    public GoogleInteractionsException(
        ImageGenerationProviderFailureKind failureKind,
        string message,
        bool retryable,
        Exception innerException)
        : base(failureKind, message, retryable, innerException)
    {
    }

    public GoogleInteractionsException(
        ImageGenerationProviderFailureKind failureKind,
        string message,
        GoogleInteractionsNoImageDiagnostics noImageDiagnostics)
        : base(failureKind, message)
    {
        NoImageDiagnostics = noImageDiagnostics
            ?? throw new ArgumentNullException(nameof(noImageDiagnostics));
    }
}
