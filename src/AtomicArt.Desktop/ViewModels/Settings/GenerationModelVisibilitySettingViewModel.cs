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
    public IReadOnlyList<GenerationModelVisibilityRowViewModel> Rows => _rows;

    protected override IRelayCommand OperationCommand => SaveCommand;

    private readonly GenerationModelVisibilitySettingDefinition _definition;
    private readonly IGenerationModelCatalogApiClient _catalogApiClient;
    private readonly IImageModelOptionCatalog _modelCatalog;
    private readonly GenerationModelVisibilityService _visibilityService;
    private readonly ISettingsStateService _settingsStateService;
    private IEnumerable<GenerationModelVisibilityOptionViewModel> Options => Rows.SelectMany(row => row.Options);
    private IReadOnlyList<GenerationModelVisibilityRowViewModel> _rows =
        Array.Empty<GenerationModelVisibilityRowViewModel>();
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

        _visibilityService.VisibleModelsChanged -= OnVisibleModelsChanged;
        _modelCatalog.CatalogChanged -= OnCatalogChanged;
        ReplaceRows(Array.Empty<GenerationModelVisibilityRowViewModel>());
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

                if (!_isLoaded)
                {
                    PopulateModels();
                    _isLoaded = true;
                }
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
                _visibilityService.UpdateVisibleModels(
                    Options.ToDictionary(model => model.ModelId, model => model.IsVisible, StringComparer.Ordinal));
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
            foreach (GenerationModelVisibilityOptionViewModel model in Options)
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
            ReplaceRows(Array.Empty<GenerationModelVisibilityRowViewModel>());
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
        IReadOnlyList<GenerationModelVisibilityRowViewModel> rows = _modelCatalog
            .GetModels()
            .Where(model => GenerationModelVisibilitySettingDefinition.SupportedModelIds.Contains(model.Id))
            .GroupBy(GetModelIdentity, StringComparer.Ordinal)
            .Select(models => (Row: CreateRow(models), VendorId: GetVendorId(models.First())))
            .OrderBy(model => model.Row.DisplayName, StringComparer.Ordinal)
            .GroupBy(model => model.VendorId, StringComparer.Ordinal)
            .SelectMany((family, familyIndex) => family.Select((model, modelIndex) => model.Row with
            {
                StartsFamily = familyIndex > 0 && modelIndex == 0
            }))
            .ToArray();

        ReplaceRows(rows);
    }

    private void ReplaceRows(IReadOnlyList<GenerationModelVisibilityRowViewModel> rows)
    {
        foreach (GenerationModelVisibilityOptionViewModel model in Options)
        {
            model.PropertyChanged -= OnModelPropertyChanged;
        }

        foreach (GenerationModelVisibilityOptionViewModel model in rows.SelectMany(row => row.Options))
        {
            model.PropertyChanged += OnModelPropertyChanged;
        }

        _rows = rows;
        OnPropertyChanged(nameof(Rows));
    }

    private GenerationModelVisibilityRowViewModel CreateRow(IGrouping<string, ImageModelOption> models)
    {
        ImageModelOption? openRouter = models.FirstOrDefault(model => model.Provider == GenerationProviderIds.OpenRouter);
        ImageModelOption? direct = models.FirstOrDefault(model => model.Provider == GenerationProviderIds.Google);
        return new GenerationModelVisibilityRowViewModel(
            (openRouter ?? models.First()).DisplayName,
            CreateModelOption(openRouter),
            CreateModelOption(direct));
    }

    private GenerationModelVisibilityOptionViewModel? CreateModelOption(ImageModelOption? model)
    {
        return model is null
            ? null
            : new GenerationModelVisibilityOptionViewModel(
                model.Id,
                model.DisplayName,
                _visibilityService.IsVisible(model.Id));
    }

    private static string GetModelIdentity(ImageModelOption model)
    {
        const string OpenRouterPrefix = GenerationProviderIds.OpenRouter + "-";
        return model.Provider == GenerationProviderIds.OpenRouter
            && model.Id.StartsWith(OpenRouterPrefix, StringComparison.Ordinal)
            ? model.Id[OpenRouterPrefix.Length..]
            : model.Id;
    }

    private static string GetVendorId(ImageModelOption model)
    {
        int separatorIndex = model.ProviderModelId.IndexOf('/');
        return separatorIndex > 0 ? model.ProviderModelId[..separatorIndex] : model.Provider;
    }
}
