using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.ViewModels.Dlss5;

namespace AtomicArt.Desktop.Views.Dlss5;

public partial class Dlss5SessionView : UserControl
{
    private const double PreferredParameterWidth = 292;
    private const double MinimumParameterWidth = 248;
    private const double ParameterRowHeight = 68;

    private Dlss5SessionViewModel? _subscribedViewModel;
    private bool _isClearingSource;
    private bool _isAttached;

    public Dlss5SessionView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _isAttached = true;
        SubscribeToViewModel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        UnsubscribeFromViewModel();

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        UnsubscribeFromViewModel();
        if (_isAttached)
        {
            SubscribeToViewModel();
        }
    }

    private async void OnClearSourceClick(
        object? sender,
        RoutedEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_isClearingSource || (DataContext is not Dlss5SessionViewModel viewModel))
        {
            return;
        }

        _isClearingSource = true;
        try
        {
            await ComparisonPanel.CloseSourceAsync();
            if (viewModel.ClearSourceCommand.CanExecute(null))
            {
                viewModel.ClearSourceCommand.Execute(null);
            }
        }
        finally
        {
            _isClearingSource = false;
        }
    }

    private void OnParameterLayoutSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        double availableWidth = ParameterViewport.Bounds.Width;
        int count = ParametersPanel.Children.Count;
        if ((availableWidth <= 0) || (count == 0))
        {
            return;
        }

        int maximumColumns = Math.Max(1, (int)(availableWidth / MinimumParameterWidth));
        int availableRows = Math.Max(1, (int)(ParameterViewport.Bounds.Height / ParameterRowHeight));
        int rows = Math.Min(count, availableRows);
        int columns = (int)Math.Ceiling((double)count / rows);
        if (columns > maximumColumns)
        {
            columns = maximumColumns;
            rows = (int)Math.Ceiling((double)count / columns);
        }

        double itemWidth = Math.Min(PreferredParameterWidth, availableWidth / columns);

        ParametersPanel.Width = columns * itemWidth;
        ParametersPanel.Columns = columns;
        ParametersPanel.ItemWidth = itemWidth;
        ParametersPanel.Rows = rows;
    }

    private void OnResizeCompleted(object? sender, VectorEventArgs args)
    {
        _ = sender;
        _ = args;

        if (DataContext is Dlss5SessionViewModel viewModel)
        {
            viewModel.ParameterAreaHeight = Math.Clamp(
                LayoutGrid.RowDefinitions[3].ActualHeight,
                Dlss5SessionState.MinimumParameterAreaHeight,
                Dlss5SessionState.MaximumParameterAreaHeight);
        }
    }

    private void SubscribeToViewModel()
    {
        if ((DataContext is not Dlss5SessionViewModel viewModel)
            || ReferenceEquals(_subscribedViewModel, viewModel))
        {
            return;
        }

        _subscribedViewModel = viewModel;
        _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyParameterAreaHeight(viewModel.ParameterAreaHeight);
    }

    private void UnsubscribeFromViewModel()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribedViewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if ((sender is Dlss5SessionViewModel viewModel)
            && string.Equals(
                args.PropertyName,
                nameof(Dlss5SessionViewModel.ParameterAreaHeight),
                StringComparison.Ordinal))
        {
            ApplyParameterAreaHeight(viewModel.ParameterAreaHeight);
        }
    }

    private void ApplyParameterAreaHeight(double height)
    {
        double normalizedHeight = double.IsFinite(height)
            ? Math.Clamp(
                height,
                Dlss5SessionState.MinimumParameterAreaHeight,
                Dlss5SessionState.MaximumParameterAreaHeight)
            : Dlss5SessionState.DefaultParameterAreaHeight;
        LayoutGrid.RowDefinitions[3].Height = new GridLength(normalizedHeight);
    }
}
