using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.State;

public sealed class AppStateStore : IAppStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly string TrustedPathFailureMessage =
        TrustedPathGuard.CreateFailureMessage(
            "State path",
            AtomicArtPathNames.StateDirectory);

    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly ILogger<AppStateStore> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _writeLocks;

    public AppStateStore(
        IAtomicArtDataPathProvider pathProvider,
        ILogger<AppStateStore> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _writeLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
    }

    public async Task<TState> LoadAsync<TState>(IStateSection section, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(section);
        EnsureRequestedTypeMatchesSection<TState>(section);

        string path = GetStatePath(section);

        try
        {
            string[] trustedDirectories = [Path.GetFullPath(_pathProvider.StateDirectory)];

            if (!TrustedPathGuard.TryOpenTrustedExistingFileForRead(
                path,
                trustedDirectories,
                _pathProvider.StateDirectory,
                TrustedPathFailureMessage,
                out FileStream? stream,
                out string? _))
            {
                _logger.LogDebug(
                    "State section {SectionKey} has no trusted saved file; default state will be used.",
                    section.Key);
                return GetDefaultPayload<TState>(section);
            }

            if (stream is null)
            {
                _logger.LogDebug(
                    "State section {SectionKey} file was unavailable; default state will be used.",
                    section.Key);
                return GetDefaultPayload<TState>(section);
            }

            await using (stream.ConfigureAwait(false))
            {
                StateEnvelope<JsonElement>? envelope =
                    await JsonSerializer.DeserializeAsync<StateEnvelope<JsonElement>>(
                        stream,
                        JsonOptions,
                        ct).ConfigureAwait(false);

                if (envelope is null)
                {
                    _logger.LogWarning(
                        "State section {SectionKey} has an empty JSON envelope.",
                        section.Key);

                    return GetDefaultPayload<TState>(section);
                }

                if (envelope.SchemaVersion > section.SchemaVersion)
                {
                    _logger.LogWarning(
                        "State section {SectionKey} has unsupported schema version {SchemaVersion}.",
                        section.Key,
                        envelope.SchemaVersion);

                    return GetDefaultPayload<TState>(section);
                }

                object payload = section.DeserializePayload(
                    envelope.SchemaVersion,
                    envelope.Payload,
                    JsonOptions);

                if (payload is TState typedPayload)
                {
                    _logger.LogInformation(
                        "State section {SectionKey} loaded with schema version {SchemaVersion}.",
                        section.Key,
                        envelope.SchemaVersion);
                    return typedPayload;
                }

                _logger.LogWarning(
                    "State section {SectionKey} returned payload type {PayloadType} instead of {ExpectedType}.",
                    section.Key,
                    payload.GetType().FullName,
                    typeof(TState).FullName);

                return GetDefaultPayload<TState>(section);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse state section {SectionKey}. Default state will be used.",
                section.Key);

            return GetDefaultPayload<TState>(section);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                ex,
                "State section {SectionKey} is not supported. Default state will be used.",
                section.Key);

            return GetDefaultPayload<TState>(section);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read state section {SectionKey}. Default state will be used.",
                section.Key);

            return GetDefaultPayload<TState>(section);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Access to state section {SectionKey} was denied. Default state will be used.",
                section.Key);

            return GetDefaultPayload<TState>(section);
        }
    }

    public Task SaveAsync<TState>(IStateSection section, TState state, CancellationToken ct)
        where TState : notnull
    {
        return SaveAsync(section, (object)state, ct);
    }

    public async Task SaveAsync(IStateSection section, object state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(state);
        EnsurePayloadMatchesSection(section, state);

        TrustedPathGuard.EnsureTrustedDirectoryExists(
            _pathProvider,
            _pathProvider.StateDirectory,
            TrustedPathFailureMessage);
        string path = GetStatePath(section);
        TrustedPathGuard.EnsureTrustedWriteTarget(
            _pathProvider.StateDirectory,
            path,
            TrustedPathFailureMessage);
        SemaphoreSlim writeLock = _writeLocks.GetOrAdd(path, CreateWriteLock);

        await writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            await SaveLockedAsync(section, state, path, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "State section {SectionKey} saved with schema version {SchemaVersion}.",
                section.Key,
                section.SchemaVersion);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task SaveLockedAsync(
        IStateSection section,
        object state,
        string path,
        CancellationToken ct)
    {
        string tempPath = AtomicFileWriteTempPath.CreateHidden(
            _pathProvider.StateDirectory,
            Path.GetFileName(section.FileName));
        bool stateFileReplaced = false;

        try
        {
            TrustedPathGuard.EnsureTrustedWriteTarget(
                _pathProvider.StateDirectory,
                tempPath,
                TrustedPathFailureMessage);
            StateEnvelope<object> envelope = new StateEnvelope<object>
            {
                SchemaVersion = section.SchemaVersion,
                SavedAtUtc = DateTimeOffset.UtcNow,
                Payload = state
            };

            await using (FileStream stream = TrustedPathGuard.CreateTrustedNewFileForWrite(
                _pathProvider.StateDirectory,
                tempPath,
                TrustedPathFailureMessage))
            {
                await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, ct)
                    .ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }

            TrustedPathGuard.ReplaceTrustedFile(
                _pathProvider.StateDirectory,
                tempPath,
                path,
                TrustedPathFailureMessage);

            stateFileReplaced = true;
        }
        finally
        {
            if (!stateFileReplaced)
            {
                DeleteTempFile(tempPath);
            }
        }
    }

    private string GetStatePath(IStateSection section)
    {
        ValidateSectionFileName(section);

        string stateRoot = Path.GetFullPath(_pathProvider.StateDirectory);
        string statePath = Path.GetFullPath(Path.Combine(stateRoot, section.FileName));

        TrustedPathGuard.EnsureInsideDirectory(
            stateRoot,
            statePath,
            $"State section '{section.Key}' file name must stay inside the state directory.");

        return statePath;
    }

    private static SemaphoreSlim CreateWriteLock(string path)
    {
        return new SemaphoreSlim(1, 1);
    }

    private static void EnsureRequestedTypeMatchesSection<TState>(IStateSection section)
    {
        if (section.PayloadType != typeof(TState))
        {
            throw new InvalidOperationException(
                $"State section '{section.Key}' stores '{section.PayloadType.FullName}', not '{typeof(TState).FullName}'.");
        }
    }

    private static void EnsurePayloadMatchesSection(IStateSection section, object state)
    {
        if (!section.PayloadType.IsInstanceOfType(state))
        {
            throw new InvalidOperationException(
                $"State section '{section.Key}' cannot store payload type '{state.GetType().FullName}'.");
        }
    }

    private static TState GetDefaultPayload<TState>(IStateSection section)
    {
        object payload = section.CreateDefaultPayload();

        if (payload is TState typedPayload)
        {
            return typedPayload;
        }

        throw new InvalidOperationException(
            $"State section '{section.Key}' default payload type does not match '{typeof(TState).FullName}'.");
    }

    private static void ValidateSectionFileName(IStateSection section)
    {
        if (!SafeFileName.IsValid(section.FileName))
        {
            throw new InvalidOperationException(
                $"State section '{section.Key}' file name must be a safe file name.");
        }
    }

    private void DeleteTempFile(string tempPath)
    {
        try
        {
            FileDeletion.DeleteIfExists(tempPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to delete temporary state file.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied while deleting temporary state file.");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Temporary state file path is not supported.");
        }
    }
}
