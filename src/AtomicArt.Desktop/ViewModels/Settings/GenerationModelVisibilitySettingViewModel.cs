using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class GenerationModelVisibilitySettingViewModel : SettingItemViewModel, IDisposable
{
    public ReadOnlyObservableCollection<GenerationModelVisibilityOptionViewModel> Models { get; }

    protected override IRelayCommand OperationCommand => SaveCommand;

    private readonly GenerationModelVisibilitySettingDefinition _definition;
    private readonly IGenerationModelCatalogApiClient _catalogApiClient;
    private readonly IImageModelOptionCatalog _modelCatalog;
    private readonly GenerationModelVisibilityService _visibilityService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly ObservableCollection<GenerationModelVisibilityOptionViewModel> _models;
    private bool _isDisposed;
    private bool _isLoaded;
    private bool _isSynchronizing;

    public GenerationModelVisibilitySettingViewModel(
        GenerationModelVisibilitySettingDefinition definition,
        IGenerationModelCatalogApiClient catalogApiClient,
        IImageModelOptionCatalog modelCatalog,
        GenerationModelVisibilityService visibilityService,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, errorHandler, textProvider)
    {
        ArgumentNullException.ThrowIfNull(catalogApiClient);
        ArgumentNullException.ThrowIfNull(modelCatalog);
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _catalogApiClient = catalogApiClient;
        _modelCatalog = modelCatalog;
        _visibilityService = visibilityService ?? throw new ArgumentNullException(nameof(visibilityService));
        _settingsStateService = settingsStateService ?? throw new ArgumentNullException(nameof(settingsStateService));

        _models = [];
        Models = new ReadOnlyObservableCollection<GenerationModelVisibilityOptionViewModel>(_models);

        _visibilityService.VisibleModelsChanged += OnVisibleModelsChanged;
        _modelCatalog.CatalogChanged += OnCatalogChanged;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        foreach (GenerationModelVisibilityOptionViewModel model in Models)
        {
            model.PropertyChanged -= OnModelPropertyChanged;
        }

        _visibilityService.VisibleModelsChanged -= OnVisibleModelsChanged;
        _modelCatalog.CatalogChanged -= OnCatalogChanged;
    }

    protected override void NotifyOperationCanExecuteChanged()
    {
        base.NotifyOperationCanExecuteChanged();
        LoadCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync(CancellationToken ct)
    {
        await RunOperationAsync(
            async () =>
            {
                if (!_modelCatalog.IsLoaded)
                {
                    GenerationModelCatalogDto catalog = await _catalogApiClient.GetCatalogAsync(ct);
                    _modelCatalog.Initialize(catalog);
                }

                PopulateModels();
                _isLoaded = true;
                NotifyOperationCanExecuteChanged();
            },
            ct,
            nameof(LoadAsync));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        await RunOperationAsync(
            async () =>
            {
                _visibilityService.SetVisibleModelIds(
                    Models.Where(model => model.IsVisible).Select(model => model.ModelId));
                await _settingsStateService
                    .SaveValueAsync(_definition, _visibilityService.GetSerializedValue(), ct)
                    .ConfigureAwait(false);
            },
            ct,
            nameof(SaveAsync));
    }

    private bool CanSave()
    {
        return !IsLoading && _isLoaded;
    }

    private bool CanLoad()
    {
        return !IsLoading && !_isLoaded;
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSynchronizing || e.PropertyName != nameof(GenerationModelVisibilityOptionViewModel.IsVisible))
        {
            return;
        }

        SaveCommand.Execute(null);
    }

    private void OnVisibleModelsChanged(object? sender, EventArgs e)
    {
        _isSynchronizing = true;

        try
        {
            foreach (GenerationModelVisibilityOptionViewModel model in Models)
            {
                model.IsVisible = _visibilityService.IsVisible(model.ModelId);
            }
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!_modelCatalog.IsLoaded)
        {
            _isLoaded = false;
        }
        else
        {
            PopulateModels();
            _isLoaded = true;
            ErrorMessage = null;
        }

        NotifyOperationCanExecuteChanged();
    }

    private void PopulateModels()
    {
        foreach (GenerationModelVisibilityOptionViewModel model in _models)
        {
            model.PropertyChanged -= OnModelPropertyChanged;
        }

        _models.Clear();

        IEnumerable<GenerationModelVisibilityOptionViewModel> models = _modelCatalog
            .GetModels()
            .Where(model => GenerationModelVisibilitySettingDefinition.SupportedModelIds.Contains(model.Id))
            .Select(model => new GenerationModelVisibilityOptionViewModel(
                model.Id,
                model.DisplayName,
                _visibilityService.IsVisible(model.Id)));

        foreach (GenerationModelVisibilityOptionViewModel model in models)
        {
            model.PropertyChanged += OnModelPropertyChanged;
            _models.Add(model);
        }
    }
}
