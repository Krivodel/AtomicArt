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
        GoogleInteractionsNoImageDiagnostics noImageDiagnostics)
        : base(failureKind, message)
    {
        NoImageDiagnostics = noImageDiagnostics
            ?? throw new ArgumentNullException(nameof(noImageDiagnostics));
    }
}
