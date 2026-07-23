using Avalonia;
using Avalonia.Controls;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryLayoutServiceTests
{
    private static readonly Guid FirstItemId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondItemId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid ThirdItemId = new("00000000-0000-0000-0000-000000000003");

    [Fact]
    public void CardGeometryConstants_WhenRead_MatchAtomicArtCardCellGeometry()
    {
        GalleryLayoutService.CardWidth.Should().Be(236d);
        GalleryLayoutService.CardHeight.Should().Be(338d);
        GalleryLayoutService.CardGap.Should().Be(0d);
        GalleryLayoutService.CardTopPadding.Should().Be(20d);
        GalleryLayoutService.OverscanPixels.Should().Be(360d);
    }

    [Theory]
    [InlineData(235d, 1)]
    [InlineData(236d, 1)]
    [InlineData(471d, 1)]
    [InlineData(472d, 2)]
    [InlineData(708d, 3)]
    public void CalculateColumnCount_WhenViewportChanges_UsesAtomicArtCellFormula(double viewportWidth, int expected)
    {
        GalleryLayoutService service = new();

        int result = service.CalculateColumnCount(viewportWidth);

        result.Should().Be(expected);
    }

    [Fact]
    public void GetLogicalCardRect_WhenIndexProvided_UsesAtomicArtCellPitchAndCoordinates()
    {
        GalleryLayoutService service = new();

        Rect result = service.GetLogicalCardRect(5, 3);

        result.X.Should().Be(472d);
        result.Y.Should().Be(GalleryLayoutService.CardTopPadding + GalleryLayoutService.CardHeight);
        result.Width.Should().Be(236d);
        result.Height.Should().Be(338d);
    }

    [Fact]
    public void CalculateVisibleIndexRange_WhenViewportProvided_IncludesOverscanRows()
    {
        GalleryLayoutService service = new();

        (int start, int end) = service.CalculateVisibleIndexRange(
            40,
            3,
            800d,
            500d);

        start.Should().Be(3);
        end.Should().Be(18);
    }

    [Fact]
    public void RenderCards_WithStaleScrollOffset_ClampsOffsetAndKeepsTopCardsVisible()
    {
        GalleryLayoutService service = new();
        List<object> items =
        [
            FirstItemId,
            SecondItemId,
            ThirdItemId
        ];
        ScrollViewer scrollViewer = new();
        scrollViewer.Arrange(new Rect(0d, 0d, 560d, 420d));
        scrollViewer.Offset = new Vector(0d, 10000d);
        GalleryOperationCoordinator context = CreateContext(scrollViewer, items);

        service.RenderCards(context);

        scrollViewer.Offset.Y.Should().Be(0d);
        context.CardControls.Should().ContainKey(FirstItemId);
        Canvas.GetTop(context.CardControls[FirstItemId]).Should().Be(GalleryLayoutService.CardTopPadding);
    }

    [Fact]
    public void RenderCards_WithPartialFirstRow_SizesPanelToUsedColumns()
    {
        GalleryLayoutService service = new();
        List<object> items =
        [
            FirstItemId,
            SecondItemId
        ];
        ScrollViewer scrollViewer = new();
        scrollViewer.Arrange(new Rect(0d, 0d, 740d, 420d));
        GalleryOperationCoordinator context = CreateContext(scrollViewer, items);

        service.RenderCards(context);

        context.GalleryPanel.Width.Should().Be(GalleryLayoutService.CardWidth * 2d);
    }

    private static GalleryOperationCoordinator CreateContext(
        ScrollViewer scrollViewer,
        IList<object> items)
    {
        DiscardingUiFrameScheduler frameScheduler = new();
        GalleryOperationCoordinator context = GalleryOperationCoordinatorTestFactory.Create(
            frameScheduler,
            new GalleryOperationRunnerRegistry(new List<IGalleryOperationRunner>()));

        context.AttachScene(
            scrollViewer,
            new Canvas(),
            new Canvas(),
            items,
            item => (Guid)item,
            _ => new Border(),
            () => Task.CompletedTask);

        return context;
    }
}
