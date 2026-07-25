using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class DataRootSettingViewModel : SettingItemViewModel
{
    protected override IRelayCommand OperationCommand => ChangeDirectoryCommand;

    private readonly DataRootSettingDefinition _definition;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IAtomicArtDataRootMigrationService _migrationService;
    private readonly IAtomicArtDataPathProvider _pathProvider;

    [ObservableProperty]
    private string _value;
    [ObservableProperty]
    private double _progressPercentage;
    [ObservableProperty]
    private bool _isProgressIndeterminate;
    [ObservableProperty]
    private string? _progressText;

    public DataRootSettingViewModel(
        DataRootSettingDefinition definition,
        IFolderPickerService folderPickerService,
        IAtomicArtDataRootMigrationService migrationService,
        IAtomicArtDataPathProvider pathProvider,
        IViewModelErrorHandler errorHandler)
        : base(definition, errorHandler)
    {
        _definition = definition;
        _folderPickerService = folderPickerService
            ?? throw new ArgumentNullException(nameof(folderPickerService));
        _migrationService = migrationService
            ?? throw new ArgumentNullException(nameof(migrationService));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _value = pathProvider.RootDirectory;
    }

    [RelayCommand(CanExecute = nameof(CanChangeDirectory))]
    private async Task ChangeDirectoryAsync(CancellationToken ct)
    {
        string? selectedDirectory = await _folderPickerService.PickFolderAsync(ct);

        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            return;
        }

        Progress<DataRootMigrationProgress> progress = new(ApplyProgress);
        await RunOperationAsync(
            async () =>
            {
                try
                {
                    await _migrationService.MigrateAsync(selectedDirectory, progress, ct);
                }
                finally
                {
                    Value = _pathProvider.RootDirectory;
                }
            },
            ct,
            nameof(ChangeDirectoryAsync));
    }

    private bool CanChangeDirectory()
    {
        return !IsLoading;
    }

    private void ApplyProgress(DataRootMigrationProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        ProgressPercentage = progress.Percentage;
        IsProgressIndeterminate =
            progress.Stage == DataRootMigrationProgressStage.Preparing;
        ProgressText = progress.Stage switch
        {
            DataRootMigrationProgressStage.Preparing => UiStrings.SettingsDataRootPreparing,
            DataRootMigrationProgressStage.Copying => UiStrings.SettingsDataRootCopying,
            DataRootMigrationProgressStage.Verifying => UiStrings.SettingsDataRootVerifying,
            DataRootMigrationProgressStage.Switching => UiStrings.SettingsDataRootSwitching,
            DataRootMigrationProgressStage.Cleaning => UiStrings.SettingsDataRootCleaning,
            DataRootMigrationProgressStage.Completed => UiStrings.SettingsDataRootCompleted,
            _ => UiStrings.SettingsDataRootPreparing
        };
    }
}
