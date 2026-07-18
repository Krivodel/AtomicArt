using Avalonia.Controls;
using Avalonia.Media;
using Avalonia;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.GalleryAnimation;

public sealed class MotionFrameApplierTests
{
    [Fact]
    public void Apply_WithMotionFrame_UpdatesTransformsOpacityAndOrigin()
    {
        Border control = new();
        MotionFrame frame = new(12d, 24d, 1.25d, 18d, 0.72d);

        MotionFrameApplier.Apply(control, frame);

        TransformGroup transformGroup = control.RenderTransform.Should().BeOfType<TransformGroup>().Subject;
        transformGroup.Children.Should().HaveCount(3);
        ScaleTransform scale = transformGroup.Children[0].Should().BeOfType<ScaleTransform>().Subject;
        RotateTransform rotate = transformGroup.Children[1].Should().BeOfType<RotateTransform>().Subject;
        TranslateTransform translate = transformGroup.Children[2].Should().BeOfType<TranslateTransform>().Subject;
        scale.ScaleX.Should().Be(1.25d);
        scale.ScaleY.Should().Be(1.25d);
        rotate.Angle.Should().Be(18d);
        translate.X.Should().Be(12d);
        translate.Y.Should().Be(24d);
        control.Opacity.Should().Be(0.72d);
        control.RenderTransformOrigin.Should().Be(RelativePoint.Center);
    }
}
