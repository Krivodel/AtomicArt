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
            $"generation-{Guid.NewGuid():N}.part"));
        TrustedPathGuard.EnsureTrustedWriteTarget(
            resultsDirectory,
            temporaryPath,
            TrustedPathFailureMessage);
        FileStream stream = TrustedPathGuard.CreateTrustedNewFileForWrite(
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
