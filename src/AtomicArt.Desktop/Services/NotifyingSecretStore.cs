using CommunityToolkit.Mvvm.Messaging;

namespace AtomicArt.Desktop.Services;

public sealed class NotifyingSecretStore : ISecretStore
{
    private readonly ProtectedDesktopSecretStore _store;
    private readonly IMessenger _messenger;

    public NotifyingSecretStore(ProtectedDesktopSecretStore store, IMessenger messenger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken ct)
    {
        return _store.GetSecretAsync(key, ct);
    }

    public async Task SetSecretAsync(string key, string value, CancellationToken ct)
    {
        await _store.SetSecretAsync(key, value, ct).ConfigureAwait(false);
        _messenger.Send(new SecretChangedMessage(key));
    }
}
