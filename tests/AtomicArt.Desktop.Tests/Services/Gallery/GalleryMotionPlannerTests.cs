using Avalonia;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryMotionPlannerTests
{
    [Fact]
    public void BuildFlightFramesFromCurrent_WhenCurrentFrameProvided_UsesReferenceFrontGenerationFormula()
    {
        Rect targetRect = new(
            100d,
            200d,
            GalleryLayoutService.CardWidth,
            GalleryLayoutService.CardHeight);
        Point startCenter = new(10d, 20d);
        Point packCenter = new(180d, 250d);

        List<MotionFrame> frames = GalleryMotionPlanner.BuildFlightFramesFromCurrent(
            2,
            4,
            targetRect,
            startCenter,
            0.30d,
            0d,
            packCenter);

        frames.Should().HaveCount(4);
        frames[0].X.Should().BeApproximately(-208d, 0.000000000001d);
        frames[0].Y.Should().BeApproximately(-349d, 0.000000000001d);
        frames[0].Scale.Should().Be(0.30d);
        frames[0].Rotate.Should().BeApproximately(0.279d, 0.000000000001d);
        frames[0].Opacity.Should().Be(0d);
        frames[1].X.Should().BeApproximately(-96.09d, 0.000000000001d);
        frames[1].Y.Should().BeApproximately(-208.88d, 0.000000000001d);
        frames[1].Scale.Should().Be(0.74d);
        frames[1].Rotate.Should().BeApproximately(1.6275d, 0.000000000001d);
        frames[1].Opacity.Should().Be(1d);
        frames[2].X.Should().BeApproximately(-16.64d, 0.000000000001d);
        frames[2].Y.Should().BeApproximately(-27.92d, 0.000000000001d);
        frames[2].Scale.Should().Be(1.006d);
        frames[2].Rotate.Should().BeApproximately(0.062d, 0.000000000001d);
        frames[2].Opacity.Should().Be(1d);
        frames[3].Should().Be(new MotionFrame(0d, 0d, 1d, 0d, 1d));
    }

    [Fact]
    public void BuildExistingFrames_ForDirectMove_EndsWithIdentityFrame()
    {
        Rect first = new(0d, 0d, GalleryLayoutService.CardWidth, GalleryLayoutService.CardHeight);
        Rect last = new(
            GalleryLayoutService.CardWidth + GalleryLayoutService.CardGap,
            0d,
            GalleryLayoutService.CardWidth,
            GalleryLayoutService.CardHeight);

        List<MotionFrame> frames = GalleryMotionPlanner.BuildExistingFrames(
            first,
            last,
            0,
            1,
            new Rect(0d, 0d, 700d, 600d));

        frames.Should().HaveCount(5);
        frames[0].X.Should().Be(-(GalleryLayoutService.CardWidth + GalleryLayoutService.CardGap));
        frames[0].Y.Should().Be(0d);
        frames[0].Scale.Should().Be(1d);
        frames[0].Opacity.Should().Be(1d);
        frames[^1].Should().Be(new MotionFrame(0d, 0d, 1d, 0d, 1d));
    }

    [Fact]
    public void GetPackCenter_WhenTargetsProvided_UsesFirstRowCardCentersAndTopBias()
    {
        List<Rect> targets =
        [
            new(0d, 0d, GalleryLayoutService.CardWidth, GalleryLayoutService.CardHeight),
            new(
                GalleryLayoutService.CardWidth + GalleryLayoutService.CardGap,
                0d,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight),
            new(
                0d,
                GalleryLayoutService.CardHeight + GalleryLayoutService.CardGap,
                GalleryLayoutService.CardWidth,
                GalleryLayoutService.CardHeight)
        ];

        Point result = GalleryMotionPlanner.GetPackCenter(targets);

        result.X.Should().Be(236d);
        result.Y.Should().BeApproximately(141.96d, 0.000000000001d);
    }
}
