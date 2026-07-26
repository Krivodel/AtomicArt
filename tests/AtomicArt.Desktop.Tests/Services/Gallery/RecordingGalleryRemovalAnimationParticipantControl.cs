using Avalonia.Controls;

using AtomicArt.Desktop.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

internal sealed class RecordingGalleryRemovalAnimationParticipantControl :
    Control,
    IGalleryRemovalAnimationParticipant
{
    public bool WasPreparedForRemovalTransfer { get; private set; }
    public int? RemovalDurationMilliseconds { get; private set; }

    public void PrepareForRemovalTransfer()
    {
        WasPreparedForRemovalTransfer = true;
    }

    public void BeginRemovalAnimation(int durationMilliseconds)
    {
        RemovalDurationMilliseconds = durationMilliseconds;
    }
}
