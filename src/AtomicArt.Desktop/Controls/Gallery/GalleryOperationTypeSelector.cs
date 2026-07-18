using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal static class GalleryOperationTypeSelector
{
    internal static bool Matches(GalleryOperation operation, Type operationType)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(operationType);

        return operation.GetType() == operationType;
    }

    internal static bool Contains(
        IReadOnlyList<GalleryOperation> operations,
        Type operationType)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(operationType);

        return operations.Any(operation => Matches(operation, operationType));
    }

    internal static bool ContainsOnly(
        IReadOnlyList<GalleryOperation> operations,
        Type operationType)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(operationType);

        return (operations.Count > 0)
            && operations.All(operation => Matches(operation, operationType));
    }

    internal static IReadOnlyList<GalleryOperation> Select(
        IReadOnlyList<GalleryOperation> operations,
        Type operationType)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(operationType);

        return operations
            .Where(operation => Matches(operation, operationType))
            .ToList();
    }

    internal static GalleryOperation? FindLast(
        IReadOnlyList<GalleryOperation> operations,
        Type operationType)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(operationType);

        for (int i = operations.Count - 1; i >= 0; i--)
        {
            if (Matches(operations[i], operationType))
            {
                return operations[i];
            }
        }

        return null;
    }
}
