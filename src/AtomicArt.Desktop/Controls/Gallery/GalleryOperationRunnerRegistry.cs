namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryOperationRunnerRegistry : IGalleryOperationRunnerRegistry
{
    public IReadOnlyList<IGalleryOperationRunner> Runners => _runners;

    private readonly Dictionary<Type, IGalleryOperationRunner> _runnersByType;
    private readonly IReadOnlyList<IGalleryOperationRunner> _runners;

    public GalleryOperationRunnerRegistry(IEnumerable<IGalleryOperationRunner> runners)
    {
        ArgumentNullException.ThrowIfNull(runners);

        _runners = runners.ToList();
        _runnersByType = _runners.ToDictionary(
            runner => runner.OperationType,
            runner => runner,
            EqualityComparer<Type>.Default);
    }

    public IGalleryOperationRunner GetRunner(Type operationType)
    {
        ArgumentNullException.ThrowIfNull(operationType);

        if (_runnersByType.TryGetValue(operationType, out IGalleryOperationRunner? runner))
        {
            return runner;
        }

        throw new InvalidOperationException($"Gallery operation runner for '{operationType.Name}' was not registered.");
    }
}
