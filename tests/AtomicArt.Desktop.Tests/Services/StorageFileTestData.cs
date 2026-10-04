using Avalonia.Platform.Storage;
using Moq;

namespace AtomicArt.Desktop.Tests.Services;

internal static class StorageFileTestData
{
    public static Mock<IStorageFile> CreateFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Mock<IStorageFile> fileMock = new();
        fileMock.SetupGet(file => file.Name).Returns(Path.GetFileName(filePath));
        fileMock.SetupGet(file => file.Path).Returns(new Uri(filePath));

        return fileMock;
    }
}
