using System.Buffers.Binary;
using System.IO.Pipes;

using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.SingleInstance;

internal sealed class SingleInstanceActivationChannel
{
    private const byte ActivateCommand = 1;
    private const byte ActivationAcknowledgement = 1;
    private const int MaximumServerInstances = 1;
    private const int PipeBufferSize = 4;

    private static readonly byte[] ActivateCommandBuffer =
        [ActivateCommand];
    private static readonly byte[] ActivationAcknowledgementBuffer =
        [ActivationAcknowledgement];
    private readonly string _pipeName;
    private readonly ILogger<SingleInstanceCoordinator> _logger;
    private readonly int _pipeConnectAttemptCount;
    private readonly int _pipeConnectTimeoutMilliseconds;
    private readonly TimeSpan _pipeConnectRetryDelay;
    private readonly TimeSpan _clientProtocolTimeout;
    private readonly TimeSpan _listenerRetryDelay;

    public SingleInstanceActivationChannel(
        string pipeName,
        ILogger<SingleInstanceCoordinator> logger,
        SingleInstanceOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(options);

        _pipeName = pipeName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeConnectAttemptCount = options.PipeConnectAttemptCount;
        _pipeConnectTimeoutMilliseconds =
            options.PipeConnectTimeoutMilliseconds;
        _pipeConnectRetryDelay = TimeSpan.FromMilliseconds(
            options.PipeConnectRetryDelayMilliseconds);
        _clientProtocolTimeout = TimeSpan.FromMilliseconds(
            options.ClientProtocolTimeoutMilliseconds);
        _listenerRetryDelay = TimeSpan.FromMilliseconds(
            options.ListenerRetryDelayMilliseconds);
    }

    public async Task ListenAsync(
        Func<Task> activationHandler,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(activationHandler);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using NamedPipeServerStream pipe = CreateServerPipe();
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                await HandleClientAsync(
                        pipe,
                        activationHandler,
                        ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException
                or InvalidOperationException
                or UnauthorizedAccessException)
            {
                _logger.LogWarning(
                    ex,
                    "Atomic Art single-instance activation listener failed.");
                await Task.Delay(_listenerRetryDelay, ct)
                    .ConfigureAwait(false);
            }
        }
    }

    public bool TryNotifyExisting()
    {
        try
        {
            return NotifyExistingAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex) when (ex is IOException
            or TimeoutException
            or OperationCanceledException)
        {
            _logger.LogDebug(
                ex,
                "The existing Atomic Art process did not accept an activation request.");

            return false;
        }
    }

    private static async Task HandleClientAsync(
        NamedPipeServerStream pipe,
        Func<Task> activationHandler,
        CancellationToken ct)
    {
        byte[] processIdBytes = new byte[PipeBufferSize];
        BinaryPrimitives.WriteInt32LittleEndian(
            processIdBytes,
            Environment.ProcessId);
        await pipe.WriteAsync(processIdBytes, ct).ConfigureAwait(false);
        await pipe.FlushAsync(ct).ConfigureAwait(false);

        byte[] command = new byte[1];
        await pipe.ReadExactlyAsync(command, ct).ConfigureAwait(false);

        if (command[0] != ActivateCommand)
        {
            return;
        }

        await activationHandler().ConfigureAwait(false);
        await pipe
            .WriteAsync(ActivationAcknowledgementBuffer, ct)
            .ConfigureAwait(false);
        await pipe.FlushAsync(ct).ConfigureAwait(false);
    }

    private NamedPipeServerStream CreateServerPipe()
    {
        PipeOptions options = PipeOptions.Asynchronous
            | PipeOptions.CurrentUserOnly;

        return new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            MaximumServerInstances,
            PipeTransmissionMode.Byte,
            options);
    }

    private async Task<bool> NotifyExistingAsync()
    {
        using CancellationTokenSource timeoutCancellation = new(
            _clientProtocolTimeout);
        CancellationToken ct = timeoutCancellation.Token;

        for (int attempt = 0;
             attempt < _pipeConnectAttemptCount;
             attempt++)
        {
            await using NamedPipeClientStream pipe = new(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            if (!await TryConnectAsync(pipe, ct).ConfigureAwait(false))
            {
                continue;
            }

            byte[] processIdBytes = new byte[PipeBufferSize];
            await pipe
                .ReadExactlyAsync(processIdBytes, ct)
                .ConfigureAwait(false);
            int primaryProcessId =
                BinaryPrimitives.ReadInt32LittleEndian(processIdBytes);

            TryGrantForegroundPermission(primaryProcessId);
            await pipe
                .WriteAsync(ActivateCommandBuffer, ct)
                .ConfigureAwait(false);
            await pipe.FlushAsync(ct).ConfigureAwait(false);

            byte[] acknowledgement = new byte[1];
            await pipe
                .ReadExactlyAsync(acknowledgement, ct)
                .ConfigureAwait(false);

            return acknowledgement[0] == ActivationAcknowledgement;
        }

        return false;
    }

    private void TryGrantForegroundPermission(int primaryProcessId)
    {
        if (WindowsForegroundPermission.TryGrantToProcess(primaryProcessId))
        {
            return;
        }

        _logger.LogDebug(
            "Windows did not grant foreground activation permission to process {ProcessId}.",
            primaryProcessId);
    }

    private async Task<bool> TryConnectAsync(
        NamedPipeClientStream pipe,
        CancellationToken ct)
    {
        try
        {
            await pipe
                .ConnectAsync(_pipeConnectTimeoutMilliseconds, ct)
                .ConfigureAwait(false);

            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            await Task.Delay(_pipeConnectRetryDelay, ct)
                .ConfigureAwait(false);

            return false;
        }
    }
}
