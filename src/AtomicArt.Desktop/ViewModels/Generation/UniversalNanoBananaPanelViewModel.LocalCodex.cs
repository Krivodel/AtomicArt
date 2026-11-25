using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;

namespace AtomicArt.Desktop.ViewModels.Generation;

public sealed partial class UniversalNanoBananaPanelViewModel
{
    private readonly LocalCodexAppServer? _codexAppServer;
    private readonly LocalCodexSettings? _codexSettings;
    private CancellationTokenSource? _codexModelLoad;
    private string? _defaultCodexModelId;
    private string? _configuredDefaultCodexEffort;
    private string? _savedCodexModelId;
    private string? _savedCodexEffort;
    private bool _updatingCodexSelection;

    [ObservableProperty]
    private IReadOnlyList<LocalCodexTextModel> _codexTextModels = Array.Empty<LocalCodexTextModel>();

    [ObservableProperty]
    private LocalCodexTextModel? _selectedCodexTextModel;

    [ObservableProperty]
    private IReadOnlyList<GenerationOptionViewModel> _codexReasoningEfforts = Array.Empty<GenerationOptionViewModel>();

    [ObservableProperty]
    private GenerationOptionViewModel? _selectedCodexReasoningEffort;

    private void EnsureCodexModelsLoaded()
    {
        if (!_isDisposed && IsLocalCodexSelected && _codexAppServer is not null
            && _codexSettings?.Current is { Enabled: true } options
            && _codexModelLoad is null && CodexTextModels.Count == 0)
        {
            CancellationTokenSource load = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationSource.Token);
            _codexModelLoad = load;
            GenerateCommand.NotifyCanExecuteChanged();
            _ = LoadCodexModelsAsync(_codexAppServer, options, load);
        }
    }

    private async Task LoadCodexModelsAsync(LocalCodexAppServer appServer, LocalCodexOptions options, CancellationTokenSource load)
    {
        try
        {
            (IReadOnlyList<LocalCodexTextModel> models, string? defaultModel, string? defaultEffort) =
                await appServer.ListTextModelsAsync(options, load.Token);
            if (!load.IsCancellationRequested)
            {
                _defaultCodexModelId = defaultModel;
                _configuredDefaultCodexEffort = defaultEffort;
                ApplyCodexSelection(models);
            }
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(LoadCodexModelsAsync));
        }
        finally
        {
            if (ReferenceEquals(_codexModelLoad, load))
            {
                _codexModelLoad = null;
                GenerateCommand.NotifyCanExecuteChanged();
            }

            load.Dispose();
        }
    }

    private void CancelCodexModelLoad()
    {
        CancellationTokenSource? load = _codexModelLoad;
        _codexModelLoad = null;
        load?.Cancel();
    }

    private void ApplyCodexSelection(IReadOnlyList<LocalCodexTextModel> models)
    {
        _updatingCodexSelection = true;
        try
        {
            CodexTextModels = models;
            SelectedCodexTextModel = models.FirstOrDefault(model => model.Id == _savedCodexModelId)
                ?? models.FirstOrDefault(model => model.Id == _defaultCodexModelId)
                ?? models.FirstOrDefault();
            UpdateCodexReasoningEfforts();
        }
        finally
        {
            _updatingCodexSelection = false;
        }
    }

    private void UpdateCodexReasoningEfforts()
    {
        LocalCodexTextModel? model = SelectedCodexTextModel;
        CodexReasoningEfforts = model?.ReasoningEfforts.Select(effort => new GenerationOptionViewModel(
            effort,
            LocalCodexReasoningEffort.GetLocalizationKey(effort),
            _textProvider)).ToArray() ?? Array.Empty<GenerationOptionViewModel>();
        SelectedCodexReasoningEffort = CodexReasoningEfforts.FirstOrDefault(effort => effort.Value == _savedCodexEffort)
            ?? GetDefaultCodexReasoningEffort();
    }

    private GenerationOptionViewModel? GetDefaultCodexReasoningEffort()
    {
        LocalCodexTextModel? model = SelectedCodexTextModel;
        string? defaultEffort = model?.Id == _defaultCodexModelId
            ? _configuredDefaultCodexEffort
            : null;
        return CodexReasoningEfforts.FirstOrDefault(effort => effort.Value == LocalCodexReasoningEffort.Default)
            ?? CodexReasoningEfforts.FirstOrDefault(effort => effort.Value == defaultEffort)
            ?? CodexReasoningEfforts.FirstOrDefault(effort => effort.Value == model?.DefaultReasoningEffort)
            ?? CodexReasoningEfforts.FirstOrDefault();
    }

    private void ResetCodexReasoningEffort()
    {
        GenerationOptionViewModel? defaultEffort = GetDefaultCodexReasoningEffort();
        if (defaultEffort is null)
        {
            return;
        }

        SelectedCodexReasoningEffort = defaultEffort;
    }

    private LocalCodexTurnOptions? CreateCodexTurnOptions()
    {
        return IsLocalCodexSelected && SelectedCodexTextModel is { } model
            ? new LocalCodexTurnOptions(model.Id, model.DisplayName, SelectedCodexReasoningEffort?.Value)
            : null;
    }

    private void RestoreCodexSelection(string? modelId, string? effort)
    {
        _savedCodexModelId = modelId;
        _savedCodexEffort = effort;
        ApplyCodexSelection(CodexTextModels);
    }

    private void ApplyCodexPreset(GenerationRequestSettings? settings)
    {
        if (settings?.CodexTextModelId is not { } modelId)
        {
            return;
        }

        RestoreCodexSelection(modelId, settings.GetString(GenerationParameterNames.ThinkingLevel));
    }

    private void OnCodexConnectionChanged(object? sender, EventArgs e)
    {
        CancelCodexModelLoad();
        ApplyCodexSelection(Array.Empty<LocalCodexTextModel>());
        EnsureCodexModelsLoaded();
        GenerateCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCodexTextModelChanged(LocalCodexTextModel? value)
    {
        if (!_updatingCodexSelection && value is not null)
        {
            _savedCodexModelId = value.Id;
            _updatingCodexSelection = true;
            try
            {
                UpdateCodexReasoningEfforts();
            }
            finally
            {
                _updatingCodexSelection = false;
            }

            if (_savedCodexEffort != SelectedCodexReasoningEffort?.Value)
            {
                _savedCodexEffort = null;
            }

            SchedulePanelStateSave(nameof(OnSelectedCodexTextModelChanged));
        }
    }

    partial void OnSelectedCodexReasoningEffortChanged(GenerationOptionViewModel? value)
    {
        if (!_updatingCodexSelection && value is not null)
        {
            _savedCodexEffort = value == GetDefaultCodexReasoningEffort() ? null : value.Value;
            SchedulePanelStateSave(nameof(OnSelectedCodexReasoningEffortChanged));
        }
    }
}
