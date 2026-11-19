using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

public sealed class LocalCodexAppServer(ILogger<LocalCodexAppServer> logger)
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(15);

    public async Task CheckConnectionAsync(LocalCodexOptions options, CancellationToken ct)
    {
        await RunQueryAsync(options, (_, _) => Task.FromResult(true), ct).ConfigureAwait(false);
    }

    public Task<(IReadOnlyList<LocalCodexTextModel> Models, string? DefaultModelId, string? ConfiguredEffort)> ListTextModelsAsync(
        LocalCodexOptions options, CancellationToken ct)
    {
        return RunQueryAsync(options, (protocol, token) => protocol.ListTextModelsAsync(token), ct);
    }

    private async Task<TResult> RunQueryAsync<TResult>(
        LocalCodexOptions options,
        Func<LocalCodexProtocol, CancellationToken, Task<TResult>> query,
        CancellationToken ct)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ConnectionTimeout);
        DirectoryInfo workspace = Directory.CreateTempSubdirectory("AtomicArt-Codex-");

        try
        {
            return await RunAsync(options, workspace.FullName, (protocol, _) => query(protocol, timeout.Token), timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new GenerationAttemptException(
                "Codex connection timed out.", GenerationProviderFailureErrorCodes.Timeout, false);
        }
        finally
        {
            DeleteWorkspace(workspace);
        }
    }

    internal async Task<TResult> RunAsync<TResult>(
        LocalCodexOptions options,
        string workspace,
        Func<LocalCodexProtocol, string, Task<TResult>> operation,
        CancellationToken ct)
    {
        (string executable, string home) = LocalCodexInstallation.Resolve(options);
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = workspace,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8
        };
        startInfo.Environment[LocalCodexInstallation.HomeVariable] = home;
        startInfo.Environment.Remove("OPENAI_API_KEY");
        startInfo.Environment.Remove("CODEX_API_KEY");
        startInfo.Environment.Remove("CODEX_ACCESS_TOKEN");
        foreach (string argument in new[]
        {
            "app-server", "--listen", "stdio://", "--disable", "shell_tool", "--disable", "unified_exec",
            "--disable", "plugins", "-c", "openai_base_url=\"https://chatgpt.com/backend-api/codex\""
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        Start(process);
        Task drain = process.StandardError.BaseStream.CopyToAsync(Stream.Null, ct);

        try
        {
            LocalCodexProtocol protocol = new(process.StandardOutput, process.StandardInput);
            await protocol.InitializeAsync(ct).ConfigureAwait(false);
            return await operation(protocol, home).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not GenerationAttemptException
            && exception is IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new GenerationAttemptException(
                "Codex App Server did not return a valid response.", GenerationClientFailureCodes.CodexUnavailable, false);
        }
        finally
        {
            await StopAsync(process, drain).ConfigureAwait(false);
        }
    }

    internal void DeleteWorkspace(DirectoryInfo workspace)
    {
        try
        {
            foreach (FileInfo file in workspace.EnumerateFiles())
            {
                file.Delete();
            }

            workspace.Delete();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Could not remove the temporary Codex workspace.");
        }
    }

    private static void Start(Process process)
    {
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new GenerationAttemptException(
                "Codex executable could not start.", GenerationClientFailureCodes.CodexUnavailable, false);
        }
    }

    private async Task StopAsync(Process process, Task drain)
    {
        try
        {
            process.StandardInput.Close();
            using CancellationTokenSource shutdown = new(TimeSpan.FromSeconds(2));
            await process.WaitForExitAsync(shutdown.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    using CancellationTokenSource forcedShutdown = new(TimeSpan.FromSeconds(2));
                    await process.WaitForExitAsync(forcedShutdown.Token).ConfigureAwait(false);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }
                catch (Exception failure) when (failure is Win32Exception or OperationCanceledException)
                {
                    logger.LogWarning("Could not stop the local Codex App Server.");
                }
            }
        }
        finally
        {
            process.StandardError.Dispose();
            try
            {
                await drain.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException or ObjectDisposedException)
            {
            }
        }
    }
}
