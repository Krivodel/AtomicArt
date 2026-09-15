using Avalonia;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Controls;

public sealed class Dlss5ParameterPanel : Panel
{
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }
    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<Dlss5ParameterPanel, int>(nameof(Columns), 1);
    public static readonly StyledProperty<double> ItemWidthProperty =
        AvaloniaProperty.Register<Dlss5ParameterPanel, double>(nameof(ItemWidth), 292);
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<Dlss5ParameterPanel, int>(nameof(Rows), 1);

    static Dlss5ParameterPanel()
    {
        AffectsMeasure<Dlss5ParameterPanel>(ColumnsProperty, ItemWidthProperty, RowsProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double itemWidth = Math.Max(0, ItemWidth);
        int columns = Math.Max(1, Columns);
        int rows = Math.Max(1, Math.Min(Rows, Children.Count));
        double desiredHeight = 0;

        foreach (Control child in Children)
        {
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
        }

        for (int row = 0; row < rows; row++)
        {
            desiredHeight += GetRowHeight(row, rows, columns);
        }

        return new Size(columns * itemWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double itemWidth = Math.Max(0, ItemWidth);
        int columns = Math.Max(1, Columns);
        int rows = Math.Max(1, Math.Min(Rows, Children.Count));
        double top = 0;

        for (int row = 0; row < rows; row++)
        {
            double rowHeight = GetRowHeight(row, rows, columns);

            for (int column = 0; column < columns; column++)
            {
                int index = (column * rows) + row;
                if (index >= Children.Count)
                {
                    continue;
                }

                Children[index].Arrange(
                    new Rect(column * itemWidth, top, itemWidth, rowHeight));
            }

            top += rowHeight;
        }

        return finalSize;
    }

    private double GetRowHeight(int row, int rows, int columns)
    {
        double rowHeight = 0;

        for (int column = 0; column < columns; column++)
        {
            int index = (column * rows) + row;
            if (index >= Children.Count)
            {
                continue;
            }

            rowHeight = Math.Max(rowHeight, Children[index].DesiredSize.Height);
        }

        return rowHeight;
    }
}
