using Avalonia.Controls;

using AtomicArt.Desktop.Controls.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

internal sealed class RecordingGalleryRemovalAnimationParticipantControl :
    Control,
    IGalleryRemovalAnimationParticipant
{
    public int? RemovalDurationMilliseconds { get; private set; }

    public void BeginRemovalAnimation(int durationMilliseconds)
    {
        RemovalDurationMilliseconds = durationMilliseconds;
    }
}
