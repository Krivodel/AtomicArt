using Avalonia.Controls;

namespace AtomicArt.Desktop.Views.Gallery;

internal sealed class GenerationPreviewSessionFactory
    : IGenerationPreviewSessionFactory
{
    private readonly Func<TopLevel, IGenerationPreviewSession> _createSession;

    public GenerationPreviewSessionFactory(
        Func<TopLevel, IGenerationPreviewSession> createSession)
    {
        _createSession = createSession
            ?? throw new ArgumentNullException(nameof(createSession));
    }

    public IGenerationPreviewSession Create(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        return _createSession(topLevel);
    }
}
