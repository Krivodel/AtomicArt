using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class VirtualFileDropInputSessionTests
{
    [Fact]
    public void Scope_WhenInputsAreNotTaken_DisposesThem()
    {
        TrackingDisposable resource = new();
        ImageAttachmentInput input = CreateInput(resource);
        VirtualFileDropInputSession session = new();

        using (session.Begin(new ImageAttachmentInput[] { input }))
        {
        }

        resource.IsDisposed.Should().BeTrue();
        session.TryTakeInputs(out _).Should().BeFalse();
    }

    [Fact]
    public void TryTakeInputs_WithActiveSession_TransfersOwnership()
    {
        TrackingDisposable resource = new();
        ImageAttachmentInput input = CreateInput(resource);
        VirtualFileDropInputSession session = new();

        using (session.Begin(new ImageAttachmentInput[] { input }))
        {
            bool wasTaken = session.TryTakeInputs(
                out IReadOnlyList<ImageAttachmentInput> inputs);

            wasTaken.Should().BeTrue();
            inputs.Should().ContainSingle().Which.Should().BeSameAs(input);
        }

        resource.IsDisposed.Should().BeFalse();

        input.Dispose();

        resource.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Begin_WithActiveSession_ThrowsInvalidOperationException()
    {
        ImageAttachmentInput input = ImageAttachmentInput.FromImage(
            new AttachedImageDto(
                "image.png",
                "image/png",
                new byte[] { 1 }));
        VirtualFileDropInputSession session = new();

        using IDisposable scope = session.Begin(
            new ImageAttachmentInput[] { input });
        Action act = () => session.Begin(
            Array.Empty<ImageAttachmentInput>());

        act.Should().Throw<InvalidOperationException>();
    }

    private static ImageAttachmentInput CreateInput(
        TrackingDisposable resource)
    {
        return new ImageAttachmentInput(
            "image.png",
            _ => Task.FromResult<AttachedImageDto?>(null),
            resource);
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
