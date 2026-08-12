using Avalonia.Controls;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Views.Settings;

namespace AtomicArt.Desktop.Tests.Views.Settings;

public sealed class BooleanSettingViewTests : DesktopControlTestBase
{
    [Fact]
    public void Layout_WhenCreated_PlacesLabelAndCheckBoxInSameRow()
    {
        Dispatch(() =>
        {
            BooleanSettingView view = new();
            Grid grid = view.Content
                .Should()
                .BeOfType<Grid>()
                .Subject;
            TextBlock label = grid.Children
                .OfType<TextBlock>()
                .Single(textBlock => textBlock.Classes.Contains("settings-label"));
            CheckBox checkBox = grid.Children
                .OfType<CheckBox>()
                .Single();

            Grid.GetRow(label).Should().Be(0);
            Grid.GetRow(checkBox).Should().Be(0);
            Grid.GetColumn(label).Should().Be(0);
            Grid.GetColumn(checkBox).Should().Be(1);
        });
    }
}
