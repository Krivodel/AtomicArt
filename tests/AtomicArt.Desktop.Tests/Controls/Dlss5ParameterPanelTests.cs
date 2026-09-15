using Avalonia;
using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Controls;

public sealed class Dlss5ParameterPanelTests : DesktopControlTestBase
{
    [Fact]
    public void Layout_FillsColumnsTopToBottomWithoutChangingItemOrder()
    {
        Dispatch(() =>
        {
            Dlss5ParameterPanel panel = new()
            {
                Columns = 3,
                ItemWidth = 100,
                Rows = 2
            };

            for (int index = 0; index < 5; index++)
            {
                panel.Children.Add(new Border { Height = 40 });
            }

            Size available = new(300, 80);
            panel.Measure(available);
            panel.Arrange(new Rect(available));

            panel.Children[0].Bounds.Left.Should().Be(0);
            panel.Children[1].Bounds.Left.Should().Be(0);
            panel.Children[1].Bounds.Top.Should().Be(40);
            panel.Children[2].Bounds.Left.Should().Be(100);
            panel.Children[2].Bounds.Top.Should().Be(0);
            panel.Children[3].Bounds.Left.Should().Be(100);
            panel.Children[3].Bounds.Top.Should().Be(40);
            panel.Children[4].Bounds.Left.Should().Be(200);
            panel.Children[4].Bounds.Top.Should().Be(0);
        });
    }
}
