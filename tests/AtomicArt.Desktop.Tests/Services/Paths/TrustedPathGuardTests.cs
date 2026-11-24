using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Paths;

public sealed class TrustedPathGuardTests
{
    [Fact]
    public async Task MoveTrustedFile_WithAnExistingTarget_KeepsBothFilesUnchanged()
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(TrustedPathGuardTests));

        try
        {
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "source.png");
            string target = Path.Combine(root, "target.png");
            byte[] sourceContent = [1, 2, 3];
            byte[] targetContent = [4, 5, 6];
            await File.WriteAllBytesAsync(source, sourceContent);
            await File.WriteAllBytesAsync(target, targetContent);

            Action move = () => TrustedPathGuard.MoveTrustedFile(root, source, target, "Unsafe image move.");

            move.Should().Throw<IOException>();
            (await File.ReadAllBytesAsync(source)).Should().Equal(sourceContent);
            (await File.ReadAllBytesAsync(target)).Should().Equal(targetContent);
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveTrustedFile_WithAPathOutsideTheTrustedDirectory_KeepsTheSource(bool sourceOutside)
    {
        string root = TestDirectories.GetUniqueDirectoryPath(typeof(TrustedPathGuardTests));

        try
        {
            string trusted = Path.Combine(root, "trusted");
            Directory.CreateDirectory(trusted);
            string source = Path.Combine(sourceOutside ? root : trusted, "source.png");
            string target = Path.Combine(sourceOutside ? trusted : root, "target.png");
            byte[] content = [1, 2, 3];
            await File.WriteAllBytesAsync(source, content);

            Action move = () => TrustedPathGuard.MoveTrustedFile(trusted, source, target, "Unsafe image move.");

            move.Should().Throw<IOException>();
            (await File.ReadAllBytesAsync(source)).Should().Equal(content);
            File.Exists(target).Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(root);
        }
    }
}
