using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Controls.Overlays;
using AtomicArt.Desktop.Tests.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Controls.Overlays;

public sealed class ModalOverlayLayerControlTests : AnimatedGalleryControlTestBase
{
    private const int LowerOrder = 100;
    private const int UpperOrder = 200;

    [Theory]
    [InlineData(Key.Escape, PhysicalKey.Escape)]
    [InlineData(Key.Cancel, PhysicalKey.Pause)]
    public void DismissKey_WhenMultipleOverlaysAreOpen_ClosesOnlyHighestOrder(
        Key key,
        PhysicalKey physicalKey)
    {
        Dispatch(() =>
        {
            int lowerCloseCount = 0;
            int upperCloseCount = 0;
            ModalOverlayPresenterControl lowerOverlay = new()
            {
                Body = new Border(),
                IsOpen = true,
                Order = LowerOrder
            };
            ModalOverlayPresenterControl upperOverlay = new()
            {
                Body = new Border(),
                IsOpen = true,
                Order = UpperOrder
            };
            lowerOverlay.CloseCommand = new RelayCommand(() =>
            {
                lowerCloseCount++;
                lowerOverlay.IsOpen = false;
            });
            upperOverlay.CloseCommand = new RelayCommand(() =>
            {
                upperCloseCount++;
                upperOverlay.IsOpen = false;
            });
            ModalOverlayLayerControl layer = new();
            layer.Children.Add(lowerOverlay);
            layer.Children.Add(upperOverlay);
            Window window = Show(layer);

            try
            {
                window.KeyPress(
                    key,
                    RawInputModifiers.None,
                    physicalKey,
                    null);

                lowerCloseCount.Should().Be(0);
                upperCloseCount.Should().Be(1);
                lowerOverlay.IsOpen.Should().BeTrue();
                upperOverlay.IsOpen.Should().BeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
