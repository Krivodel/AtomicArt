using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

public sealed class Dlss5NativeEngine : IDlss5NativeEngine, IDisposable
{
    public bool IsInitialized { get; private set; }

    private const uint VideoMagic = 0x3456_3544;
    private const uint SetupMagic = 0x3450_5553;
    private const uint FrameMagic = 0x314D_5246;
    private const uint OutputMagic = 0x3154_554F;
    private const uint EndMagic = 0x3144_4E45;
    private const int HeaderSize = 14 * sizeof(uint) + 4 * sizeof(float);
    private const int SetupResponseSize = 12 * sizeof(uint);
    private const int FrameHeaderSize = 4 * sizeof(uint) + sizeof(long);
    private const int OutputHeaderSize = 5 * sizeof(uint) + sizeof(long);
    private const int ProtocolMagicOffset = 0;
    private const int ProtocolSuccessOffset = sizeof(uint);
    private const int SetupResultOffset = 2 * sizeof(uint);
    private const int SetupRenderWidthOffset = 3 * sizeof(uint);
    private const int SetupRenderHeightOffset = 4 * sizeof(uint);
    private const int SetupOutputWidthOffset = 5 * sizeof(uint);
    private const int SetupOutputHeightOffset = 6 * sizeof(uint);
    private const int SetupModelPresetOffset = 11 * sizeof(uint);
    private const int OutputFrameIndexOffset = sizeof(uint);
    private const int OutputSuccessOffset = 2 * sizeof(uint);
    private const int OutputByteCountOffset = 3 * sizeof(uint);
    private const int OutputNgxResultOffset = 4 * sizeof(uint);
    private const int OutputPresentationTimestampOffset = 5 * sizeof(uint);
    private const int MinimumSourceDimension = 64;
    private const int StreamingDimensionThreshold = 128;
    private const int GracefulStopWaitMilliseconds = 2_000;
    private const int ForcedStopWaitMilliseconds = 500;
    private const int MaxConcurrentWorkerStarts = 1;
    private const int MaxPrewarmCandidates = 1;
    private const int ExclusiveGateCount = 1;
    private const int CleanupProgress = 20;
    private const int RuntimeVerificationProgress = 70;
    private const int InitializationCompleteProgress = 100;
    private const int MaximumDiagnosticLineCount = 60;
    private const int WorkerHookArmingLoopOffset = 0x3BD6;
    private const int WorkerLoopCountOffset = WorkerHookArmingLoopOffset + 1;
    private const byte WorkerLoopInstructionOpcode = 0xBB;
    private const byte WorkerFollowingInstructionOpcode = 0xBE;
    private const byte WorkerLoopReservedByte = 0;
    private const byte OriginalWorkerHookArmingLoopCount = 120;
    private const byte OptimizedWorkerHookArmingLoopCount = 20;
    private const string DynamicAddonFileName = "a-dlss5-event-dynamic.addon64";
    private const string DynamicParameterFileName = "dlss5-event-dynamic.params";
    private const uint InitialFrameIndex = 0;
    private const uint StreamingFrameCount = 0;
    private const uint OneShotFrameCount = 1;
    private const uint FrameReadyFlag = 1;
    private const uint FrameReservedValue = 0;
    private const uint NgxSuccessResult = 1;

    private bool HasDynamicParameterAddon => File.Exists(_paths.DynamicAddonPath);

    private readonly Dlss5ModulePaths _paths;
    private readonly ILogger<Dlss5NativeEngine> _logger;
    private readonly SemaphoreSlim _gate = new(ExclusiveGateCount, ExclusiveGateCount);
    private readonly SemaphoreSlim _startupGate = new(MaxConcurrentWorkerStarts, MaxConcurrentWorkerStarts);
    private readonly object _workerStateSync = new();
    private readonly Dictionary<Dlss5WorkerKey, PrewarmState> _prewarms = [];
    private Process? _worker;
    private Task<string>? _workerStderrTask;
    private Dlss5WorkerKey? _workerKey;
    private (int Width, int Height)? _workerDimensions;
    private uint _nextFrameIndex;
    private long _workerGeneration;
    private long _prewarmSequence;
    private Dlss5WorkerKey? _precomputedKey;
    private Dlss5NativeRenderResult? _precomputedResult;
    private bool _isDisposed;

    public Dlss5NativeEngine(
        Dlss5ModulePaths paths,
        ILogger<Dlss5NativeEngine> logger)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Stop()
    {
        CancelPrewarm();
        StopWorker("panel closed", graceful: true);
    }

    public async Task InitializeAsync(IProgress<int>? progress, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            int deletedWorkerDirectoryCount = await Task.Run(
                () => Dlss5WorkerDirectoryLifecycle.DeleteStaleWorkerDirectories(
                    _paths.RuntimeDirectory,
                    _logger),
                ct).ConfigureAwait(false);
            if (deletedWorkerDirectoryCount > 0)
            {
                _logger.LogInformation(
                    "Removed {WorkerDirectoryCount} stale DLSS 5 worker directories.",
                    deletedWorkerDirectoryCount);
            }

            if (IsInitialized)
            {
                progress?.Report(InitializationCompleteProgress);
                return;
            }

            progress?.Report(CleanupProgress);
            string[] requiredFiles =
            [
                _paths.WorkerPath,
                _paths.HostDxgiPath,
                _paths.AddonPath,
                _paths.NeuralRuntimePath,
                _paths.SuperResolutionPath
            ];
            string[] missingFiles = requiredFiles.Where(path => !File.Exists(path)).ToArray();
            if (missingFiles.Length > 0)
            {
                throw new InvalidDataException(
                    $"DLSS 5 v7 runtime is incomplete: {string.Join(", ", missingFiles)}");
            }

            progress?.Report(RuntimeVerificationProgress);
            IsInitialized = true;
            progress?.Report(InitializationCompleteProgress);
            _logger.LogInformation(
                "DLSS 5 v{Version} runtime is ready at {RuntimeDirectory}; backend {Backend}.",
                Dlss5FeatureDefinition.Version,
                _paths.RuntimeDirectory,
                File.Exists(_paths.DynamicAddonPath)
                    ? "isolated v7 ReShade stream worker with in-process parameter updates"
                    : "isolated v7 ReShade stream worker with exact reference composition and restart fallback");
            if (!File.Exists(_paths.DynamicAddonPath))
            {
                _logger.LogWarning(
                    "DLSS 5 dynamic parameter add-on is missing at {DynamicAddonPath}; parameter changes will use the slower worker restart fallback.",
                    _paths.DynamicAddonPath);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "DLSS 5 v{Version} worker runtime initialization failed.", Dlss5FeatureDefinition.Version);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Dlss5NativeRenderResult> RenderAsync(
        SKBitmap source,
        Dlss5RenderSettings settings,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        settings.Validate();
        settings = settings.NormalizeForNative();
        ct.ThrowIfCancellationRequested();

        if ((source.Width >= StreamingDimensionThreshold)
            && (source.Height >= StreamingDimensionThreshold))
        {
            Dlss5WorkerKey requestedKey = CreateWorkerKey(source.Width, source.Height, settings);
            // The dynamic v7 add-on applies intensity changes to the live
            // worker before evaluate. Do not wait for a speculative exact-key
            // prewarm in that case: waiting would turn an in-place update back
            // into the same cold-start latency that the streaming path avoids.
            if (!HasReusableDynamicWorker(requestedKey))
            {
                await AwaitMatchingPrewarmAsync(requestedKey, ct).ConfigureAwait(false);
            }
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("DLSS 5 is not initialized.");
            }

            Dlss5PixelBuffers buffers = Dlss5PixelBuffers.Create(source);
            bool streaming = (source.Width >= StreamingDimensionThreshold) && (source.Height >= StreamingDimensionThreshold);
            Stopwatch startupStopwatch = Stopwatch.StartNew();
            (Process worker, bool reused) = await EnsureWorkerAsync(
                source.Width,
                source.Height,
                settings,
                streaming,
                ct).ConfigureAwait(false);
            TimeSpan startupDuration = startupStopwatch.Elapsed;

            Dlss5NativeRenderResult? precomputedResult = TakePrecomputedResult(
                new Dlss5WorkerKey(source.Width, source.Height, settings));
            if (precomputedResult is not null)
            {
                _nextFrameIndex = checked(_nextFrameIndex + 1);
                _logger.LogDebug(
                    "DLSS 5 v{Version} used precomputed frame for {Width}x{Height}; worker reused {WorkerReused}, setup {SetupMilliseconds} ms, evaluate {EvaluateMilliseconds} ms.",
                    Dlss5FeatureDefinition.Version,
                    source.Width,
                    source.Height,
                    reused,
                    startupDuration.TotalMilliseconds,
                    precomputedResult.EvaluateDuration.TotalMilliseconds);
                return precomputedResult;
            }

            try
            {
                ct.ThrowIfCancellationRequested();
                WriteDynamicParameters(worker, settings);

                uint frameIndex = _nextFrameIndex;
                Dlss5NativeRenderResult frameResult = await RenderFrameAsync(
                    worker,
                    buffers,
                    frameIndex,
                    CancellationToken.None,
                    abortWorkerOnCancellation: false).ConfigureAwait(false);
                _nextFrameIndex = checked(frameIndex + 1);
                if (ct.IsCancellationRequested)
                {
                    frameResult.Bitmap.Dispose();
                    throw new OperationCanceledException(ct);
                }
                TimeSpan uploadDuration = frameResult.UploadDuration;
                TimeSpan evaluateDuration = frameResult.EvaluateDuration;
                TimeSpan downloadDuration = frameResult.DownloadDuration;

                if (!streaming)
                {
                    await CompleteOneShotWorkerAsync(worker, ct).ConfigureAwait(false);
                }

                _logger.LogDebug(
                    "DLSS 5 v{Version} render completed for {Width}x{Height}; style {Style}, general {GeneralIntensity}, local structure {LocalStructureIntensity}, skin {SkinStructureStrength}, local tone {LocalToneStrength}, effective auto mask {AutomaticMask}; worker reused {WorkerReused}, worker ensure {WorkerEnsureMilliseconds} ms, upload {UploadMilliseconds} ms, evaluate {EvaluateMilliseconds} ms, download {DownloadMilliseconds} ms, frame {FrameIndex}.",
                    Dlss5FeatureDefinition.Version,
                    source.Width,
                    source.Height,
                    settings.Style,
                    settings.GeneralIntensity,
                    settings.LocalStructureIntensity,
                    settings.SkinStructureStrength,
                    settings.LocalToneStrength,
                    Dlss5NativeEngine.GetEffectiveAutomaticMask(settings),
                    reused,
                    startupDuration.TotalMilliseconds,
                    uploadDuration.TotalMilliseconds,
                    evaluateDuration.TotalMilliseconds,
                    downloadDuration.TotalMilliseconds,
                    frameIndex);

                return new Dlss5NativeRenderResult(
                    frameResult.Bitmap,
                    uploadDuration,
                    evaluateDuration,
                    downloadDuration);
            }
            catch (Exception exception) when ((streaming) && (Dlss5NativeEngine.IsStreamingReadbackFailure(exception)) && (!ct.IsCancellationRequested))
            {
                StopWorker("streaming readback failed; retrying one-shot worker");
                _logger.LogWarning(
                    exception,
                    "DLSS 5 streaming readback failed for {Width}x{Height}; retrying the current frame with a one-shot worker.",
                    source.Width,
                    source.Height);
                return await RenderOneShotFallbackAsync(source, settings, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The frame was allowed to drain so the streaming protocol and
                // the warm worker remain reusable for the newest revision.
                throw;
            }
            catch (OperationCanceledException)
            {
                StopWorker("render canceled");
                throw;
            }
            catch (Exception)
            {
                StopWorker("render failure");
                throw;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            StopWorker("render canceled");
            throw;
        }
        catch (Exception exception)
        {
            StopWorker("render failure");
            _logger.LogError(
                exception,
                "DLSS 5 v{Version} render failed for {Width}x{Height} with style {Style}, general {GeneralIntensity}, local structure {LocalStructureIntensity}, skin {SkinStructureStrength}, local tone {LocalToneStrength}. Runtime {RuntimeDirectory}; worker {WorkerPath}; addon {AddonPath}; neural runtime {NeuralRuntimePath}; super-resolution runtime {SuperResolutionPath}; ReShade log {ReShadeLogPath}.",
                Dlss5FeatureDefinition.Version,
                source.Width,
                source.Height,
                settings.Style,
                settings.GeneralIntensity,
                settings.LocalStructureIntensity,
                settings.SkinStructureStrength,
                settings.LocalToneStrength,
                _paths.RuntimeDirectory,
                _paths.WorkerPath,
                _paths.AddonPath,
                _paths.NeuralRuntimePath,
                _paths.SuperResolutionPath,
                _paths.ReShadeLogPath);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PrepareAsync(
        int width,
        int height,
        Dlss5RenderSettings settings,
        SKBitmap? source,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, MinimumSourceDimension);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, MinimumSourceDimension);
        settings.Validate();
        settings = settings.NormalizeForNative();
        ct.ThrowIfCancellationRequested();

        if (!IsInitialized)
        {
            return;
        }

        bool streaming = (width >= StreamingDimensionThreshold) && (height >= StreamingDimensionThreshold);
        if (!streaming)
        {
            return;
        }

        Dlss5WorkerKey key = CreateWorkerKey(width, height, settings);
        TaskCompletionSource<object?> completion;
        CancellationTokenSource cancellation;
        bool start;
        List<PrewarmState> retiredStates = [];
        lock (_workerStateSync)
        {
            if (IsActiveWorkerMatch(key))
            {
                return;
            }

            if (_prewarms.TryGetValue(key, out PrewarmState? existing))
            {
                completion = existing.Completion;
                cancellation = existing.Cancellation;
                start = false;
            }
            else
            {
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                completion = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _prewarms.Add(
                    key,
                    new PrewarmState
                    {
                        Sequence = ++_prewarmSequence,
                        Cancellation = cancellation,
                        Completion = completion
                    });
                while (_prewarms.Count > MaxPrewarmCandidates)
                {
                    (Dlss5WorkerKey retiredKey, PrewarmState retiredState) = _prewarms
                        .OrderBy(pair => pair.Value.Sequence)
                        .First();
                    _prewarms.Remove(retiredKey);
                    retiredStates.Add(retiredState);
                }
                start = true;
            }
        }

        foreach (PrewarmState retiredState in retiredStates)
        {
            retiredState.Cancellation.Cancel();
            retiredState.Completion.TrySetCanceled();
        }

        if (start)
        {
            _ = RunPrewarmAsync(key, completion, cancellation, source, width, height, settings);
        }

        await completion.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        CancelPrewarm();
        StopWorker("engine disposed", graceful: true);
    }

    internal static string CreateDynamicParameterContent(Dlss5RenderSettings settings)
    {
        Dlss5NativeParameterMapping mapped = Dlss5NativeParameterMapping.Create(settings);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{mapped.Style} {mapped.GeneralIntensity:0.######} 1 {mapped.LocalToneStrength:0.######} {mapped.LocalStructureStrength:0.######} {mapped.SkinStructureStrength:0.######}\n");
    }

    internal static bool TryOptimizeWorkerStartup(string workerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerPath);

        byte[] worker = File.ReadAllBytes(workerPath);
        ReadOnlySpan<byte> expectedLoopBytes =
        [
            OriginalWorkerHookArmingLoopCount,
            WorkerLoopReservedByte,
            WorkerLoopReservedByte,
            WorkerLoopReservedByte,
            WorkerFollowingInstructionOpcode
        ];
        if ((worker.Length < WorkerLoopCountOffset + expectedLoopBytes.Length)
            || (worker[WorkerHookArmingLoopOffset] != WorkerLoopInstructionOpcode)
            || (!worker.AsSpan(WorkerLoopCountOffset, expectedLoopBytes.Length)
                .SequenceEqual(expectedLoopBytes)))
        {
            return false;
        }

        worker[WorkerLoopCountOffset] = OptimizedWorkerHookArmingLoopCount;
        File.WriteAllBytes(workerPath, worker);
        return true;
    }

    internal static string CreateWorkerAddonPath(string workerDirectory, string addonDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(addonDirectory);

        return Path.GetRelativePath(
            Path.GetFullPath(workerDirectory),
            Path.GetFullPath(addonDirectory));
    }

    internal static string CreateReShadeProfile(Dlss5RenderSettings settings)
    {
        return Dlss5NativeEngine.CreateReShadeProfile(settings, "..\\dlssnr");
    }

    internal static string CreateReShadeProfile(Dlss5RenderSettings settings, string addonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonPath);
        Dlss5NativeParameterMapping mapped = Dlss5NativeParameterMapping.Create(settings);
        // Keep the canonical v7 profile contract. Hook mode 2 is the value
        // written by the reference v7 launcher; using it here avoids a hidden
        // backend-policy difference between Atomic Art and the reference.
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[ADDON]\r\nAddonPath={addonPath}\r\n\r\n[RenoDX.DLSS5]\r\nEnableHooks=2\r\nNREnableUpscaling=0\r\nNRPreset={Dlss5NativeParameterMapping.FixedNrPreset}\r\nNRStyle={mapped.Style}\r\nNRAutoMask={mapped.AutomaticMask}\r\nNRUICorrection={Dlss5NativeParameterMapping.FixedUiCorrection}\r\nNRIntensity={mapped.GeneralIntensity:0.0000}\r\nNRLocalTone={mapped.LocalToneStrength:0.0000}\r\nNRLocalStructure={mapped.LocalStructureStrength:0.0000}\r\nNRSkinStructure={mapped.SkinStructureStrength:0.0000}\r\n");
    }

    internal static byte[] CreateVideoHeader(
        int width,
        int height,
        Dlss5RenderSettings settings,
        bool streaming)
    {
        Dlss5NativeParameterMapping mapped = Dlss5NativeParameterMapping.Create(settings);
        byte[] header = new byte[HeaderSize];
        int offset = 0;
        Dlss5NativeEngine.WriteUInt32(header, ref offset, VideoMagic);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, checked((uint)width));
        Dlss5NativeEngine.WriteUInt32(header, ref offset, checked((uint)height));
        Dlss5NativeEngine.WriteUInt32(header, ref offset, checked((uint)width));
        Dlss5NativeEngine.WriteUInt32(header, ref offset, checked((uint)height));
        Dlss5NativeEngine.WriteUInt32(header, ref offset, Dlss5NativeParameterMapping.NoWarmupFrames);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, streaming ? StreamingFrameCount : OneShotFrameCount);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, Dlss5NativeParameterMapping.DlaaPerformanceQuality);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, Dlss5NativeParameterMapping.FixedDlssModelPreset);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, Dlss5NativeParameterMapping.FixedProfile);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, Dlss5NativeParameterMapping.FixedNrPreset);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, (uint)mapped.Style);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, (uint)mapped.AutomaticMask);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, Dlss5NativeParameterMapping.FixedUiCorrection);
        Dlss5NativeEngine.WriteSingle(header, ref offset, mapped.GeneralIntensity);
        Dlss5NativeEngine.WriteSingle(header, ref offset, mapped.LocalToneStrength);
        Dlss5NativeEngine.WriteSingle(header, ref offset, mapped.LocalStructureStrength);
        Dlss5NativeEngine.WriteSingle(header, ref offset, mapped.SkinStructureStrength);
        return header;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or ObjectDisposedException
            or System.ComponentModel.Win32Exception)
        {
        }
    }

    private static bool IsProcessRunning(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or ObjectDisposedException
            or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static bool IsStreamingReadbackFailure(Exception exception)
    {
        return exception is EndOfStreamException or IOException;
    }

    private static void TryCloseWorker(Process worker, uint frameCount)
    {
        try
        {
            if (worker.HasExited)
            {
                return;
            }

            worker.StandardInput.BaseStream.Write(Dlss5NativeEngine.CreateEndHeader(frameCount));
            worker.StandardInput.BaseStream.Flush();
            worker.StandardInput.Close();
            worker.WaitForExit(GracefulStopWaitMilliseconds);
        }
        catch (Exception exception) when (
            exception is IOException
            or InvalidOperationException
            or ObjectDisposedException
            or System.ComponentModel.Win32Exception)
        {
        }
    }

    private static uint GetEffectiveAutomaticMask(Dlss5RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return (uint)Dlss5NativeParameterMapping.Create(settings).AutomaticMask;
    }

    private static byte[] CreateFrameHeader(uint frameIndex)
    {
        byte[] header = new byte[FrameHeaderSize];
        int offset = 0;
        Dlss5NativeEngine.WriteUInt32(header, ref offset, FrameMagic);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, frameIndex);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, FrameReadyFlag);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, FrameReservedValue);
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(offset), frameIndex);
        return header;
    }

    private static byte[] CreateEndHeader(uint frameCount)
    {
        byte[] header = new byte[FrameHeaderSize];
        int offset = 0;
        Dlss5NativeEngine.WriteUInt32(header, ref offset, EndMagic);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, frameCount);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, FrameReservedValue);
        Dlss5NativeEngine.WriteUInt32(header, ref offset, FrameReservedValue);
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(offset), InitialFrameIndex);
        return header;
    }

    private static void ValidateSetup(byte[] setup, int width, int height)
    {
        uint setupMagic = Dlss5NativeEngine.ReadUInt32(setup, ProtocolMagicOffset);
        uint setupOk = Dlss5NativeEngine.ReadUInt32(setup, ProtocolSuccessOffset);
        uint setupResult = Dlss5NativeEngine.ReadUInt32(setup, SetupResultOffset);
        uint renderWidth = Dlss5NativeEngine.ReadUInt32(setup, SetupRenderWidthOffset);
        uint renderHeight = Dlss5NativeEngine.ReadUInt32(setup, SetupRenderHeightOffset);
        uint outputWidth = Dlss5NativeEngine.ReadUInt32(setup, SetupOutputWidthOffset);
        uint outputHeight = Dlss5NativeEngine.ReadUInt32(setup, SetupOutputHeightOffset);
        uint appliedModelPreset = Dlss5NativeEngine.ReadUInt32(setup, SetupModelPresetOffset);

        if (setupMagic != SetupMagic)
        {
            throw new InvalidDataException("The installed DLSS 5 v7 worker returned an unsupported setup protocol.");
        }

        if (setupOk == 0)
        {
            throw new InvalidOperationException(
                $"DLSS 5 setup failed for {width}x{height} (NGX 0x{setupResult:X8}).");
        }

        if ((outputWidth != width) || (outputHeight != height) || (renderWidth != width) || (renderHeight != height))
        {
            throw new InvalidDataException(
                $"DLSS 5 worker negotiated unexpected dimensions: render {renderWidth}x{renderHeight}, output {outputWidth}x{outputHeight}; requested {width}x{height}.");
        }

        if (appliedModelPreset != Dlss5NativeParameterMapping.FixedDlssModelPreset)
        {
            throw new InvalidDataException(
                $"DLSS 5 worker applied model preset {appliedModelPreset} instead of Default (0).");
        }
    }

    private static void ValidateOutputHeader(
        byte[] header,
        int expectedByteCount,
        uint expectedFrameIndex)
    {
        uint magic = Dlss5NativeEngine.ReadUInt32(header, ProtocolMagicOffset);
        uint index = Dlss5NativeEngine.ReadUInt32(header, OutputFrameIndexOffset);
        uint ok = Dlss5NativeEngine.ReadUInt32(header, OutputSuccessOffset);
        uint byteCount = Dlss5NativeEngine.ReadUInt32(header, OutputByteCountOffset);
        uint ngxResult = Dlss5NativeEngine.ReadUInt32(header, OutputNgxResultOffset);
        long presentationTimestamp = BinaryPrimitives.ReadInt64LittleEndian(
            header.AsSpan(OutputPresentationTimestampOffset));

        if ((magic != OutputMagic) || (index != expectedFrameIndex) || (ok == 0)
            || (byteCount != expectedByteCount) || (presentationTimestamp != expectedFrameIndex))
        {
            throw new InvalidDataException(
                $"DLSS 5 worker returned an invalid frame response (magic 0x{magic:X8}, index {index}, ok {ok}, bytes {byteCount}, pts {presentationTimestamp}).");
        }

        if (ngxResult != NgxSuccessResult)
        {
            throw new InvalidOperationException(
                $"DLSS 5 feature-18 evaluation failed (NGX 0x{ngxResult:X8}).");
        }
    }

    private static async Task<string> ReadStderrAsync(StreamReader reader)
    {
        try
        {
            string diagnostics = await reader.ReadToEndAsync().ConfigureAwait(false);
            const int MaximumCharacters = 32_768;
            return diagnostics.Length <= MaximumCharacters
                ? diagnostics
                : diagnostics[^MaximumCharacters..];
        }
        catch (ObjectDisposedException)
        {
            return string.Empty;
        }
    }

    private static async Task WriteAsync(Stream stream, byte[] data, CancellationToken ct)
    {
        await stream.WriteAsync(data.AsMemory(), ct).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken ct)
    {
        byte[] result = GC.AllocateUninitializedArray<byte>(length);
        await Dlss5NativeEngine.ReadExactIntoAsync(stream, result, ct).ConfigureAwait(false);
        return result;
    }

    private static async Task ReadExactIntoAsync(Stream stream, byte[] target, CancellationToken ct)
    {
        int offset = 0;
        while (offset < target.Length)
        {
            int read = await stream.ReadAsync(target.AsMemory(offset), ct).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    $"DLSS 5 worker stopped after {offset} of {target.Length} bytes.");
            }

            offset += read;
        }
    }

    private static uint ReadUInt32(byte[] buffer, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, sizeof(uint)));
    }

    private static void WriteUInt32(byte[] buffer, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, sizeof(uint)), value);
        offset += sizeof(uint);
    }

    private static void WriteSingle(byte[] buffer, ref int offset, float value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(offset, sizeof(float)),
            BitConverter.SingleToInt32Bits(value));
        offset += sizeof(float);
    }

    private async Task RunPrewarmAsync(
        Dlss5WorkerKey key,
        TaskCompletionSource<object?> completion,
        CancellationTokenSource cancellation,
        SKBitmap? source,
        int width,
        int height,
        Dlss5RenderSettings settings)
    {
        Process? preparedWorker = null;
        Task<string>? preparedStderrTask = null;
        Dlss5NativeRenderResult? precomputedResult = null;
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            await StopMismatchedWorkerAsync(key, cancellation.Token).ConfigureAwait(false);
            (preparedWorker, preparedStderrTask) = await StartConfiguredWorkerAsync(
                width,
                height,
                settings,
                streaming: true,
                "prewarm",
                cancellation.Token).ConfigureAwait(false);

            if (source is not null)
            {
                Dlss5PixelBuffers buffers = Dlss5PixelBuffers.Create(source);
                precomputedResult = await RenderFrameAsync(
                    preparedWorker,
                    buffers,
                    0,
                    cancellation.Token).ConfigureAwait(false);
            }

            // The native worker warms its feature on the first real frame.
            // Sending a synthetic frame here adds a full-size upload,
            // evaluation and readback before the source can be processed.

            await _gate.WaitAsync(cancellation.Token).ConfigureAwait(false);
            try
            {
                Process? oldWorker;
                Task<string>? oldStderrTask;
                uint oldFrameCount;
                bool accepted;
                lock (_workerStateSync)
                {
                    accepted = (!cancellation.IsCancellationRequested)
                        && (!_isDisposed);
                    if (accepted)
                    {
                        oldWorker = _worker;
                        oldStderrTask = _workerStderrTask;
                        oldFrameCount = _nextFrameIndex;
                        _worker = preparedWorker;
                        _workerStderrTask = preparedStderrTask;
                        _workerKey = key;
                        _workerDimensions = (width, height);
                        _nextFrameIndex = InitialFrameIndex;
                        _workerGeneration++;
                        _precomputedKey = new Dlss5WorkerKey(width, height, settings);
                        _precomputedResult?.Bitmap.Dispose();
                        _precomputedResult = precomputedResult;
                        precomputedResult = null;
                        preparedWorker = null;
                        preparedStderrTask = null;
                    }
                    else
                    {
                        oldWorker = null;
                        oldStderrTask = null;
                        oldFrameCount = 0;
                    }
                }

                if (accepted)
                {
                    StopDetachedWorker(
                        oldWorker,
                        oldStderrTask,
                        oldFrameCount,
                        "replaced by prewarmed worker",
                        graceful: false);
                    _logger.LogDebug(
                        "DLSS 5 worker prewarm completed and promoted for {Width}x{Height}, settings {Settings}; total prewarm {PrewarmMilliseconds} ms.",
                        width,
                        height,
                        settings,
                        stopwatch.Elapsed.TotalMilliseconds);
                }
            }
            finally
            {
                _gate.Release();
            }

            completion.TrySetResult(null);
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "DLSS 5 worker prewarm failed for {Width}x{Height}, settings {Settings}.",
                width,
                height,
                settings);
            completion.TrySetException(exception);
        }
        finally
        {
            if (preparedWorker is not null)
            {
                StopDetachedWorker(
                    preparedWorker,
                    preparedStderrTask,
                    0,
                    "discarded prewarm worker",
                    graceful: false);
            }

            precomputedResult?.Bitmap.Dispose();

            lock (_workerStateSync)
            {
                if ((_prewarms.TryGetValue(key, out PrewarmState? state))
                    && (ReferenceEquals(state.Completion, completion)))
                {
                    _prewarms.Remove(key);
                }
            }

            cancellation.Dispose();
        }
    }

    private async Task<Dlss5NativeRenderResult> RenderOneShotFallbackAsync(
        SKBitmap source,
        Dlss5RenderSettings settings,
        CancellationToken ct)
    {
        Dlss5PixelBuffers buffers = Dlss5PixelBuffers.Create(source);
        Stopwatch startupStopwatch = Stopwatch.StartNew();
        (Process worker, bool reused) = await EnsureWorkerAsync(
            source.Width,
            source.Height,
            settings,
            streaming: false,
            ct).ConfigureAwait(false);

        try
        {
            using CancellationTokenRegistration cancellation = ct.Register(
                static state =>
                {
                    if (state is Process process)
                    {
                        Dlss5NativeEngine.TryKill(process);
                    }
                },
                worker);

            Stopwatch stopwatch = Stopwatch.StartNew();
            WriteDynamicParameters(worker, settings);
            uint frameIndex = _nextFrameIndex;
            await Dlss5NativeEngine.WriteAsync(worker.StandardInput.BaseStream, Dlss5NativeEngine.CreateFrameHeader(frameIndex), ct)
                .ConfigureAwait(false);
            await Dlss5NativeEngine.WriteAsync(worker.StandardInput.BaseStream, buffers.InputRgba, ct).ConfigureAwait(false);
            await Dlss5NativeEngine.WriteAsync(worker.StandardInput.BaseStream, buffers.Motion, ct).ConfigureAwait(false);
            await worker.StandardInput.BaseStream.FlushAsync(ct).ConfigureAwait(false);
            TimeSpan uploadDuration = stopwatch.Elapsed;

            stopwatch.Restart();
            byte[] outputHeader = await Dlss5NativeEngine.ReadExactAsync(worker.StandardOutput.BaseStream, OutputHeaderSize, ct)
                .ConfigureAwait(false);
            Dlss5NativeEngine.ValidateOutputHeader(outputHeader, buffers.OutputRgba.Length, frameIndex);
            TimeSpan evaluateDuration = stopwatch.Elapsed;

            stopwatch.Restart();
            await Dlss5NativeEngine.ReadExactIntoAsync(worker.StandardOutput.BaseStream, buffers.OutputRgba, ct)
                .ConfigureAwait(false);
            TimeSpan downloadDuration = stopwatch.Elapsed;
            buffers.RestoreSourceAlpha();
            _nextFrameIndex = checked(frameIndex + 1);
            await CompleteOneShotWorkerAsync(worker, ct).ConfigureAwait(false);

            _logger.LogDebug(
                "DLSS 5 one-shot fallback completed for {Width}x{Height}; worker reused {WorkerReused}, worker ensure {WorkerEnsureMilliseconds} ms, upload {UploadMilliseconds} ms, evaluate {EvaluateMilliseconds} ms, download {DownloadMilliseconds} ms.",
                source.Width,
                source.Height,
                reused,
                startupStopwatch.Elapsed.TotalMilliseconds,
                uploadDuration.TotalMilliseconds,
                evaluateDuration.TotalMilliseconds,
                downloadDuration.TotalMilliseconds);

            return new Dlss5NativeRenderResult(
                buffers.CreateBitmap(),
                uploadDuration,
                evaluateDuration,
                downloadDuration);
        }
        catch (OperationCanceledException)
        {
            StopWorker("one-shot fallback canceled");
            throw;
        }
        catch (Exception)
        {
            StopWorker("one-shot fallback failed");
            throw;
        }
    }

    private Process StartWorker(string workerDirectory)
    {
        string workerPath = Path.Combine(workerDirectory, Path.GetFileName(_paths.WorkerPath));
        ProcessStartInfo startInfo = new()
        {
            FileName = workerPath,
            WorkingDirectory = workerDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("--video");

        Process worker = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!worker.Start())
        {
            worker.Dispose();
            throw new InvalidOperationException("DLSS 5 worker could not be started.");
        }

        _logger.LogDebug("Started hidden DLSS 5 worker process {ProcessId} from {WorkerPath}.", worker.Id, workerPath);
        return worker;
    }

    private bool IsActiveWorkerMatch(Dlss5WorkerKey key)
    {
        return (_workerKey is { } activeKey)
            && ((HasDynamicParameterAddon) || (activeKey == key))
            && (_workerDimensions is { } dimensions)
            && (dimensions.Width == key.Width)
            && (dimensions.Height == key.Height)
            && (_worker is not null)
            && (Dlss5NativeEngine.IsProcessRunning(_worker));
    }

    private bool HasReusableDynamicWorker(Dlss5WorkerKey key)
    {
        if (!HasDynamicParameterAddon)
        {
            return false;
        }

        lock (_workerStateSync)
        {
            return IsActiveWorkerMatch(key);
        }
    }

    private Dlss5WorkerKey CreateWorkerKey(
        int width,
        int height,
        Dlss5RenderSettings settings)
    {
        if (!HasDynamicParameterAddon)
        {
            return new Dlss5WorkerKey(width, height, settings);
        }

        // Dynamic parameters do not require a new worker. Use the same key
        // for preparation and caching so returning to a source size reuses
        // its GPU resources even after the settings have changed.
        return new Dlss5WorkerKey(width, height, Dlss5RenderSettings.Default);
    }

    private async Task AwaitMatchingPrewarmAsync(
        Dlss5WorkerKey key,
        CancellationToken ct)
    {
        TaskCompletionSource<object?>? completion;
        lock (_workerStateSync)
        {
            completion = _prewarms.TryGetValue(key, out PrewarmState? state)
                ? state.Completion
                : null;
        }

        if (completion is null)
        {
            return;
        }

        try
        {
            await completion.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // A newer slider value canceled this candidate. Direct setup below
            // creates the latest worker.
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "DLSS 5 prewarm candidate was unavailable for {Width}x{Height}, settings {Settings}; falling back to direct setup.",
                key.Width,
                key.Height,
                key.Settings);
        }
    }

    private Dlss5NativeRenderResult? TakePrecomputedResult(Dlss5WorkerKey key)
    {
        lock (_workerStateSync)
        {
            if ((_precomputedKey != key) || (_precomputedResult is null))
            {
                return null;
            }

            Dlss5NativeRenderResult result = _precomputedResult;
            _precomputedKey = null;
            _precomputedResult = null;
            return result;
        }
    }

    private async Task<
        (Process Worker, Task<string> StderrTask)> StartConfiguredWorkerAsync(
        int width,
        int height,
        Dlss5RenderSettings settings,
        bool streaming,
        string reason,
        CancellationToken ct)
    {
        await _startupGate.WaitAsync(ct).ConfigureAwait(false);
        Process? worker = null;
        Task<string>? stderrTask = null;
        Stopwatch setupStopwatch = Stopwatch.StartNew();
        string? workerDirectory = null;
        bool handedOffWorker = false;
        try
        {
            workerDirectory = CreateWorkerDirectory();
            string workerRoot = Path.GetDirectoryName(workerDirectory)
                ?? throw new InvalidOperationException(
                    $"DLSS 5 worker directory '{workerDirectory}' has no worker root.");
            WriteReShadeProfile(
                settings,
                Path.Combine(workerDirectory, "ReShade.ini"),
                Path.Combine(workerRoot, "dlssnr"));
            WriteDynamicParameters(workerDirectory, settings);
            worker = StartWorker(workerDirectory);
            Dlss5WorkerDirectoryLifecycle.MarkOwnedByProcess(workerRoot, worker);
            stderrTask = Dlss5NativeEngine.ReadStderrAsync(worker.StandardError);
            using CancellationTokenRegistration cancellation =
                ct.Register(static state =>
                {
                    if (state is Process process)
                    {
                        Dlss5NativeEngine.TryKill(process);
                    }
                }, worker);

            await Dlss5NativeEngine.WriteAsync(
                worker.StandardInput.BaseStream,
                Dlss5NativeEngine.CreateVideoHeader(width, height, settings, streaming),
                ct).ConfigureAwait(false);
            await worker.StandardInput.BaseStream.FlushAsync(ct).ConfigureAwait(false);
            byte[] setup = await Dlss5NativeEngine.ReadExactAsync(
                    worker.StandardOutput.BaseStream,
                    SetupResponseSize,
                    ct)
                .ConfigureAwait(false);
            Dlss5NativeEngine.ValidateSetup(setup, width, height);
            _logger.LogInformation(
                "Started DLSS 5 worker process {ProcessId} for {Width}x{Height}; setup reason {SetupReason}.",
                worker.Id,
                width,
                height,
                reason);
            _logger.LogDebug(
                "DLSS 5 worker process {ProcessId} setup completed in {SetupMilliseconds} ms.",
                worker.Id,
                setupStopwatch.Elapsed.TotalMilliseconds);
            handedOffWorker = true;
            return (worker, stderrTask);
        }
        catch (Exception exception)
        {
            if (worker is null)
            {
                throw;
            }

            Dlss5NativeEngine.TryKill(worker);
            string diagnostics = stderrTask is null
                ? string.Empty
                : await stderrTask.ConfigureAwait(false);
            int exitCode = Dlss5NativeEngine.IsProcessRunning(worker) ? 0 : worker.ExitCode;
            worker.Dispose();
            if ((exitCode != 0) && (!string.IsNullOrWhiteSpace(diagnostics)))
            {
                throw new InvalidOperationException(
                    CreateWorkerFailureMessage(
                        exitCode,
                        diagnostics,
                        workerDirectory is null ? null : Path.Combine(workerDirectory, "ReShade.log")),
                    exception);
            }

            throw;
        }
        finally
        {
            if ((!handedOffWorker) && (workerDirectory is not null))
            {
                TryDeleteWorkerDirectory(workerDirectory);
            }
            _startupGate.Release();
        }
    }

    private async Task<Dlss5NativeRenderResult> RenderFrameAsync(
        Process worker,
        Dlss5PixelBuffers buffers,
        uint frameIndex,
        CancellationToken ct,
        bool abortWorkerOnCancellation = true)
    {
        using CancellationTokenRegistration cancellation = abortWorkerOnCancellation
            ? ct.Register(static state =>
            {
                if (state is Process process)
                {
                    Dlss5NativeEngine.TryKill(process);
                }
            }, worker)
            : default;

        Stopwatch stopwatch = Stopwatch.StartNew();
        await Dlss5NativeEngine.WriteAsync(worker.StandardInput.BaseStream, Dlss5NativeEngine.CreateFrameHeader(frameIndex), ct)
            .ConfigureAwait(false);
        await Dlss5NativeEngine.WriteAsync(worker.StandardInput.BaseStream, buffers.InputRgba, ct)
            .ConfigureAwait(false);
        await Dlss5NativeEngine.WriteAsync(worker.StandardInput.BaseStream, buffers.Motion, ct)
            .ConfigureAwait(false);
        await worker.StandardInput.BaseStream.FlushAsync(ct).ConfigureAwait(false);
        TimeSpan uploadDuration = stopwatch.Elapsed;

        stopwatch.Restart();
        byte[] outputHeader = await Dlss5NativeEngine.ReadExactAsync(
                worker.StandardOutput.BaseStream,
                OutputHeaderSize,
                ct)
            .ConfigureAwait(false);
        Dlss5NativeEngine.ValidateOutputHeader(outputHeader, buffers.OutputRgba.Length, frameIndex);
        TimeSpan evaluateDuration = stopwatch.Elapsed;

        stopwatch.Restart();
        await Dlss5NativeEngine.ReadExactIntoAsync(worker.StandardOutput.BaseStream, buffers.OutputRgba, ct)
            .ConfigureAwait(false);
        TimeSpan downloadDuration = stopwatch.Elapsed;
        buffers.RestoreSourceAlpha();

        return new Dlss5NativeRenderResult(
            buffers.CreateBitmap(),
            uploadDuration,
            evaluateDuration,
            downloadDuration);
    }

    private async Task<(Process Worker, bool Reused)> EnsureWorkerAsync(
        int width,
        int height,
        Dlss5RenderSettings settings,
        bool streaming,
        CancellationToken ct)
    {
        Dlss5WorkerKey requestedKey = CreateWorkerKey(width, height, settings);
        long workerGeneration;
        Process? currentWorker;
        (int Width, int Height)? currentDimensions;
        Dlss5WorkerKey? currentKey;
        lock (_workerStateSync)
        {
            workerGeneration = _workerGeneration;
            currentWorker = _worker;
            currentDimensions = _workerDimensions;
            currentKey = _workerKey;
        }

        if ((streaming)
            && (currentWorker is not null)
            && (Dlss5NativeEngine.IsProcessRunning(currentWorker))
            && (currentDimensions is { } dimensions)
            && (dimensions.Width == width)
            && (dimensions.Height == height)
            && (currentKey is { } activeKey)
            && ((HasDynamicParameterAddon) || (activeKey == requestedKey)))
        {
            _logger.LogDebug(
                "Reusing DLSS 5 worker process {ProcessId} for {Width}x{Height}; settings {Settings}; dynamic parameters {DynamicParameters}.",
                currentWorker.Id,
                width,
                height,
                settings,
                HasDynamicParameterAddon);
            return (currentWorker, true);
        }

        if (streaming)
        {
            ClearPrecomputedResult();
        }

        string restartReason = !streaming
            ? "small-image one-shot fallback"
            : currentWorker is null
            ? "initial render"
            : !Dlss5NativeEngine.IsProcessRunning(currentWorker)
                ? "previous worker exited"
                : (!HasDynamicParameterAddon)
                    && (currentKey is { } activeSettingsKey)
                    && (activeSettingsKey.Settings != settings)
                    ? "settings changed; v7 feature profile requires restart"
                    : "dimensions changed";
        if (currentWorker is not null)
        {
            StopWorker(restartReason, graceful: false);
        }

        workerGeneration = Volatile.Read(ref _workerGeneration);

        (Process worker, Task<string> stderrTask) = await StartConfiguredWorkerAsync(
            width,
            height,
            settings,
            streaming,
            restartReason,
            ct).ConfigureAwait(false);

        Process? replacedWorker = null;
        Task<string>? replacedStderrTask = null;
        uint replacedFrameCount = InitialFrameIndex;
        lock (_workerStateSync)
        {
            if ((workerGeneration != _workerGeneration) || (_isDisposed))
            {
                Dlss5NativeEngine.TryKill(worker);
                worker.Dispose();
                throw new OperationCanceledException("DLSS 5 worker was superseded while starting.");
            }

            replacedWorker = _worker;
            replacedStderrTask = _workerStderrTask;
            replacedFrameCount = _nextFrameIndex;

            _worker = worker;
            _workerStderrTask = stderrTask;
            _workerKey = requestedKey;
            _workerDimensions = (width, height);
            _nextFrameIndex = InitialFrameIndex;
        }

        StopDetachedWorker(
            replacedWorker,
            replacedStderrTask,
            replacedFrameCount,
            "replaced by newly configured worker",
            graceful: false);
        return (worker, false);
    }

    private async Task StopMismatchedWorkerAsync(Dlss5WorkerKey key, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            bool shouldStop;
            lock (_workerStateSync)
            {
                shouldStop = (_worker is not null)
                    && ((!Dlss5NativeEngine.IsProcessRunning(_worker)) || (_workerKey != key));
            }

            if (shouldStop)
            {
                StopWorker("source dimensions changed before prewarm", graceful: false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CompleteOneShotWorkerAsync(Process worker, CancellationToken ct)
    {
        worker.StandardInput.Close();
        await worker.WaitForExitAsync(ct).ConfigureAwait(false);
        string diagnostics = _workerStderrTask is null
            ? string.Empty
            : await _workerStderrTask.ConfigureAwait(false);
        if (worker.ExitCode != 0)
        {
            throw new InvalidOperationException(
                CreateWorkerFailureMessage(
                    worker.ExitCode,
                    diagnostics,
                    Path.Combine(worker.StartInfo.WorkingDirectory, "ReShade.log")));
        }

        StopWorker("small-image one-shot completed");
    }

    private string CreateWorkerDirectory()
    {
        string workersRoot = Path.GetFullPath(_paths.RuntimeDirectory);
        Directory.CreateDirectory(workersRoot);
        string workerRoot = Path.Combine(
            workersRoot,
            Dlss5WorkerDirectoryLifecycle.DirectoryPrefix + Guid.NewGuid().ToString("N"));
        string workerDirectory = Path.Combine(workerRoot, "host");
        Directory.CreateDirectory(workerDirectory);
        try
        {
            Dlss5WorkerDirectoryLifecycle.MarkOwnedByCurrentProcess(workerRoot);
            string isolatedWorkerPath = Path.Combine(workerDirectory, Path.GetFileName(_paths.WorkerPath));
            File.Copy(_paths.WorkerPath, isolatedWorkerPath);
            if (!Dlss5NativeEngine.TryOptimizeWorkerStartup(isolatedWorkerPath))
            {
                _logger.LogWarning(
                    "DLSS 5 v{Version} worker does not match the expected startup loop; the safe hook-arming optimization was skipped.",
                    Dlss5FeatureDefinition.Version);
            }
            File.Copy(_paths.HostDxgiPath, Path.Combine(workerDirectory, Path.GetFileName(_paths.HostDxgiPath)));
            string isolatedDlssDirectory = Path.Combine(workerRoot, "dlss");
            Directory.CreateDirectory(isolatedDlssDirectory);
            File.Copy(
                _paths.SuperResolutionPath,
                Path.Combine(isolatedDlssDirectory, Path.GetFileName(_paths.SuperResolutionPath)));
            string isolatedDlssnrDirectory = Path.Combine(workerRoot, "dlssnr");
            Directory.CreateDirectory(isolatedDlssnrDirectory);
            File.Copy(
                _paths.AddonPath,
                Path.Combine(isolatedDlssnrDirectory, Path.GetFileName(_paths.AddonPath)));
            File.Copy(
                _paths.NeuralRuntimePath,
                Path.Combine(isolatedDlssnrDirectory, Path.GetFileName(_paths.NeuralRuntimePath)));
            if (File.Exists(_paths.DynamicAddonPath))
            {
                File.Copy(
                    _paths.DynamicAddonPath,
                    Path.Combine(isolatedDlssnrDirectory, DynamicAddonFileName));
            }
            string licensePath = Path.Combine(_paths.DlssnrDirectory, "LICENSE-RenoDX.txt");
            if (File.Exists(licensePath))
            {
                File.Copy(licensePath, Path.Combine(isolatedDlssnrDirectory, Path.GetFileName(licensePath)));
            }
            return workerDirectory;
        }
        catch (Exception)
        {
            TryDeleteWorkerDirectory(workerDirectory);
            throw;
        }
    }

    private void WriteReShadeProfile(
        Dlss5RenderSettings settings,
        string profilePath,
        string addonDirectory)
    {
        string workerDirectory = Path.GetDirectoryName(profilePath)
            ?? throw new InvalidOperationException(
                $"DLSS 5 worker profile path '{profilePath}' has no parent directory.");
        string addonPath = Dlss5NativeEngine.CreateWorkerAddonPath(workerDirectory, addonDirectory);
        string content = Dlss5NativeEngine.CreateReShadeProfile(settings, addonPath);
        string temporaryPath = $"{profilePath}.partial";
        File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, profilePath, overwrite: true);
    }

    private void WriteDynamicParameters(Process worker, Dlss5RenderSettings settings)
    {
        if (!HasDynamicParameterAddon)
        {
            return;
        }

        WriteDynamicParameters(worker.StartInfo.WorkingDirectory, settings);
    }

    private void WriteDynamicParameters(string workerDirectory, Dlss5RenderSettings settings)
    {
        if (!HasDynamicParameterAddon)
        {
            return;
        }

        Dlss5NativeParameterMapping mapped = Dlss5NativeParameterMapping.Create(settings);
        string path = Path.Combine(workerDirectory, DynamicParameterFileName);
        string temporaryPath = $"{path}.partial";
        string content = Dlss5NativeEngine.CreateDynamicParameterContent(settings);
        File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private void CancelPrewarm()
    {
        PrewarmState[] states;
        Dlss5NativeRenderResult? precomputedResult;
        lock (_workerStateSync)
        {
            states = _prewarms.Values.ToArray();
            _prewarms.Clear();
            precomputedResult = _precomputedResult;
            _precomputedResult = null;
            _precomputedKey = null;
        }

        foreach (PrewarmState state in states)
        {
            state.Cancellation.Cancel();
            state.Completion.TrySetCanceled();
        }

        precomputedResult?.Bitmap.Dispose();
    }

    private void ClearPrecomputedResult()
    {
        Dlss5NativeRenderResult? precomputedResult;
        lock (_workerStateSync)
        {
            precomputedResult = _precomputedResult;
            _precomputedResult = null;
            _precomputedKey = null;
        }

        precomputedResult?.Bitmap.Dispose();
    }

    private void StopWorker(string reason, bool graceful = false)
    {
        Process? worker;
        Task<string>? stderrTask;
        uint frameCount;
        lock (_workerStateSync)
        {
            // Invalidate any EnsureWorkerAsync call that is still between
            // process setup and publishing the process into engine state.
            _workerGeneration++;
            worker = _worker;
            stderrTask = _workerStderrTask;
            frameCount = _nextFrameIndex;
            _worker = null;
            _workerStderrTask = null;
            _workerKey = null;
            _workerDimensions = null;
            _nextFrameIndex = InitialFrameIndex;
        }

        StopDetachedWorker(worker, stderrTask, frameCount, reason, graceful);
    }

    private void StopDetachedWorker(
        Process? worker,
        Task<string>? stderrTask,
        uint frameCount,
        string reason,
        bool graceful)
    {
        if (worker is null)
        {
            return;
        }

        _logger.LogInformation(
            "Stopping DLSS 5 worker process {ProcessId}; reason {StopReason}.",
            worker.Id,
            reason);
        if ((graceful) && (frameCount > 0))
        {
            Dlss5NativeEngine.TryCloseWorker(worker, frameCount);
        }

        Dlss5NativeEngine.TryKill(worker);
        try
        {
            int waitMilliseconds = graceful ? GracefulStopWaitMilliseconds : ForcedStopWaitMilliseconds;
            if ((!worker.WaitForExit(waitMilliseconds)) && (!worker.HasExited))
            {
                _logger.LogWarning(
                    "DLSS 5 worker process {ProcessId} did not exit within {WaitMilliseconds} ms after termination request.",
                    worker.Id,
                    waitMilliseconds);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or ObjectDisposedException
            or System.ComponentModel.Win32Exception)
        {
        }

        bool workerExited = !Dlss5NativeEngine.IsProcessRunning(worker);
        if ((stderrTask is not null) && (workerExited))
        {
            try
            {
                string diagnostics = stderrTask.GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(diagnostics))
                {
                    _logger.LogDebug(
                        "DLSS 5 worker {ProcessId} diagnostics ({StopReason}): {Diagnostics}.",
                        worker.Id,
                        reason,
                        diagnostics.Trim());
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
            }
        }

        string workerDirectory = worker.StartInfo.WorkingDirectory;
        string reshadeDiagnostics = ReadReShadeDiagnostics(Path.Combine(workerDirectory, "ReShade.log"));
        if (!string.IsNullOrWhiteSpace(reshadeDiagnostics))
        {
            _logger.LogDebug(
                "DLSS 5 worker {ProcessId} ReShade diagnostics ({StopReason}): {Diagnostics}.",
                worker.Id,
                reason,
                reshadeDiagnostics);
        }

        worker.Dispose();
        if (workerExited)
        {
            TryDeleteWorkerDirectory(workerDirectory);
        }
        else
        {
            _logger.LogWarning(
                "Preserving DLSS 5 worker directory {WorkerDirectory} because process termination is still pending.",
                workerDirectory);
        }
    }

    private void TryDeleteWorkerDirectory(string? workerDirectory)
    {
        Dlss5WorkerDirectoryLifecycle.TryDeleteWorkerDirectory(
            _paths.RuntimeDirectory,
            workerDirectory,
            _logger);
    }

    private string CreateWorkerFailureMessage(int exitCode, string stderr, string? reshadeLogPath = null)
    {
        string reshade = ReadReShadeDiagnostics(reshadeLogPath);
        StringBuilder message = new();
        message.Append("DLSS 5 worker exited with code ");
        message.Append(exitCode.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            message.Append(". Worker diagnostics: ");
            message.Append(stderr.Trim());
        }

        if (!string.IsNullOrWhiteSpace(reshade))
        {
            message.Append(" ReShade diagnostics: ");
            message.Append(reshade);
        }

        return message.ToString();
    }

    private string ReadReShadeDiagnostics(string? reshadeLogPath = null)
    {
        try
        {
            string logPath = string.IsNullOrWhiteSpace(reshadeLogPath)
                ? _paths.ReShadeLogPath
                : reshadeLogPath;
            if (!File.Exists(logPath))
            {
                return string.Empty;
            }

            string[] lines = File.ReadAllLines(logPath);
            return string.Join(
                Environment.NewLine,
                lines.Where(line => (line.Contains("DLSS", StringComparison.OrdinalIgnoreCase))
                    || (line.Contains("feature 18", StringComparison.OrdinalIgnoreCase))
                    || (line.Contains("failed", StringComparison.OrdinalIgnoreCase))
                    || (line.Contains("exception", StringComparison.OrdinalIgnoreCase)))
                .TakeLast(MaximumDiagnosticLineCount));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Unable to read DLSS 5 ReShade diagnostics at {ReShadeLogPath}.", reshadeLogPath ?? _paths.ReShadeLogPath);
            return string.Empty;
        }
    }

    private sealed class PrewarmState
    {
        public required long Sequence { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public required TaskCompletionSource<object?> Completion { get; init; }
    }
}
