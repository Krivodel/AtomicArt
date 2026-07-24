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
    private const int IterationCount = 5;
    private const int WarmupCount = 2;

    private static readonly int[] ImageDataSizes =
    [
        16 * 1024 * 1024,
        128 * 1024 * 1024
    ];

    public static void Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string outputPath = GetOutputPath(args);
        List<ComparisonResult> results = [];

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

        string report = CreateReport(results);
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

        AppendInBlocks(response, analyzer.Append);

        ProviderGenerationSummary summary = analyzer.Complete();
        GC.KeepAlive(summary);
    }

    private static void AnalyzeWithCurrentImplementation(byte[] response)
    {
        GoogleStreamingResponseAnalyzer analyzer = new(
            new GoogleInteractionsResponseParser(),
            new GoogleInteractionsFailureClassifier());

        AppendInBlocks(response, analyzer.Append);

        ProviderGenerationSummary summary = analyzer.Complete();
        GC.KeepAlive(summary);
    }

    private static void AppendInBlocks(
        byte[] response,
        SpanConsumer append)
    {
        for (int offset = 0; offset < response.Length; offset += BlockSize)
        {
            int count = Math.Min(BlockSize, response.Length - offset);
            append(response.AsSpan(offset, count));
        }
    }

    private static byte[] CreateResponse(int imageDataSize)
    {
        byte[] prefix = """
        {
          "status": "completed",
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
        byte[] response = new byte[prefix.Length + imageDataSize + suffix.Length];

        prefix.CopyTo(response, 0);
        response.AsSpan(prefix.Length, imageDataSize).Fill((byte)'A');
        suffix.CopyTo(response, prefix.Length + imageDataSize);

        return response;
    }

    private static string CreateReport(IReadOnlyList<ComparisonResult> results)
    {
        StringBuilder report = new();
        report.AppendLine("# Измерения GoogleStreamingResponseAnalyzer");
        report.AppendLine();
        report.AppendLine(
            $"Дата: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();
        report.AppendLine(
            $"Среда: {Environment.OSVersion}; .NET {Environment.Version}; {RuntimeInformation.ProcessArchitecture}.");
        report.AppendLine();
        report.AppendLine(
            $"Для каждой строки выполнено {WarmupCount} прогревочных и {IterationCount} измеряемых запусков; приведена медиана.");
        report.AppendLine(
            $"Ответ подавался блоками по {BlockSize.ToString("N0", CultureInfo.InvariantCulture)} байт.");
        report.AppendLine();
        report.AppendLine(
            "| Base64-поле | Реализация | Время, мс | Выделено, байт | Ускорение | Сокращение выделений |");
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
                $"| {imageSize} | Прежняя | {FormatNumber(result.Previous.ElapsedMilliseconds)} | {result.Previous.AllocatedBytes.ToString("N0", CultureInfo.InvariantCulture)} | 1,00× | 0,00% |");
            report.AppendLine(
                $"| {imageSize} | Блочная | {FormatNumber(result.Current.ElapsedMilliseconds)} | {result.Current.AllocatedBytes.ToString("N0", CultureInfo.InvariantCulture)} | {FormatNumber(speedup)}× | {FormatPercent(allocationReduction)} |");
        }

        return report.ToString();
    }

    private static string FormatMebibytes(int byteCount)
    {
        int mebibytes = byteCount / (1024 * 1024);

        return $"{mebibytes.ToString(CultureInfo.InvariantCulture)} МиБ";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("P2", CultureInfo.InvariantCulture);
    }

    private delegate void SpanConsumer(ReadOnlySpan<byte> content);

    private sealed record MeasurementResult(
        double ElapsedMilliseconds,
        long AllocatedBytes);

    private sealed record ComparisonResult(
        int ImageDataSize,
        MeasurementResult Previous,
        MeasurementResult Current);
}
