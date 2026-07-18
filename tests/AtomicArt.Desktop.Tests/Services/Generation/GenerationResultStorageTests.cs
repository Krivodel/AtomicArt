using System.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationResultStorageTests
{
    private static readonly Guid BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SaveAsync_WithValidImageContent_WritesFileInsideResults()
    {
        string rootDirectory = CreateCleanDirectory(nameof(SaveAsync_WithValidImageContent_WritesFileInsideResults));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        byte[] validPngBytes = GenerationImageTestData.ValidPngBytes;
        GenerationImageContentValidationResult content = ValidateContent(GenerationImageContentTypes.Png, validPngBytes);
        GenerationResultStorage storage = new(
            pathProvider,
            GenerationImageFormatRegistryTestFactory.Create(),
            new GenerationImageFileNamePolicy(),
            NullLogger<GenerationResultStorage>.Instance);

        string? resultPath = storage.GetExpectedResultPathOrDefault(
            BatchId,
            ItemId,
            content.ContentType);

        await storage.SaveAsync(
            BatchId,
            ItemId,
            content,
            CancellationToken.None);

        resultPath.Should().NotBeNull();
        File.Exists(resultPath).Should().BeTrue();
        Path.GetDirectoryName(resultPath).Should().Be(Path.GetFullPath(pathProvider.ArtDirectory));
        Path.GetFileName(resultPath).Should().Contain(BatchId.ToString("N"));
        Path.GetFileName(resultPath).Should().Contain(ItemId.ToString("N"));
        byte[] savedBytes = await File.ReadAllBytesAsync(resultPath);
        savedBytes.Should().Equal(validPngBytes);
    }

    [Fact]
    public async Task SaveAsync_WhenResultsDirectoryIsFile_ThrowsAndDoesNotWriteFile()
    {
        string rootDirectory = CreateCleanDirectory(nameof(SaveAsync_WhenResultsDirectoryIsFile_ThrowsAndDoesNotWriteFile));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        File.WriteAllText(pathProvider.ArtDirectory, "occupied");
        GenerationImageContentValidationResult content = ValidateContent(
            GenerationImageContentTypes.Png,
            GenerationImageTestData.ValidPngBytes);
        GenerationResultStorage storage = new(
            pathProvider,
            GenerationImageFormatRegistryTestFactory.Create(),
            new GenerationImageFileNamePolicy(),
            NullLogger<GenerationResultStorage>.Instance);

        string? resultPath = storage.GetExpectedResultPathOrDefault(
            BatchId,
            ItemId,
            content.ContentType);

        Func<Task> act = () => storage.SaveAsync(
            BatchId,
            ItemId,
            content,
            CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        resultPath.Should().NotBeNull();
        File.Exists(resultPath).Should().BeFalse();
    }

    [Fact]
    public void SaveAsync_WithUnsupportedContentType_ReturnsErrorState()
    {
        string resultsDirectory = CreateCleanDirectory(nameof(SaveAsync_WithUnsupportedContentType_ReturnsErrorState));
        GenerationImageContentValidator validator = GenerationImageFormatRegistryTestFactory.CreateValidator();
        GenerationImageContentDto content = new(
            GenerationImageContentTypes.Gif,
            Convert.ToBase64String(GenerationImageTestData.ValidPngBytes));

        bool result = validator.TryValidate(content, out GenerationImageContentValidationResult? validationResult);

        result.Should().BeFalse();
        validationResult.Should().BeNull();
        Directory.GetFiles(resultsDirectory).Should().BeEmpty();
    }

    [Fact]
    public void SaveAsync_WithOversizedBase64_ReturnsErrorState()
    {
        string resultsDirectory = CreateCleanDirectory(nameof(SaveAsync_WithOversizedBase64_ReturnsErrorState));
        GenerationImageContentValidator validator = GenerationImageFormatRegistryTestFactory.CreateValidator(
            GenerationImageTestData.TestMaxImageBytes);
        GenerationImageContentDto content = new(
            GenerationImageContentTypes.Png,
            new string('A', GenerationImageTestData.TestOversizedBase64Length));

        bool result = validator.TryValidate(content, out GenerationImageContentValidationResult? validationResult);

        result.Should().BeFalse();
        validationResult.Should().BeNull();
        Directory.GetFiles(resultsDirectory).Should().BeEmpty();
    }

    [Fact]
    public void SaveAsync_WithInvalidSignature_ReturnsErrorState()
    {
        string resultsDirectory = CreateCleanDirectory(nameof(SaveAsync_WithInvalidSignature_ReturnsErrorState));
        GenerationImageContentValidator validator = GenerationImageFormatRegistryTestFactory.CreateValidator();
        GenerationImageContentDto content = new(
            GenerationImageContentTypes.Png,
            Convert.ToBase64String(new byte[] { 0x01, 0x02, 0x03 }));

        bool result = validator.TryValidate(content, out GenerationImageContentValidationResult? validationResult);

        result.Should().BeFalse();
        validationResult.Should().BeNull();
        Directory.GetFiles(resultsDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WhenParentDirectoryIsReparsePoint_ThrowsAndDoesNotWriteOutsideResults()
    {
        string rootDirectory = CreateCleanDirectory(nameof(SaveAsync_WhenParentDirectoryIsReparsePoint_ThrowsAndDoesNotWriteOutsideResults));
        string redirectedDirectory = Path.Combine(rootDirectory, "Redirected");
        string atomicArtDirectory = Path.Combine(rootDirectory, "AtomicArt");
        AtomicArtDataPathProvider pathProvider = new(atomicArtDirectory);
        Directory.CreateDirectory(redirectedDirectory);
        await CreateJunctionAsync(atomicArtDirectory, redirectedDirectory);
        GenerationImageContentValidationResult content = ValidateContent(
            GenerationImageContentTypes.Png,
            GenerationImageTestData.ValidPngBytes);
        GenerationResultStorage storage = new(
            pathProvider,
            GenerationImageFormatRegistryTestFactory.Create(),
            new GenerationImageFileNamePolicy(),
            NullLogger<GenerationResultStorage>.Instance);

        Func<Task> act = () => storage.SaveAsync(
            BatchId,
            ItemId,
            content,
            CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        Directory.Exists(Path.Combine(redirectedDirectory, "Art")).Should().BeFalse();
    }

    [Fact]
    public void GetExpectedResultPathOrDefault_WithSupportedContentType_UsesFileNamePolicy()
    {
        string rootDirectory = CreateCleanDirectory(
            nameof(GetExpectedResultPathOrDefault_WithSupportedContentType_UsesFileNamePolicy));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        GenerationImageFileNamePolicy fileNamePolicy = new();
        GenerationResultStorage storage = new(
            pathProvider,
            GenerationImageFormatRegistryTestFactory.Create(),
            fileNamePolicy,
            NullLogger<GenerationResultStorage>.Instance);
        string expectedFileName = fileNamePolicy.BuildFileName(BatchId, ItemId, ".png");

        string? resultPath = storage.GetExpectedResultPathOrDefault(
            BatchId,
            ItemId,
            GenerationImageContentTypes.Png);

        resultPath.Should().NotBeNull();
        Path.GetFileName(resultPath).Should().Be(expectedFileName);
    }

    private static GenerationImageContentValidationResult ValidateContent(string contentType, byte[] bytes)
    {
        GenerationImageContentValidator validator = GenerationImageFormatRegistryTestFactory.CreateValidator();
        GenerationImageContentDto content = new(contentType, Convert.ToBase64String(bytes));
        bool isValid = validator.TryValidate(content, out GenerationImageContentValidationResult? validationResult);

        if (!isValid || validationResult is null)
        {
            throw new InvalidOperationException("Test image content must be valid.");
        }

        return validationResult;
    }

    private static string CreateCleanDirectory(string name)
    {
        string directory = DesktopTestDirectories.GetDirectoryPath(name);

        DeleteDirectoryIfExists(directory);
        Directory.CreateDirectory(directory);

        return directory;
    }

    private static async Task CreateJunctionAsync(string junctionPath, string targetPath)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "cmd.exe",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(junctionPath);
        startInfo.ArgumentList.Add(targetPath);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start junction creation process.");
        string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        string error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to create test junction. Output: {output}. Error: {error}.");
        }
    }

    private static void DeleteDirectoryIfExists(string directory)
    {
        DirectoryInfo directoryInfo = new(directory);

        if (!directoryInfo.Exists)
        {
            return;
        }

        DeleteDirectory(directoryInfo);
    }

    private static void DeleteDirectory(DirectoryInfo directory)
    {
        directory.Refresh();

        if (!directory.Exists)
        {
            return;
        }

        if ((directory.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
        {
            directory.Delete();
            return;
        }

        foreach (FileSystemInfo child in directory.EnumerateFileSystemInfos())
        {
            if ((child.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                child.Delete();
                continue;
            }

            if (child is DirectoryInfo childDirectory)
            {
                DeleteDirectory(childDirectory);
                continue;
            }

            child.Delete();
        }

        directory.Delete();
    }
}
