namespace AtomicArt.Desktop.Services.State;

public interface IStateSectionRegistry
{
    IReadOnlyCollection<IStateSection> Sections { get; }

    IStateSection GetRequired(string key);

    IStateSection GetRequired(Type payloadType);

    IStateSection GetRequired<TState>();

    bool TryGet(string key, out IStateSection? section);

    bool TryGet(Type payloadType, out IStateSection? section);

    bool TryGet<TState>(out IStateSection? section);
}
