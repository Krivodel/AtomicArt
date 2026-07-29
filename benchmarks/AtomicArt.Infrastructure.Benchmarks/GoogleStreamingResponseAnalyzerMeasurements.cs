using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;

namespace AtomicArt.Infrastructure.Benchmarks;

internal static class GoogleStreamingResponseAnalyzerMeasurements
{
    private const int BlockSize = 65536;
    private const int IterationCount = 15;
    private const int WarmupCount = 5;

    private static readonly int[] ImageDataSizes =
    [
        16 * 1024 * 1024,
        128 * 1024 * 1024
    ];

    private static readonly ClientSignatureScenario[] ClientSignatureScenarios =
    [
        new("Actual response size", 1244508, 2932808),
        new(
            "Large synthetic response",
            16 * 1024 * 1024,
            32 * 1024 * 1024)
    ];

    public static void Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string outputPath = GetOutputPath(args);
        List<ComparisonResult> results = [];
        List<ClientSignatureComparisonResult> clientSignatureResults = [];

        foreach (int imageDataSize in ImageDataSizes)
        {
            byte[] response = CreateResponse(imageDataSize);

            MeasurementResult previous = Measure(
                response,
                AnalyzeWithPreviousImplementation);
            MeasurementResult current = Measure(
                response,
                AnalyzeWithCurrentImplementation);

            results.Add(new ComparisonResult(
                imageDataSize,
                previous,
                current));
        }

        foreach (ClientSignatureScenario scenario in ClientSignatureScenarios)
        {
            byte[] response = CreateResponse(
                scenario.ImageDataSize,
                scenario.SignatureSize);
            MeasurementResult withSignature = Measure(
                response,
                AnalyzeWithCurrentImplementationAndRetainSignature);
            MeasurementResult withoutSignature = Measure(
                response,
                AnalyzeWithCurrentImplementation);

            clientSignatureResults.Add(new ClientSignatureComparisonResult(
                scenario,
                response.LongLength,
                response.LongLength - scenario.SignatureSize,
                withSignature,
                withoutSignature));
        }

        string report = CreateReport(results, clientSignatureResults);
        string? outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, report, new UTF8Encoding(false));
    }

    private static string GetOutputPath(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException(
                "At most one output path can be specified.",
                nameof(args));
        }

        return args.Length == 1
            ? Path.GetFullPath(args[0])
            : Path.Combine(
                AppContext.BaseDirectory,
                "google-streaming-response-analyzer-measurements.md");
    }

    private static MeasurementResult Measure(
        byte[] response,
        Action<byte[]> analyze)
    {
        for (int iteration = 0; iteration < WarmupCount; iteration++)
        {
            analyze(response);
        }

        double[] elapsedMilliseconds = new double[IterationCount];
        long[] allocatedBytes = new long[IterationCount];

        for (int iteration = 0; iteration < IterationCount; iteration++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocationStart = GC.GetAllocatedBytesForCurrentThread();
            long timestampStart = Stopwatch.GetTimestamp();

            analyze(response);

            elapsedMilliseconds[iteration] =
                Stopwatch.GetElapsedTime(timestampStart).TotalMilliseconds;
            allocatedBytes[iteration] =
                GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        }

        Array.Sort(elapsedMilliseconds);
        Array.Sort(allocatedBytes);

        return new MeasurementResult(
            elapsedMilliseconds[IterationCount / 2],
            allocatedBytes[IterationCount / 2]);
    }

    private static void AnalyzeWithPreviousImplementation(byte[] response)
    {
        PreviousGoogleStreamingResponseAnalyzer analyzer = new(
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier());

        long clientResponseBytes = ProcessInBlocks(
            response,
            AppendAndRetain);

        ProviderGenerationSummary summary = analyzer.Complete();
        GC.KeepAlive(summary);
        GC.KeepAlive(clientResponseBytes);

        int AppendAndRetain(Span<byte> content)
        {
            analyzer.Append(content);

            return content.Length;
        }
    }

    private static void AnalyzeWithCurrentImplementation(byte[] response)
    {
        AnalyzeWithCurrentImplementation(response, sanitizeClientResponse: true);
    }

    private static void AnalyzeWithCurrentImplementationAndRetainSignature(
        byte[] response)
    {
        AnalyzeWithCurrentImplementation(
            response,
            sanitizeClientResponse: false);
    }

    private static void AnalyzeWithCurrentImplementation(
        byte[] response,
        bool sanitizeClientResponse)
    {
        GoogleStreamingResponseAnalyzer analyzer = new(
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier(),
            GoogleStreamingResponseAnalyzerBenchmarkLimits
                .MaximumFilteredResponseBytes,
            GoogleStreamingResponseAnalyzerBenchmarkLimits
                .MaximumStructureDepth,
            GoogleStreamingResponseAnalyzerBenchmarkLimits
                .MaximumDiagnosticTextCharacters);

        long clientResponseBytes = ProcessInBlocks(
            response,
            Transform);

        ProviderGenerationSummary summary = analyzer.Complete();
        GC.KeepAlive(summary);
        GC.KeepAlive(clientResponseBytes);

        int Transform(Span<byte> content)
        {
            if (sanitizeClientResponse)
            {
                return analyzer.AppendAndSanitize(content);
            }

            analyzer.Append(content);

            return content.Length;
        }
    }

    private static long ProcessInBlocks(
        byte[] response,
        SpanTransformer transform)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BlockSize);
        long outputBytes = 0L;

        try
        {
            for (int offset = 0; offset < response.Length; offset += BlockSize)
            {
                int count = Math.Min(BlockSize, response.Length - offset);
                response.AsSpan(offset, count).CopyTo(buffer);
                outputBytes += transform(buffer.AsSpan(0, count));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return outputBytes;
    }

    private static byte[] CreateResponse(int imageDataSize)
    {
        return CreateResponse(imageDataSize, signatureSize: 0);
    }

    private static byte[] CreateResponse(
        int imageDataSize,
        int signatureSize)
    {
        byte[] prefix = """
        {
          "status": "completed",
          "steps": [
            {
              "type": "thought",
              "signature": "
        """u8.ToArray();
        byte[] betweenSignatureAndImage = """
        "
            }
          ],
          "output": [
            {
              "type": "image",
              "mime_type": "image/jpeg",
              "data": "
        """u8.ToArray();
        byte[] suffix = """
        "
            }
          ],
          "usage": {
            "total_input_tokens": 10,
            "total_output_tokens": 20,
            "total_tokens": 30
          }
        }
        """u8.ToArray();
        byte[] response = new byte[
            prefix.Length
            + signatureSize
            + betweenSignatureAndImage.Length
            + imageDataSize
            + suffix.Length];

        prefix.CopyTo(response, 0);
        response.AsSpan(prefix.Length, signatureSize).Fill((byte)'S');
        int imageOffset =
            prefix.Length + signatureSize + betweenSignatureAndImage.Length;
        betweenSignatureAndImage.CopyTo(
            response,
            prefix.Length + signatureSize);
        response.AsSpan(imageOffset, imageDataSize).Fill((byte)'A');
        suffix.CopyTo(response, imageOffset + imageDataSize);

        return response;
    }

    private static string CreateReport(
        IReadOnlyList<ComparisonResult> results,
        IReadOnlyList<ClientSignatureComparisonResult> clientSignatureResults)
    {
        StringBuilder report = new();
        report.AppendLine("# GoogleStreamingResponseAnalyzer measurements");
        report.AppendLine();
        report.AppendLine(
            $"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();
        report.AppendLine(
            $"Environment: {Environment.OSVersion}; .NET {Environment.Version}; {RuntimeInformation.ProcessArchitecture}.");
        report.AppendLine();
        report.AppendLine(
            $"Each row uses {WarmupCount} warm-up runs and {IterationCount} measured runs; the median is reported.");
        report.AppendLine(
            $"The response was supplied in {BlockSize.ToString("N0", CultureInfo.InvariantCulture)} byte blocks.");
        report.AppendLine();
        report.AppendLine(
            "| Base64 field | Implementation | Time, ms | Allocated, bytes | Speedup | Allocation reduction |");
        report.AppendLine(
            "|---:|---|---:|---:|---:|---:|");

        foreach (ComparisonResult result in results)
        {
            double speedup = result.Previous.ElapsedMilliseconds
                / result.Current.ElapsedMilliseconds;
            double allocationReduction = 1.0
                - ((double)result.Current.AllocatedBytes
                    / result.Previous.AllocatedBytes);
            string imageSize = FormatMebibytes(result.ImageDataSize);

            report.AppendLine(
                $"| {imageSize} | Previous | {FormatNumber(result.Previous.ElapsedMilliseconds)} | {result.Previous.AllocatedBytes.ToString("N0", CultureInfo.InvariantCulture)} | 1.00× | 0.00% |");
            report.AppendLine(
                $"| {imageSize} | Block-based | {FormatNumber(result.Current.ElapsedMilliseconds)} | {result.Current.AllocatedBytes.ToString("N0", CultureInfo.InvariantCulture)} | {FormatNumber(speedup)}× | {FormatPercent(allocationReduction)} |");
        }

        report.AppendLine();
        report.AppendLine(
            "## Client transfer with and without the signature");
        report.AppendLine();
        report.AppendLine(
            "The measurement includes block-based analysis and building the client blocks in memory; network time is excluded.");
        report.AppendLine();
        report.AppendLine(
            "| Scenario | Variant | Time, ms | Allocated, bytes | Sent to client | Time change |");
        report.AppendLine(
            "|---|---|---:|---:|---:|---:|");

        foreach (ClientSignatureComparisonResult result in clientSignatureResults)
        {
            double elapsedChange = result.WithoutSignature.ElapsedMilliseconds
                / result.WithSignature.ElapsedMilliseconds
                - 1.0;

            report.AppendLine(
                $"| {result.Scenario.Name} (`data` {FormatByteSize(result.Scenario.ImageDataSize)}, `signature` {FormatByteSize(result.Scenario.SignatureSize)}) | With signature | {FormatNumber(result.WithSignature.ElapsedMilliseconds)} | {result.WithSignature.AllocatedBytes.ToString("N0", CultureInfo.InvariantCulture)} | {FormatByteSize(result.ResponseBytes)} | 0.00% |");
            report.AppendLine(
                $"| {result.Scenario.Name} (`data` {FormatByteSize(result.Scenario.ImageDataSize)}, `signature` {FormatByteSize(result.Scenario.SignatureSize)}) | Without signature | {FormatNumber(result.WithoutSignature.ElapsedMilliseconds)} | {result.WithoutSignature.AllocatedBytes.ToString("N0", CultureInfo.InvariantCulture)} | {FormatByteSize(result.SanitizedResponseBytes)} | {FormatSignedPercent(elapsedChange)} |");
        }

        return report.ToString();
    }

    private static string FormatMebibytes(int byteCount)
    {
        int mebibytes = byteCount / (1024 * 1024);

        return $"{mebibytes.ToString(CultureInfo.InvariantCulture)} MiB";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatByteSize(long byteCount)
    {
        double mebibytes = byteCount / (1024.0 * 1024.0);

        return $"{mebibytes.ToString("0.00", CultureInfo.InvariantCulture)} MiB";
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("P2", CultureInfo.InvariantCulture);
    }

    private static string FormatSignedPercent(double value)
    {
        string prefix = value > 0.0 ? "+" : string.Empty;

        return string.Concat(prefix, FormatPercent(value));
    }

    private delegate int SpanTransformer(Span<byte> content);

    private sealed record MeasurementResult(
        double ElapsedMilliseconds,
        long AllocatedBytes);

    private sealed record ComparisonResult(
        int ImageDataSize,
        MeasurementResult Previous,
        MeasurementResult Current);

    private sealed record ClientSignatureScenario(
        string Name,
        int ImageDataSize,
        int SignatureSize);

    private sealed record ClientSignatureComparisonResult(
        ClientSignatureScenario Scenario,
        long ResponseBytes,
        long SanitizedResponseBytes,
        MeasurementResult WithSignature,
        MeasurementResult WithoutSignature);
}
