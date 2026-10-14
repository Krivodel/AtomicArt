using Avalonia;
using Avalonia.Controls;

using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Windowing;

public sealed class GenerationPanelLayoutTracker : IDisposable
{
    private readonly IAppStateStore _stateStore;
    private readonly IStateWriteScheduler _writeScheduler;
    private readonly GenerationPanelLayoutStateSection _stateSection;
    private readonly ILogger<GenerationPanelLayoutTracker> _logger;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private Control? _panel;
    private RowDefinition? _row;
    private bool _isTracking;
    private bool _widthChangedDuringRestore;
    private bool _heightChangedDuringRestore;
    private bool _isDisposed;

    public GenerationPanelLayoutTracker(
        IAppStateStore stateStore,
        IStateWriteScheduler writeScheduler,
        GenerationPanelLayoutStateSection stateSection,
        ILogger<GenerationPanelLayoutTracker> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _writeScheduler = writeScheduler ?? throw new ArgumentNullException(nameof(writeScheduler));
        _stateSection = stateSection ?? throw new ArgumentNullException(nameof(stateSection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AttachAsync(Control panel, RowDefinition row)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(row);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_panel is not null)
        {
            return;
        }

        _panel = panel;
        _row = row;
        double initialWidth = panel.MaxWidth;
        panel.PropertyChanged += OnPanelPropertyChanged;
        row.PropertyChanged += OnRowPropertyChanged;
        CancellationToken ct = _lifetimeCancellation.Token;
        GenerationPanelLayoutState state;

        try
        {
            state = await _stateStore.LoadAsync<GenerationPanelLayoutState>(_stateSection, ct);
            state = state.CreateNormalized();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore the generation panel dimensions.");
            state = new GenerationPanelLayoutState();
        }

        if (_isDisposed)
        {
            return;
        }

        bool resizedDuringRestore = _widthChangedDuringRestore || _heightChangedDuringRestore;

        if (!_widthChangedDuringRestore && state.Width is double width)
        {
            panel.SetCurrentValue(Control.MaxWidthProperty, Math.Max(initialWidth, width));
        }

        if (!_heightChangedDuringRestore && state.Height is double height)
        {
            row.SetCurrentValue(RowDefinition.HeightProperty, new GridLength(Math.Max(row.MinHeight, height)));
        }

        _isTracking = true;

        if (resizedDuringRestore)
        {
            ScheduleStateWrite();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();

        if (_panel is not null)
        {
            _panel.PropertyChanged -= OnPanelPropertyChanged;
            _panel = null;
        }

        if (_row is not null)
        {
            _row.PropertyChanged -= OnRowPropertyChanged;
            _row = null;
        }
    }

    private void ScheduleStateWrite()
    {
        if (!_isTracking || _panel is null || _row is null)
        {
            return;
        }

        GenerationPanelLayoutState state = new GenerationPanelLayoutState
        {
            Width = _panel.MaxWidth,
            Height = _row.Height.IsAbsolute ? _row.Height.Value : null
        }.CreateNormalized();

        _writeScheduler.ScheduleWrite(_stateSection, state);
    }

    private void OnPanelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property == Control.MaxWidthProperty)
        {
            if (!_isTracking)
            {
                _widthChangedDuringRestore = true;
                return;
            }

            ScheduleStateWrite();
        }
    }

    private void OnRowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property == RowDefinition.HeightProperty)
        {
            if (!_isTracking)
            {
                _heightChangedDuringRestore = true;
                return;
            }

            ScheduleStateWrite();
        }
    }
}
