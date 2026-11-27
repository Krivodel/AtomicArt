using FluentAssertions;
using NAudio.Wave;
using Xunit;

using AtomicArt.Desktop.Services.Notifications;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Notifications;

public sealed class NotificationSoundStoreTests : IDisposable
{
    private readonly string _root = TestDirectories.GetUniqueDirectoryPath(typeof(NotificationSoundStoreTests));

    [Fact]
    public async Task ImportAsync_WithValidSound_KeepsOneExactCopyAfterTheSourceIsDeleted()
    {
        Directory.CreateDirectory(_root);
        string source = Path.Combine(_root, "source.wav");
        byte[] content = CreateWave();
        await File.WriteAllBytesAsync(source, content);
        NotificationSoundStore store = CreateStore();

        await store.ImportAsync(source, CancellationToken.None);
        await store.ImportAsync(source, CancellationToken.None);
        File.Delete(source);

        store.HasCustomSound.Should().BeTrue();
        (await store.ReadAsync(CancellationToken.None)).Should().Equal(content);
        Directory.GetFiles(Path.Combine(_root, "Notifications")).Should().ContainSingle();
    }

    [Fact]
    public async Task ImportAsync_WithInvalidReplacement_PreservesTheExistingSound()
    {
        Directory.CreateDirectory(_root);
        string source = Path.Combine(_root, "source.wav");
        byte[] content = CreateWave();
        await File.WriteAllBytesAsync(source, content);
        NotificationSoundStore store = CreateStore();
        await store.ImportAsync(source, CancellationToken.None);
        await File.WriteAllTextAsync(source, "not an audio file");

        Func<Task> import = () => store.ImportAsync(source, CancellationToken.None);

        await import.Should().ThrowAsync<NotificationSoundException>();
        (await store.ReadAsync(CancellationToken.None)).Should().Equal(content);
        Directory.GetFiles(Path.Combine(_root, "Notifications")).Should().ContainSingle();
    }

    [Fact]
    public async Task ResetAsync_WithCustomSound_ReturnsToTheDefaultWithoutRemovingOtherFiles()
    {
        Directory.CreateDirectory(_root);
        string source = Path.Combine(_root, "source.wav");
        await File.WriteAllBytesAsync(source, CreateWave());
        NotificationSoundStore store = CreateStore();
        await store.ImportAsync(source, CancellationToken.None);

        await store.ResetAsync(CancellationToken.None);

        store.HasCustomSound.Should().BeFalse();
        (await store.ReadAsync(CancellationToken.None)).Should().BeNull();
        File.Exists(source).Should().BeTrue();
    }

    [Fact]
    public async Task ReadAsync_AfterDataRootSwitch_UsesTheNewRoot()
    {
        Directory.CreateDirectory(_root);
        string source = Path.Combine(_root, "source.wav");
        byte[] content = CreateWave();
        await File.WriteAllBytesAsync(source, content);
        AtomicArtDataPathProvider paths = new(_root);
        NotificationSoundStore store = new(paths, new DataRootAccessCoordinator());
        await store.ImportAsync(source, CancellationToken.None);
        string nextRoot = Path.Combine(_root, "next-root");
        Directory.CreateDirectory(Path.Combine(nextRoot, "Notifications"));
        File.Move(Path.Combine(_root, "Notifications", "Sound"), Path.Combine(nextRoot, "Notifications", "Sound"));

        paths.SwitchRootDirectory(nextRoot);

        store.HasCustomSound.Should().BeTrue();
        (await store.ReadAsync(CancellationToken.None)).Should().Equal(content);
    }

    public void Dispose()
    {
        TestDirectories.DeleteIfExists(_root);
    }

    private static byte[] CreateWave()
    {
        using MemoryStream stream = new();
        using (WaveFileWriter writer = new(stream, new WaveFormat(8000, 16, 1)))
        {
            writer.Write(new byte[1600], 0, 1600);
            writer.Flush();
        }

        return stream.ToArray();
    }

    private NotificationSoundStore CreateStore()
    {
        return new NotificationSoundStore(new AtomicArtDataPathProvider(_root), new DataRootAccessCoordinator());
    }
}
