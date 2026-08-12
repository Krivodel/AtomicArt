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
    private readonly TrustedFileStreamFactory _trustedFileStreamFactory;

    public GenerationStreamingResultStore(
        IAtomicArtDataPathProvider pathProvider,
        IGenerationImageFormatRegistry formatRegistry,
        GenerationImageFileNamePolicy fileNamePolicy,
        TrustedFileStreamFactory trustedFileStreamFactory)
    {
        _pathProvider = pathProvider
            ?? throw new ArgumentNullException(nameof(pathProvider));
        _formatRegistry = formatRegistry
            ?? throw new ArgumentNullException(nameof(formatRegistry));
        _fileNamePolicy = fileNamePolicy
            ?? throw new ArgumentNullException(nameof(fileNamePolicy));
        _trustedFileStreamFactory = trustedFileStreamFactory
            ?? throw new ArgumentNullException(nameof(trustedFileStreamFactory));
    }

    public GenerationTemporaryResult CreateTemporaryResult()
    {
        string resultsDirectory = Path.GetFullPath(_pathProvider.ArtDirectory);
        TrustedPathGuard.EnsureTrustedDirectoryExists(
            _pathProvider,
            resultsDirectory,
            TrustedPathFailureMessage);
        string temporaryPath = Path.GetFullPath(Path.Combine(
            resultsDirectory,
            $"{Guid.NewGuid():N}.part"));
        TrustedPathGuard.EnsureTrustedWriteTarget(
            resultsDirectory,
            temporaryPath,
            TrustedPathFailureMessage);
        FileStream stream = _trustedFileStreamFactory.CreateNewFileForWrite(
            resultsDirectory,
            temporaryPath,
            TrustedPathFailureMessage);

        return new GenerationTemporaryResult(
            temporaryPath,
            stream,
            resultsDirectory,
            _formatRegistry,
            _fileNamePolicy);
    }
}
