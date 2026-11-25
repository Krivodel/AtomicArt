using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class LocalCodexSettingViewModel : SettingItemViewModel, IDisposable
{
    public bool IsConnectionCheckSuccessful => ConnectionCheckSucceeded == true;
    public bool IsConnectionCheckFailed => ConnectionCheckSucceeded == false;
    public string? ConnectionCheckDescription => ConnectionCheckSucceeded switch
    {
        true => TextProvider.Get(SettingsLocalizationKeys.LocalCodex.Connected),
        false => TextProvider.Get(SettingsLocalizationKeys.LocalCodex.Unavailable),
        null => null
    };

    protected override IRelayCommand OperationCommand => CommitCommand;

    private readonly LocalCodexSettingDefinition _definition;
    private readonly ISettingsStateService _state;
    private readonly LocalCodexSettings _settings;
    private readonly LocalCodexAppServer _appServer;
    private int _settingsRevision;
    private bool _isSynchronizingOptions;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private bool _enabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private string _folder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private string _systemPrompt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnectionCheckSuccessful))]
    [NotifyPropertyChangedFor(nameof(IsConnectionCheckFailed))]
    [NotifyPropertyChangedFor(nameof(ConnectionCheckDescription))]
    private bool? _connectionCheckSucceeded;

    public LocalCodexSettingViewModel(
        LocalCodexSettingDefinition definition,
        ISettingsStateService state,
        LocalCodexSettings settings,
        LocalCodexAppServer appServer,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, errorHandler, textProvider)
    {
        _definition = definition;
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _appServer = appServer ?? throw new ArgumentNullException(nameof(appServer));
        LocalCodexOptions current = settings.Current;
        _enabled = current.Enabled;
        _folder = current.DisplayFolder;
        _systemPrompt = current.SystemPrompt;
        _settings.OptionsChanged += OnOptionsChanged;
        _settings.ConnectionChanged += OnConnectionChanged;
    }

    public void Dispose()
    {
        _settings.OptionsChanged -= OnOptionsChanged;
        _settings.ConnectionChanged -= OnConnectionChanged;
    }

    public override void RefreshLocalization()
    {
        base.RefreshLocalization();
        OnPropertyChanged(nameof(ConnectionCheckDescription));
    }

    protected override void NotifyOperationCanExecuteChanged()
    {
        base.NotifyOperationCanExecuteChanged();
        CheckConnectionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task CommitAsync(CancellationToken ct)
    {
        LocalCodexOptions options = GetOptions();
        if (!IsValidFolder(options.Folder))
        {
            SynchronizeEnabled(_settings.Current.Enabled);
            return;
        }

        await RunOperationAsync(async () =>
        {
            string value = JsonSerializer.Serialize(options);
            await _state.SaveValueAsync(_definition, value, ct);
            _state.ApplyValue(_definition, value);
        }, ct, nameof(CommitAsync));
    }

    [RelayCommand(CanExecute = nameof(CanCheckConnection))]
    private async Task CheckConnectionAsync(CancellationToken ct)
    {
        ConnectionCheckSucceeded = null;
        LocalCodexOptions options = GetOptions();
        if (!IsValidFolder(options.Folder))
        {
            ConnectionCheckSucceeded = false;
            return;
        }

        int revision = _settingsRevision;
        await RunOperationAsync(async () =>
        {
            bool succeeded;
            try
            {
                await _appServer.CheckConnectionAsync(options, ct);
                succeeded = true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                ErrorHandler.Log(exception, nameof(CheckConnectionAsync));
                succeeded = false;
            }

            if (revision == _settingsRevision)
            {
                ConnectionCheckSucceeded = succeeded;
            }
        }, ct, nameof(CheckConnectionAsync));
    }

    private bool CanCommit()
    {
        return !IsLoading && GetOptions() != _settings.Current;
    }

    private bool CanCheckConnection()
    {
        return !IsLoading;
    }

    private LocalCodexOptions GetOptions()
    {
        LocalCodexOptions current = _settings.Current;
        string folder = NormalizePath(Folder);
        LocalCodexOptions options = string.Equals(folder, current.DisplayFolder, StringComparison.Ordinal)
            ? current
            : new LocalCodexOptions(Enabled, folder);

        return options with { Enabled = Enabled, SystemPrompt = SystemPrompt };
    }

    private static string NormalizePath(string? value)
    {
        return value?.Trim().Trim('"') ?? string.Empty;
    }

    private static bool IsValidFolder(string path)
    {
        return path.Length == 0 || (Path.IsPathFullyQualified(path) && !path.Any(char.IsControl));
    }

    private void ResetConnectionCheck()
    {
        _settingsRevision++;
        ConnectionCheckSucceeded = null;
    }

    private void OnOptionsChanged(object? sender, EventArgs e)
    {
        LocalCodexOptions current = _settings.Current;
        SynchronizeEnabled(current.Enabled);
        Folder = current.DisplayFolder;
        SystemPrompt = current.SystemPrompt;
        CommitCommand.NotifyCanExecuteChanged();
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        ResetConnectionCheck();
    }

    private void SynchronizeEnabled(bool value)
    {
        _isSynchronizingOptions = true;
        try
        {
            Enabled = value;
        }
        finally
        {
            _isSynchronizingOptions = false;
        }
    }

    partial void OnEnabledChanged(bool value)
    {
        ResetConnectionCheck();
        if (!_isSynchronizingOptions && CommitCommand.CanExecute(null))
        {
            CommitCommand.Execute(null);
        }
    }

    partial void OnFolderChanged(string value)
    {
        ResetConnectionCheck();
    }
}
