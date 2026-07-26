namespace AtomicArt.Desktop.Controls.Gallery;

internal interface IGalleryRemovalAnimationParticipant
{
    void PrepareForRemovalTransfer();

    void BeginRemovalAnimation(int durationMilliseconds);
}
