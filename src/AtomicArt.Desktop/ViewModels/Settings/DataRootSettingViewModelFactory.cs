using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class DataRootSettingViewModelFactory :
    SettingItemViewModelFactory<DataRootSettingDefinition>
{
    private readonly IFolderPickerService _folderPickerService;
    private readonly IAtomicArtDataRootMigrationService _migrationService;
    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;

    public DataRootSettingViewModelFactory(
        IFolderPickerService folderPickerService,
        IAtomicArtDataRootMigrationService migrationService,
        IAtomicArtDataPathProvider pathProvider,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base("Data root setting definition expected.")
    {
        _folderPickerService = folderPickerService
            ?? throw new ArgumentNullException(nameof(folderPickerService));
        _migrationService = migrationService
            ?? throw new ArgumentNullException(nameof(migrationService));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        DataRootSettingDefinition definition)
    {
        return new DataRootSettingViewModel(
            definition,
            _folderPickerService,
            _migrationService,
            _pathProvider,
            _errorHandler,
            _textProvider);
    }
}
