namespace AtomicArt.Desktop.Services.State;

public sealed class StateSectionRegistry : IStateSectionRegistry
{
    private readonly IReadOnlyCollection<IStateSection> _sections;
    private readonly Dictionary<string, IStateSection> _sectionsByKey;
    private readonly Dictionary<Type, IStateSection> _sectionsByPayloadType;

    public IReadOnlyCollection<IStateSection> Sections => _sections;

    public StateSectionRegistry(IEnumerable<IStateSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        List<IStateSection> sectionList = sections.ToList();
        _sectionsByKey = new Dictionary<string, IStateSection>(StringComparer.Ordinal);
        _sectionsByPayloadType = new Dictionary<Type, IStateSection>();
        HashSet<string> fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (IStateSection section in sectionList)
        {
            ValidateSection(section);

            if (!_sectionsByKey.TryAdd(section.Key, section))
            {
                throw new InvalidOperationException(
                    $"State section key '{section.Key}' is registered more than once.");
            }

            if (!fileNames.Add(section.FileName))
            {
                throw new InvalidOperationException(
                    $"State section file name '{section.FileName}' is registered more than once.");
            }

            if (!_sectionsByPayloadType.TryAdd(section.PayloadType, section))
            {
                throw new InvalidOperationException(
                    $"State section payload type '{section.PayloadType.FullName}' is registered more than once.");
            }
        }

        _sections = sectionList.AsReadOnly();
    }

    public IStateSection GetRequired(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_sectionsByKey.TryGetValue(key, out IStateSection? section))
        {
            return section;
        }

        throw new KeyNotFoundException($"State section '{key}' is not registered.");
    }

    public IStateSection GetRequired(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        if (_sectionsByPayloadType.TryGetValue(payloadType, out IStateSection? section))
        {
            return section;
        }

        throw new KeyNotFoundException(
            $"State section for payload type '{payloadType.FullName}' is not registered.");
    }

    public IStateSection GetRequired<TState>()
    {
        return GetRequired(typeof(TState));
    }

    public bool TryGet(string key, out IStateSection? section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _sectionsByKey.TryGetValue(key, out section);
    }

    public bool TryGet(Type payloadType, out IStateSection? section)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        return _sectionsByPayloadType.TryGetValue(payloadType, out section);
    }

    public bool TryGet<TState>(out IStateSection? section)
    {
        return TryGet(typeof(TState), out section);
    }

    private static void ValidateSection(IStateSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        ArgumentException.ThrowIfNullOrWhiteSpace(section.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(section.FileName);
        ArgumentNullException.ThrowIfNull(section.PayloadType);

        if (section.SchemaVersion <= 0)
        {
            throw new InvalidOperationException(
                $"State section '{section.Key}' must have a positive schema version.");
        }
    }
}
