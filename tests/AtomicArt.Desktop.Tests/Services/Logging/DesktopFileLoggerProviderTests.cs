using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Logging;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Logging;

public sealed class DesktopFileLoggerProviderTests
{
    [Fact]
    public void Log_WithFileSizeLimit_RotatesAndRetainsConfiguredFileCount()
    {
        string rootDirectory = CreateLoggingRootPath("AtomicArtLoggingRotationTests");

        try
        {
            ExecuteLogging(rootDirectory, logger =>
            {
                string padding = new('x', 4096);

                for (int index = 0; index < 40; index++)
                {
                    logger.LogInformation(
                        "Rotation record {RecordIndex} {Padding}",
                        index,
                        padding);
                }
            });

            string[] logPaths = Directory.GetFiles(
                Path.Combine(rootDirectory, "Logs"),
                "atomicart-*.log");
            string retainedContents = string.Join(
                Environment.NewLine,
                logPaths.Select(File.ReadAllText));

            logPaths.Should().HaveCount(2);
            retainedContents.Should().Contain("Rotation record 39");
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public void Log_WithUnavailableLogDirectory_DoesNotThrow()
    {
        string rootPath = CreateLoggingRootPath("AtomicArtLoggingRoot");
        File.WriteAllText(rootPath, "not a directory");

        try
        {
            Action act = () =>
            {
                ExecuteLogging(rootPath, logger =>
                {
                    logger.LogError("This write cannot reach the file system.");
                });
            };

            act.Should().NotThrow();
        }
        finally
        {
            File.Delete(rootPath);
        }
    }

    [Fact]
    public void Log_WithException_WritesSanitizedMessageWithoutSensitiveDataOrSourcePath()
    {
        string rootDirectory = CreateLoggingRootPath("AtomicArtLoggingTests");
        string confidentialValue = "provider-key-confidential";
        string confidentialPath = Path.Combine(rootDirectory, "private-image.png");
        string apiKey = "desktop-api-key-secret-value";
        string bearerToken = "bearer-token-secret-value";
        string encodedData = "QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFB";

        try
        {
            ExecuteLogging(rootDirectory, logger =>
            {
                InvalidOperationException exception = new(
                    $"Failure included {confidentialValue}, path '{confidentialPath}', "
                        + $"URL https://private.example.invalid/resource, owner@example.com, "
                        + $"apiKey={apiKey}, Bearer {bearerToken}, data {encodedData}, "
                        + "phone +1 (555) 123-4567, SSN 123-45-6789 and IP 192.168.1.55 "
                        + "while opening the generation result.",
                    new IOException(
                        $"Inner read failed for '{confidentialPath}' with token={bearerToken}."));

                logger.LogError(
                    exception,
                    "Safe operation {OperationName} failed.\r\nForged log line",
                    "generation");
            });

            string logPath = Directory
                .GetFiles(Path.Combine(rootDirectory, "Logs"), "atomicart-*.log")
                .Should()
                .ContainSingle()
                .Which;
            string contents = File.ReadAllText(logPath);

            contents.Should().Contain("Safe operation generation failed.  Forged log line");
            contents.Should().Contain(typeof(InvalidOperationException).FullName);
            contents.Should().Contain("ExceptionMessage=Failure included");
            contents.Should().Contain("while opening the generation result.");
            contents.Should().Contain("ExceptionMessage=Inner read failed");
            contents.Should().Contain("[REDACTED SECRET]");
            contents.Should().Contain("[REDACTED CREDENTIAL]");
            contents.Should().Contain("[REDACTED DATA]");
            contents.Should().Contain("[REDACTED PATH]");
            contents.Should().Contain("[REDACTED URL]");
            contents.Should().Contain("[REDACTED EMAIL]");
            contents.Should().Contain("[REDACTED PHONE]");
            contents.Should().Contain("[REDACTED SSN]");
            contents.Should().Contain("[REDACTED IP]");
            contents.Should().NotContain(confidentialValue);
            contents.Should().NotContain(confidentialPath);
            contents.Should().NotContain(apiKey);
            contents.Should().NotContain(bearerToken);
            contents.Should().NotContain(encodedData);
            contents.Should().NotContain("private.example.invalid");
            contents.Should().NotContain("owner@example.com");
            contents.Should().NotContain("+1 (555) 123-4567");
            contents.Should().NotContain("123-45-6789");
            contents.Should().NotContain("192.168.1.55");
            contents.Should().NotContain("\r\nForged log line");
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    private static DesktopFileLoggerProvider CreateLoggerProvider(string rootPath)
    {
        IConfiguration configuration = CreateConfiguration();
        AtomicArtDataPathProvider pathProvider = new(rootPath);
        DesktopFileLoggingOptions options = new(configuration);

        return new DesktopFileLoggerProvider(pathProvider, options);
    }

    private static string CreateLoggingRootPath(string prefix)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"{prefix}-{Guid.NewGuid():N}");
    }

    private static IConfiguration CreateConfiguration()
    {
        Dictionary<string, string?> values = new(StringComparer.Ordinal)
        {
            ["Logging:File:MinimumLevel"] = nameof(LogLevel.Debug),
            ["Logging:File:MaxFileSizeBytes"] = "65536",
            ["Logging:File:RetainedFileCount"] = "2",
            ["Logging:File:RetentionDays"] = "14"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void ExecuteLogging(string rootPath, Action<ILogger> writeLog)
    {
        using DesktopFileLoggerProvider provider = CreateLoggerProvider(rootPath);
        ILogger logger = provider.CreateLogger("AtomicArt.Tests");

        writeLog(logger);
    }
}
