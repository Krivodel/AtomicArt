using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationStreamingResultStore
    : IGenerationStreamingResultStore
{
    private static readonly string TrustedPathFailureMessage =
        TrustedPathGuard.CreateFailureMessage(
            "Streaming generation result path",
            AtomicArtPathNames.ArtDirectory);

    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly IGenerationImageFormatRegistry _formatRegistry;
    private readonly GenerationImageFileNamePolicy _fileNamePolicy;
    private readonly string _resultsDirectory;

    public GenerationStreamingResultStore(
        IAtomicArtDataPathProvider pathProvider,
        IGenerationImageFormatRegistry formatRegistry,
        GenerationImageFileNamePolicy fileNamePolicy)
    {
        _pathProvider = pathProvider
            ?? throw new ArgumentNullException(nameof(pathProvider));
        _formatRegistry = formatRegistry
            ?? throw new ArgumentNullException(nameof(formatRegistry));
        _fileNamePolicy = fileNamePolicy
            ?? throw new ArgumentNullException(nameof(fileNamePolicy));
        _resultsDirectory = Path.GetFullPath(pathProvider.ArtDirectory);
    }

    public GenerationTemporaryResult CreateTemporaryResult()
    {
        TrustedPathGuard.EnsureTrustedDirectoryExists(
            _pathProvider,
            _resultsDirectory,
            TrustedPathFailureMessage);
        string temporaryPath = Path.GetFullPath(Path.Combine(
            _resultsDirectory,
            $"generation-{Guid.NewGuid():N}.part"));
        TrustedPathGuard.EnsureTrustedWriteTarget(
            _resultsDirectory,
            temporaryPath,
            TrustedPathFailureMessage);
        FileStream stream = TrustedPathGuard.CreateTrustedNewFileForWrite(
            _resultsDirectory,
            temporaryPath,
            TrustedPathFailureMessage);

        return new GenerationTemporaryResult(
            temporaryPath,
            stream,
            _resultsDirectory,
            _formatRegistry,
            _fileNamePolicy);
    }
}
