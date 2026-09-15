using System.Diagnostics;
using System.Security.Cryptography;

using SkiaSharp;

using AtomicArt.Desktop.Services.Dlss5;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Dlss5.Benchmarks;

const int ExpectedArgumentCount = 2;
const int DataRootArgumentIndex = 0;
const int SourceImageArgumentIndex = 1;
const int UsageErrorExitCode = 2;
const int FailureExitCode = 1;
const int SuccessExitCode = 0;
const int AlternateSourceDimension = 512;
const int MinimumSourceDimension = 128;
const int BenchmarkTimeoutMinutes = 3;
const int ExpectedWorkerStartCount = 3;
const float InitialGeneralIntensity = 0.37f;
const float ChangedLocalToneStrength = 0.63f;

if (args.Length != ExpectedArgumentCount)
{
    Console.Error.WriteLine("Usage: AtomicArt.Dlss5.Benchmarks <data-root> <source-image>");
    return UsageErrorExitCode;
}

AtomicArtDataPathProvider paths = new(args[DataRootArgumentIndex]);
Dlss5ModulePaths modulePaths = new(paths);

if (!File.Exists(modulePaths.DynamicAddonPath))
{
    Console.Error.WriteLine("The dynamic DLSS 5 add-on must be installed for this benchmark.");
    return UsageErrorExitCode;
}

using SKBitmap source = SKBitmap.Decode(args[SourceImageArgumentIndex])
    ?? throw new InvalidDataException("The benchmark source could not be decoded.");
using SKBitmap otherSource = source.Resize(
    new SKImageInfo(
        AlternateSourceDimension,
        AlternateSourceDimension,
        SKColorType.Rgba8888,
        SKAlphaType.Unpremul),
    new SKSamplingOptions(SKFilterMode.Linear))
    ?? throw new InvalidDataException("The alternate benchmark source could not be resized.");
if ((source.Width < MinimumSourceDimension) || (source.Height < MinimumSourceDimension)
    || ((source.Width == otherSource.Width) && (source.Height == otherSource.Height)))
{
    Console.Error.WriteLine("Use a source at least 128x128 with dimensions other than 512x512.");
    return UsageErrorExitCode;
}

using CancellationTokenSource cancellation = new(TimeSpan.FromMinutes(BenchmarkTimeoutMinutes));
PreparationLogger logger = new();
using Dlss5NativeEngine engine = new(modulePaths, logger);

await engine.InitializeAsync(null, cancellation.Token).ConfigureAwait(false);

Dlss5RenderSettings initial = Dlss5RenderSettings.Default with
{
    GeneralIntensity = InitialGeneralIntensity
};
Dlss5RenderSettings changed = initial with
{
    Style = Dlss5Style.Natural,
    LocalToneStrength = ChangedLocalToneStrength
};

string initialHash = await RenderAsync("first-size", source, initial).ConfigureAwait(false);
string changedHash = await RenderAsync("same-size-new-settings", source, changed).ConfigureAwait(false);
await RenderAsync("second-size", otherSource, initial).ConfigureAwait(false);
string reusedHash = await RenderAsync("return-with-new-settings", source, changed).ConfigureAwait(false);
int startsBeforeReference = logger.WorkerStarts;
string initialReferenceHash = await RenderAsync("initial-settings-reference", source, initial).ConfigureAwait(false);
bool pixelsMatch = string.Equals(reusedHash, changedHash, StringComparison.Ordinal);
bool firstFrameMatches = string.Equals(initialHash, initialReferenceHash, StringComparison.Ordinal);
Console.WriteLine($"worker_starts_before_reference={startsBeforeReference}; expected={ExpectedWorkerStartCount}; pixels_match={pixelsMatch}; first_frame_matches={firstFrameMatches}");
return (startsBeforeReference == ExpectedWorkerStartCount) && (pixelsMatch) && (firstFrameMatches)
    ? SuccessExitCode
    : FailureExitCode;

async Task<string> RenderAsync(string scenario, SKBitmap bitmap, Dlss5RenderSettings settings)
{
    Stopwatch stopwatch = Stopwatch.StartNew();

    await engine.PrepareAsync(bitmap.Width, bitmap.Height, settings, null, cancellation.Token)
        .ConfigureAwait(false);

    double preparationMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
    Dlss5NativeRenderResult result = await engine.RenderAsync(bitmap, settings, cancellation.Token)
        .ConfigureAwait(false);
    using SKBitmap output = result.Bitmap;

    Console.WriteLine($"{scenario}: prepare_ms={preparationMilliseconds:F1}; total_ms={stopwatch.Elapsed.TotalMilliseconds:F1}");
    return Convert.ToHexString(SHA256.HashData(output.GetPixelSpan()));
}
