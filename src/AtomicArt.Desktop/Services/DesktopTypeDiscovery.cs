namespace AtomicArt.Desktop.Services;

internal static class DesktopTypeDiscovery
{
    private static readonly IReadOnlyList<Type> ConcreteTypes = typeof(DesktopTypeDiscovery)
        .Assembly
        .GetTypes()
        .Where(type => type is { IsAbstract: false, IsInterface: false })
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToList();

    internal static IReadOnlyList<Type> FindPublicImplementations(params Type[] markerTypes)
    {
        return FindImplementations(markerTypes, type => type.IsPublic);
    }

    internal static IReadOnlyList<Type> FindAllImplementations(params Type[] markerTypes)
    {
        return FindImplementations(markerTypes, _ => true);
    }

    private static IReadOnlyList<Type> FindImplementations(
        IReadOnlyCollection<Type> markerTypes,
        Func<Type, bool> visibilityPredicate)
    {
        ArgumentNullException.ThrowIfNull(markerTypes);
        ArgumentNullException.ThrowIfNull(visibilityPredicate);

        if (markerTypes.Count == 0)
        {
            throw new ArgumentException(
                "At least one marker type is required.",
                nameof(markerTypes));
        }

        foreach (Type markerType in markerTypes)
        {
            ArgumentNullException.ThrowIfNull(markerType);
        }

        return ConcreteTypes
            .Where(visibilityPredicate)
            .Where(type => markerTypes.Any(markerType => markerType.IsAssignableFrom(type)))
            .ToList();
    }
}
