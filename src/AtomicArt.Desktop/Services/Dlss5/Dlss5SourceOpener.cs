using AtomicArt.Desktop.ViewModels.Dlss5;

namespace AtomicArt.Desktop.Services.Dlss5;

public sealed class Dlss5SourceOpener : IDlss5SourceOpener
{
    private readonly Func<Dlss5SessionViewModel> _sessionFactory;

    public Dlss5SourceOpener(Func<Dlss5SessionViewModel> sessionFactory)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    public Task<bool> OpenFromImagePathAsync(string imagePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        Dlss5SessionViewModel session = _sessionFactory();
        return session.OpenFromImagePathAsync(imagePath, ct);
    }
}
