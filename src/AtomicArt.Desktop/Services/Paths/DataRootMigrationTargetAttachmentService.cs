namespace AtomicArt.Desktop.Services.Paths;

public sealed class DataRootMigrationTargetAttachmentService :
    IDataRootMigrationTargetAttachmentService
{
    private readonly object _syncRoot = new();
    private IDataRootMigrationTarget? _target;

    public void Attach(IDataRootMigrationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        lock (_syncRoot)
        {
            _target = target;
        }
    }

    internal IDataRootMigrationTarget GetTarget()
    {
        lock (_syncRoot)
        {
            return _target
                ?? throw new InvalidOperationException(
                    "The data root migration target has not been attached.");
        }
    }
}
