using AtomicArt.Desktop.Services.Paths;
using Krivodeling.Localization.Avalonia;

namespace AtomicArt.Desktop.Services.Localization;

internal sealed class AtomicArtLocalizationFileStore : LocalizationFileStore
{
    public override string DirectoryPath => _pathProvider.LocalizationsDirectory;

    private const string FailureMessage = "Localization path is not a trusted Atomic Art data path.";

    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly TrustedFileStreamFactory _streamFactory;

    internal AtomicArtLocalizationFileStore(
        IAtomicArtDataPathProvider pathProvider,
        TrustedFileStreamFactory streamFactory)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));
    }

    public override void EnsureDirectoryExists()
    {
        _pathProvider.EnsureDirectoryExists(DirectoryPath);
    }

    public override FileStream? OpenRead(string path)
    {
        string[] directories = [DirectoryPath];
        _streamFactory.TryOpenExistingFileForRead(path, directories, _pathProvider.RootDirectory,
            FailureMessage, out FileStream? stream, out _);

        return stream;
    }

    public override FileStream CreateNewFile(string path)
    {
        return _streamFactory.CreateNewFileForWrite(DirectoryPath, path, FailureMessage);
    }

    public override string CreateTemporaryPath()
    {
        return AtomicFileWriteTempPath.CreateHidden(DirectoryPath, LocalizationConstants.TemplateFileName);
    }

    public override void ReplaceFile(string source, string destination)
    {
        TrustedPathGuard.ReplaceTrustedFile(DirectoryPath, source, destination, FailureMessage);
    }

    public override void DeleteFile(string path)
    {
        FileDeletion.DeleteIfExists(path);
    }
}
