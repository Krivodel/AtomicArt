using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using CommunityToolkit.Mvvm.Messaging;
using Lang.Avalonia;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Localization;

public sealed class LocalizationService :
    ILocalizationService,
    ILocalizationTextProvider,
    IDisposable
{
    public IReadOnlyList<LocalizationOption> AvailableLocalizations
    {
        get
        {
            lock (_syncRoot)
            {
                return _catalog.Values
                    .OrderBy(snapshot => snapshot.SortOrder)
                    .ThenBy(snapshot => snapshot.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(snapshot => snapshot.ToOption())
                    .ToList();
            }
        }
    }
    public LocalizationOption? CurrentLocalization
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentSnapshot?.ToOption();
            }
        }
    }
    public CultureInfo CurrentCulture
    {
        get
        {
            lock (_syncRoot)
            {
                return _textResolver.Active.Culture;
            }
        }
    }

    private const string TemplateWriteFailureMessage =
        "Localization template path is not a trusted Atomic Art data path.";

    private static readonly JsonSerializerOptions TemplateJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly TrustedFileStreamFactory _trustedFileStreamFactory;
    private readonly ILogger<LocalizationService> _logger;
    private readonly IMessenger _messenger;
    private readonly CultureInfo _systemCulture;
    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyDictionary<string, LocalizationSnapshot> _catalog =
        new Dictionary<string, LocalizationSnapshot>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<LocalizationSnapshot>? _builtInSnapshots;
    private LocalizationSnapshot? _englishSnapshot;
    private LocalizationSnapshot? _currentSnapshot;
    private LocalizationTextResolver _textResolver;
    private JsonElement? _englishStringsElement;
    private bool _isDisposed;

    public LocalizationService(
        IAtomicArtDataPathProvider pathProvider,
        TrustedFileStreamFactory trustedFileStreamFactory,
        ILogger<LocalizationService> logger,
        IMessenger messenger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _trustedFileStreamFactory = trustedFileStreamFactory
            ?? throw new ArgumentNullException(nameof(trustedFileStreamFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _systemCulture = CultureInfo.CurrentUICulture;

        EnsureBuiltInSnapshotsLoaded();
        Dictionary<string, LocalizationSnapshot> catalog = CreateBuiltInCatalog();
        PublishCatalog(catalog);
        _textResolver = RegisterWithLang(ResolveSystemDefault(catalog.Values));
    }

    public async Task RefreshAvailableLocalizationsAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            EnsureBuiltInSnapshotsLoaded();
            Dictionary<string, LocalizationSnapshot> catalog =
                CreateBuiltInCatalog();
            string localizationDirectory = _pathProvider.LocalizationsDirectory;

            if (!TryEnsureLocalizationDirectory(localizationDirectory))
            {
                PublishCatalog(catalog);
                return;
            }

            await TryUpdateTemplateAsync(localizationDirectory, ct).ConfigureAwait(false);
            IReadOnlyList<string> localizationFiles =
                GetUserLocalizationFiles(localizationDirectory);

            foreach (string localizationFile in localizationFiles)
            {
                LocalizationSnapshot? snapshot = await TryLoadUserSnapshotAsync(
                        localizationFile,
                        localizationDirectory,
                        ct)
                    .ConfigureAwait(false);

                if (snapshot is null)
                {
                    continue;
                }

                if (!catalog.TryAdd(snapshot.Id, snapshot))
                {
                    _logger.LogWarning(
                        "User localization {LocalizationFile} conflicts with localization identifier {LocalizationId} and was skipped.",
                        localizationFile,
                        snapshot.Id);
                }
            }

            PublishCatalog(catalog);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void ReconcileCurrentOrSystemDefault()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        LocalizationSnapshot snapshot;

        lock (_syncRoot)
        {
            if (_currentSnapshot is null)
            {
                snapshot = ResolveSystemDefault(_catalog.Values);
            }
            else if (_catalog.TryGetValue(
                         _currentSnapshot.Id,
                         out LocalizationSnapshot? refreshedSnapshot))
            {
                snapshot = refreshedSnapshot;
            }
            else
            {
                snapshot = GetRequiredBuiltIn(
                    _catalog.Values,
                    LocalizationConstants.EnglishId);
            }
        }

        Activate(snapshot);
    }

    public void Select(string localizationId)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationId);
        LocalizationSnapshot snapshot;

        lock (_syncRoot)
        {
            if (!_catalog.TryGetValue(localizationId, out LocalizationSnapshot? selected))
            {
                throw new InvalidOperationException(
                    $"Localization '{localizationId}' is unavailable.");
            }

            snapshot = selected;
        }

        Activate(snapshot);
    }

    public void SelectSavedOrEnglishFallback(string localizationId)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationId);
        LocalizationSnapshot snapshot;

        lock (_syncRoot)
        {
            snapshot = _catalog.TryGetValue(
                localizationId,
                out LocalizationSnapshot? selected)
                ? selected
                : GetRequiredBuiltIn(
                    _catalog.Values,
                    LocalizationConstants.EnglishId);
        }

        Activate(snapshot);
    }

    public string Get(string key)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_syncRoot)
        {
            return _textResolver.Get(key);
        }
    }

    public string Format(string key, params object?[] arguments)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(arguments);
        string format;
        CultureInfo culture;

        lock (_syncRoot)
        {
            format = _textResolver.Get(key);
            culture = _textResolver.Active.Culture;
        }

        return string.Format(culture, format, arguments);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshLock.Dispose();
    }

    private static IReadOnlyDictionary<string, string> FilterKnownStrings(
        LocalizationDocument document,
        IReadOnlySet<string> knownKeys,
        string localizationFile,
        ILogger logger)
    {
        Dictionary<string, string> strings = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in document.Strings)
        {
            if (knownKeys.Contains(item.Key))
            {
                strings[item.Key] = item.Value;
                continue;
            }

            logger.LogWarning(
                "User localization {LocalizationFile} contains unknown key {LocalizationKey}; the key was ignored.",
                localizationFile,
                item.Key);
        }

        return strings;
    }

    private static byte[] SerializeTemplate(JsonElement englishStrings)
    {
        LocalizationTemplateDocument template = new()
        {
            SchemaVersion = LocalizationConstants.SchemaVersion,
            Culture = "en-US",
            Strings = englishStrings
        };

        return JsonSerializer.SerializeToUtf8Bytes(template, TemplateJsonOptions);
    }

    private static LocalizationSnapshot GetRequiredBuiltIn(
        IEnumerable<LocalizationSnapshot> snapshots,
        string localizationId)
    {
        return snapshots.Single(snapshot =>
            snapshot.IsBuiltIn
            && string.Equals(
                snapshot.Id,
                localizationId,
                StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureBuiltInSnapshotsLoaded()
    {
        if (_builtInSnapshots is not null)
        {
            return;
        }

        BuiltInLocalizationCatalog builtIns = BuiltInLocalizationCatalog.Current;
        _englishSnapshot = builtIns.English;
        _englishStringsElement = builtIns.EnglishStrings;
        _builtInSnapshots = new List<LocalizationSnapshot>
        {
            builtIns.Russian,
            builtIns.English
        };
    }

    private Dictionary<string, LocalizationSnapshot> CreateBuiltInCatalog()
    {
        IReadOnlyList<LocalizationSnapshot> builtIns = _builtInSnapshots
            ?? throw new InvalidOperationException(
                "Built-in localization snapshots are unavailable.");

        return builtIns.ToDictionary(
            snapshot => snapshot.Id,
            snapshot => snapshot,
            StringComparer.OrdinalIgnoreCase);
    }

    private bool TryEnsureLocalizationDirectory(string localizationDirectory)
    {
        try
        {
            _pathProvider.EnsureDirectoryExists(localizationDirectory);
            return true;
        }
        catch (IOException exception)
        {
            LogLocalizationDirectoryFailure(exception, localizationDirectory);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogLocalizationDirectoryFailure(exception, localizationDirectory);
        }
        catch (NotSupportedException exception)
        {
            LogLocalizationDirectoryFailure(exception, localizationDirectory);
        }
        catch (ArgumentException exception)
        {
            LogLocalizationDirectoryFailure(exception, localizationDirectory);
        }

        return false;
    }

    private async Task TryUpdateTemplateAsync(
        string localizationDirectory,
        CancellationToken ct)
    {
        try
        {
            await UpdateTemplateAsync(localizationDirectory, ct).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            LogTemplateUpdateFailure(exception, localizationDirectory);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogTemplateUpdateFailure(exception, localizationDirectory);
        }
        catch (NotSupportedException exception)
        {
            LogTemplateUpdateFailure(exception, localizationDirectory);
        }
        catch (InvalidOperationException exception)
        {
            LogTemplateUpdateFailure(exception, localizationDirectory);
        }
    }

    private async Task UpdateTemplateAsync(
        string localizationDirectory,
        CancellationToken ct)
    {
        JsonElement englishStrings = _englishStringsElement
            ?? throw new InvalidOperationException(
                "English localization strings are unavailable.");
        byte[] content = SerializeTemplate(englishStrings);
        string templatePath = Path.Combine(
            localizationDirectory,
            LocalizationConstants.TemplateFileName);

        if (await HasSameContentAsync(
                templatePath,
                localizationDirectory,
                content,
                ct)
            .ConfigureAwait(false))
        {
            return;
        }

        string temporaryPath = AtomicFileWriteTempPath.CreateHidden(
            localizationDirectory,
            LocalizationConstants.TemplateFileName);

        try
        {
            await using (FileStream stream =
                _trustedFileStreamFactory.CreateNewFileForWrite(
                    localizationDirectory,
                    temporaryPath,
                    TemplateWriteFailureMessage))
            {
                await stream.WriteAsync(content, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }

            TrustedPathGuard.ReplaceTrustedFile(
                localizationDirectory,
                temporaryPath,
                templatePath,
                TemplateWriteFailureMessage);
            _logger.LogInformation(
                "Localization template {TemplatePath} was updated.",
                templatePath);
        }
        finally
        {
            FileDeletion.DeleteIfExists(temporaryPath);
        }
    }

    private async Task<bool> HasSameContentAsync(
        string templatePath,
        string localizationDirectory,
        ReadOnlyMemory<byte> expectedContent,
        CancellationToken ct)
    {
        string[] trustedDirectories = [localizationDirectory];
        bool opened = _trustedFileStreamFactory.TryOpenExistingFileForRead(
            templatePath,
            trustedDirectories,
            _pathProvider.RootDirectory,
            TemplateWriteFailureMessage,
            out FileStream? stream,
            out _);

        if (!opened || stream is null)
        {
            return false;
        }

        await using (stream)
        {
            if (stream.Length != expectedContent.Length)
            {
                return false;
            }

            byte[] existingContent = new byte[expectedContent.Length];
            await stream.ReadExactlyAsync(existingContent, ct).ConfigureAwait(false);

            return existingContent.AsSpan().SequenceEqual(expectedContent.Span);
        }
    }

    private IReadOnlyList<string> GetUserLocalizationFiles(string localizationDirectory)
    {
        try
        {
            return Directory
                .EnumerateFiles(
                    localizationDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(
                    Path.GetExtension(path),
                    LocalizationConstants.JsonExtension,
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    LocalizationConstants.TemplateFileName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (IOException exception)
        {
            LogLocalizationDirectoryFailure(exception, localizationDirectory);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogLocalizationDirectoryFailure(exception, localizationDirectory);
        }
        catch (NotSupportedException exception)
        {
            LogLocalizationDirectoryFailure(exception, localizationDirectory);
        }

        return new List<string>();
    }

    private async Task<LocalizationSnapshot?> TryLoadUserSnapshotAsync(
        string localizationFile,
        string localizationDirectory,
        CancellationToken ct)
    {
        string localizationId = Path.GetFileNameWithoutExtension(localizationFile);

        if (string.IsNullOrWhiteSpace(localizationId))
        {
            _logger.LogWarning(
                "User localization {LocalizationFile} has an empty localization identifier and was skipped.",
                localizationFile);
            return null;
        }

        if (string.Equals(
                localizationId,
                LocalizationConstants.EnglishId,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                localizationId,
                LocalizationConstants.RussianId,
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "User localization {LocalizationFile} conflicts with a built-in localization name and was skipped.",
                localizationFile);
            return null;
        }

        try
        {
            string[] trustedDirectories = [localizationDirectory];
            bool opened = _trustedFileStreamFactory.TryOpenExistingFileForRead(
                localizationFile,
                trustedDirectories,
                _pathProvider.RootDirectory,
                "User localization path is not a trusted Atomic Art data path.",
                out FileStream? stream,
                out string? trustedPath);

            if (!opened || stream is null || trustedPath is null)
            {
                _logger.LogWarning(
                    "User localization {LocalizationFile} is not a trusted file and was skipped.",
                    localizationFile);
                return null;
            }

            await using (stream)
            {
                if (stream.Length > LocalizationConstants.MaximumFileBytes)
                {
                    _logger.LogWarning(
                        "User localization {LocalizationFile} exceeds the {MaximumFileBytes} byte limit and was skipped.",
                        trustedPath,
                        LocalizationConstants.MaximumFileBytes);
                    return null;
                }

                LocalizationDocument document = await LocalizationDocumentParser
                    .ParseAsync(stream, trustedPath, ct)
                    .ConfigureAwait(false);
                LocalizationSnapshot english = _englishSnapshot
                    ?? throw new InvalidOperationException(
                        "English localization snapshot is unavailable.");
                IReadOnlySet<string> knownKeys = english.Strings.Keys
                    .ToHashSet(StringComparer.Ordinal);
                IReadOnlyDictionary<string, string> strings = FilterKnownStrings(
                    document,
                    knownKeys,
                    trustedPath,
                    _logger);

                return new LocalizationSnapshot(
                    localizationId,
                    document.Culture,
                    strings,
                    IsBuiltIn: false,
                    SortOrder: 100);
            }
        }
        catch (JsonException exception)
        {
            LogRejectedLocalization(exception, localizationFile);
        }
        catch (InvalidDataException exception)
        {
            LogRejectedLocalization(exception, localizationFile);
        }
        catch (IOException exception)
        {
            LogRejectedLocalization(exception, localizationFile);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogRejectedLocalization(exception, localizationFile);
        }
        catch (NotSupportedException exception)
        {
            LogRejectedLocalization(exception, localizationFile);
        }

        return null;
    }

    private void PublishCatalog(
        IReadOnlyDictionary<string, LocalizationSnapshot> catalog)
    {
        lock (_syncRoot)
        {
            _catalog = new Dictionary<string, LocalizationSnapshot>(
                catalog,
                StringComparer.OrdinalIgnoreCase);
        }

        _logger.LogInformation(
            "Localization catalog refreshed with {LocalizationCount} available variants.",
            catalog.Count);
    }

    private LocalizationSnapshot ResolveSystemDefault(
        IEnumerable<LocalizationSnapshot> snapshots)
    {
        IReadOnlyList<LocalizationSnapshot> available = snapshots
            .OrderBy(snapshot => snapshot.SortOrder)
            .ThenBy(snapshot => snapshot.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        string systemLanguage = _systemCulture.TwoLetterISOLanguageName;

        if (string.Equals(
                systemLanguage,
                LocalizationConstants.RussianLanguageCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return GetRequiredBuiltIn(available, LocalizationConstants.RussianId);
        }

        if (string.Equals(
                systemLanguage,
                LocalizationConstants.EnglishLanguageCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return GetRequiredBuiltIn(available, LocalizationConstants.EnglishId);
        }

        LocalizationSnapshot? exactUserMatch = available.FirstOrDefault(snapshot =>
            !snapshot.IsBuiltIn
            && string.Equals(
                snapshot.Culture.Name,
                _systemCulture.Name,
                StringComparison.OrdinalIgnoreCase));

        if (exactUserMatch is not null)
        {
            return exactUserMatch;
        }

        LocalizationSnapshot? languageUserMatch = available.FirstOrDefault(snapshot =>
            !snapshot.IsBuiltIn
            && string.Equals(
                snapshot.Culture.TwoLetterISOLanguageName,
                systemLanguage,
                StringComparison.OrdinalIgnoreCase));

        return languageUserMatch
            ?? GetRequiredBuiltIn(available, LocalizationConstants.EnglishId);
    }

    private void Activate(LocalizationSnapshot snapshot)
    {
        LocalizationTextResolver textResolver = RegisterWithLang(snapshot);

        lock (_syncRoot)
        {
            _currentSnapshot = snapshot;
            _textResolver = textResolver;
        }

        _logger.LogInformation(
            "Localization {LocalizationId} with culture {CultureName} was activated.",
            snapshot.Id,
            snapshot.Culture.Name);
        _messenger.Send(new LocalizationChangedMessage());
    }

    private LocalizationTextResolver RegisterWithLang(LocalizationSnapshot snapshot)
    {
        LocalizationSnapshot english = _englishSnapshot
            ?? throw new InvalidOperationException(
                "English localization snapshot is unavailable.");
        LocalizationTextResolver textResolver = new(snapshot, english);
        LocalizationLangPlugin plugin = new(textResolver);

        if (!I18nManager.Instance.Register(
            plugin,
            snapshot.Culture,
            out string? error))
        {
            throw new InvalidOperationException(
                $"Localization '{snapshot.Id}' could not be activated: {error}");
        }

        return textResolver;
    }

    private void LogLocalizationDirectoryFailure(
        Exception exception,
        string localizationDirectory)
    {
        _logger.LogWarning(
            exception,
            "Localization directory {LocalizationDirectory} is unavailable; only built-in localizations will be used.",
            localizationDirectory);
    }

    private void LogTemplateUpdateFailure(
        Exception exception,
        string localizationDirectory)
    {
        _logger.LogWarning(
            exception,
            "Localization template in directory {LocalizationDirectory} could not be updated.",
            localizationDirectory);
    }

    private void LogRejectedLocalization(
        Exception exception,
        string localizationFile)
    {
        _logger.LogWarning(
            exception,
            "User localization {LocalizationFile} is invalid and was skipped.",
            localizationFile);
    }
}
