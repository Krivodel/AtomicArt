using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Gallery;

public sealed class GenerationMetadataViewModel : ObservableObject, IDisposable
{
    public GenerationItemViewModel Item { get; private set; }
    public string Prompt => Item.Prompt;
    public string ModelDisplayName => Item.ModelDisplayName;
    public IReadOnlyList<GenerationReference> References => Item.References ?? Array.Empty<GenerationReference>();
    public bool HasReferences => References.Count > 0;
    public IReadOnlyList<GenerationMetadataRow> Rows => EnumerateFields()
        .Chunk(FieldsPerRow)
        .Select(row => new GenerationMetadataRow(
            row[0],
            row.ElementAtOrDefault(1),
            row.ElementAtOrDefault(2)))
        .ToArray();
    public string Status => Item.Status;
    public bool IsGenerated => Item.IsGenerated;
    public bool IsGenerating => Item.IsGenerating;
    public bool IsFailed => Item.IsFailed;
    public bool IsPromptCopied
    {
        get => _isPromptCopied;
        private set => SetProperty(ref _isPromptCopied, value);
    }
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand OpenViewerCommand { get; }
    public IRelayCommand ShowFailureDetailsCommand { get; }
    public IRelayCommand RepeatCommand { get; }
    public IAsyncRelayCommand<GenerationReference?>? OpenReferenceCommand { get; }
    public IAsyncRelayCommand CopyPromptCommand { get; }
    public IRelayCommand<OverlayNavigationDirection>? NavigateCommand { get; }

    private const int FieldsPerRow = 3;

    private readonly ITextClipboardService _textClipboardService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly GenerationPriceFormatter _priceFormatter;
    private readonly GenerationDurationFormatter _durationFormatter;
    private readonly ILocalizationTextProvider _textProvider;
    private int _promptCopyFeedbackVersion;
    private bool _isPromptCopied;

    private GenerationMetadataViewModel(
        GenerationItemViewModel item,
        IRelayCommand closeCommand,
        IRelayCommand openViewerCommand,
        IRelayCommand showFailureDetailsCommand,
        IRelayCommand repeatCommand,
        ITextClipboardService textClipboardService,
        IViewModelErrorHandler errorHandler,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter,
        ILocalizationTextProvider textProvider,
        IAsyncRelayCommand<GenerationReference?>? openReferenceCommand,
        IRelayCommand<OverlayNavigationDirection>? navigateCommand)
    {
        Item = item;
        CloseCommand = closeCommand;
        OpenViewerCommand = openViewerCommand;
        ShowFailureDetailsCommand = showFailureDetailsCommand;
        RepeatCommand = repeatCommand;
        OpenReferenceCommand = openReferenceCommand;
        NavigateCommand = navigateCommand;
        _textClipboardService = textClipboardService;
        _errorHandler = errorHandler;
        _priceFormatter = priceFormatter;
        _durationFormatter = durationFormatter;
        _textProvider = textProvider;
        CopyPromptCommand = new AsyncRelayCommand(CopyPromptAsync);
        Item.PropertyChanged += OnItemPropertyChanged;
    }

    public static GenerationMetadataViewModel FromItem(
        GenerationItemViewModel item,
        IRelayCommand closeCommand,
        IRelayCommand openViewerCommand,
        IRelayCommand showFailureDetailsCommand,
        IRelayCommand repeatCommand,
        ITextClipboardService textClipboardService,
        IViewModelErrorHandler errorHandler,
        GenerationPriceFormatter priceFormatter,
        GenerationDurationFormatter durationFormatter,
        ILocalizationTextProvider textProvider,
        IAsyncRelayCommand<GenerationReference?>? openReferenceCommand = null,
        IRelayCommand<OverlayNavigationDirection>? navigateCommand = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(closeCommand);
        ArgumentNullException.ThrowIfNull(openViewerCommand);
        ArgumentNullException.ThrowIfNull(showFailureDetailsCommand);
        ArgumentNullException.ThrowIfNull(repeatCommand);
        ArgumentNullException.ThrowIfNull(textClipboardService);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(priceFormatter);
        ArgumentNullException.ThrowIfNull(durationFormatter);
        ArgumentNullException.ThrowIfNull(textProvider);

        return new GenerationMetadataViewModel(
            item,
            closeCommand,
            openViewerCommand,
            showFailureDetailsCommand,
            repeatCommand,
            textClipboardService,
            errorHandler,
            priceFormatter,
            durationFormatter,
            textProvider,
            openReferenceCommand,
            navigateCommand);
    }

    internal void SelectItem(GenerationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (ReferenceEquals(Item, item))
        {
            return;
        }

        OnPropertyChanging(nameof(Item));
        ResetPromptCopyFeedback();
        Item.PropertyChanged -= OnItemPropertyChanged;
        Item = item;
        Item.PropertyChanged += OnItemPropertyChanged;
        OnPropertyChanged(string.Empty);
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Status));
    }

    public void Dispose()
    {
        ResetPromptCopyFeedback();
        Item.PropertyChanged -= OnItemPropertyChanged;
    }

    internal void ResetPromptCopyFeedback()
    {
        _promptCopyFeedbackVersion++;
        IsPromptCopied = false;
    }

    private IEnumerable<GenerationMetadataField> EnumerateFields()
    {
        DateTime createdAt = Item.CreatedAtUtc.ToLocalTime();
        string unavailable = _textProvider.Get(GalleryLocalizationKeys.Metadata.Unavailable);
        bool isLocalCodex = LocalCodexImageModel.Matches(Item.ModelId);

        yield return new GenerationMetadataField(
            _textProvider.Get(GalleryLocalizationKeys.Metadata.CreatedAt),
            createdAt.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture),
            createdAt.ToString(" HH:mm", CultureInfo.CurrentCulture));
        yield return new GenerationMetadataField(
            _textProvider.Get(GalleryLocalizationKeys.Metadata.GenerationDuration),
            _durationFormatter.Format(Item.GenerationDuration) ?? unavailable);
        if (!isLocalCodex)
        {
            string priceText = Item.Price is GenerationPriceDto price
                ? string.Concat(_priceFormatter.FormatCurrency(price), _priceFormatter.FormatAmount(price))
                : unavailable;
            yield return new GenerationMetadataField(
                _textProvider.Get(GalleryLocalizationKeys.Metadata.Price),
                priceText);
        }

        yield return new GenerationMetadataField(
            _textProvider.Get(GalleryLocalizationKeys.Metadata.Resolution),
            Item.IsGenerating || string.IsNullOrWhiteSpace(Item.Resolution)
                || (isLocalCodex && GenerationAspectRatios.IsAuto(Item.Resolution)) ? unavailable : Item.Resolution);
        if (!isLocalCodex)
        {
            yield return new GenerationMetadataField(
                _textProvider.Get(GalleryLocalizationKeys.Metadata.AspectRatio),
                FormatAspectRatio(Item.AspectRatio));
        }

        if (Item.RequestSettings is not { } settings)
        {
            yield break;
        }

        foreach ((string name, GenerationSavedParameter parameter) in settings.Parameters)
        {
            string? value = GenerationParameterText.FormatValue(name, parameter.Value, _textProvider);
            if (string.IsNullOrWhiteSpace(value) || ShouldHideSizeParameter(name, value, isLocalCodex))
            {
                continue;
            }

            yield return new GenerationMetadataField(
                LocalizeOption(name, parameter.LabelLocalizationKey),
                LocalizeOption(value, parameter.ValueLocalizationKey));
        }
    }

    private bool ShouldHideSizeParameter(string name, string value, bool isLocalCodex)
    {
        return name switch
        {
            GenerationParameterNames.AspectRatio =>
                isLocalCodex || string.Equals(value, Item.AspectRatio, StringComparison.OrdinalIgnoreCase),
            GenerationParameterNames.Resolution =>
                isLocalCodex || string.Equals(value, Item.Resolution, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private string FormatAspectRatio(string aspectRatio)
    {
        return GenerationAspectRatios.IsAuto(aspectRatio)
            ? _textProvider.Get(GenerationLocalizationKeys.OptionsAuto)
            : aspectRatio;
    }

    private string LocalizeOption(string value, string? localizationKey)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
        {
            return value;
        }

        string localized = _textProvider.Get(localizationKey);

        return string.Equals(localized, localizationKey, StringComparison.Ordinal)
            ? value
            : localized;
    }

    private async Task CopyPromptAsync(CancellationToken ct)
    {
        int feedbackVersion = _promptCopyFeedbackVersion;

        try
        {
            await _textClipboardService.SetTextAsync(Prompt, ct);

            if (feedbackVersion == _promptCopyFeedbackVersion && !ct.IsCancellationRequested)
            {
                IsPromptCopied = true;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _errorHandler.Log(ex, nameof(CopyPromptAsync));
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            OnPropertyChanged(string.Empty);
            return;
        }

        if (e.PropertyName == nameof(GenerationItemViewModel.Prompt))
        {
            ResetPromptCopyFeedback();
        }

        switch (e.PropertyName)
        {
            case nameof(GenerationItemViewModel.CreatedAtUtc):
            case nameof(GenerationItemViewModel.ModelId):
            case nameof(GenerationItemViewModel.Price):
            case nameof(GenerationItemViewModel.Resolution):
            case nameof(GenerationItemViewModel.AspectRatio):
            case nameof(GenerationItemViewModel.GenerationDuration):
                OnPropertyChanged(nameof(Rows));
                break;
            case nameof(GenerationItemViewModel.IsGenerating):
                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(e.PropertyName);
                break;
            case nameof(GenerationItemViewModel.Prompt):
            case nameof(GenerationItemViewModel.ModelDisplayName):
            case nameof(GenerationItemViewModel.Status):
            case nameof(GenerationItemViewModel.IsGenerated):
            case nameof(GenerationItemViewModel.IsFailed):
                OnPropertyChanged(e.PropertyName);
                break;
        }
    }
}
