using System.Buffers;
using System.Buffers.Text;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class JsonBase64ProviderResponseImageDecoder
    : IProviderResponseImageDecoder
{
    private const int InputBufferSize = 65536;
    private const int OutputBufferSize = 49152;
    private const long MaximumImageBytes = 512L * 1024L * 1024L;

    private static readonly byte[] DataPropertyName = "\"data\""u8.ToArray();

    public bool CanDecode(string providerId, string contentType)
    {
        return (string.Equals(
                    providerId,
                    GenerationProviderIds.Google,
                    StringComparison.Ordinal)
                || string.Equals(
                    providerId,
                    GenerationProviderIds.Test,
                    StringComparison.Ordinal))
            && contentType.StartsWith(
                "application/json",
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task DecodeAsync(
        Stream providerResponse,
        Stream imageDestination,
        ProviderResponseImageDecodeResult result,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(providerResponse);
        ArgumentNullException.ThrowIfNull(imageDestination);
        ArgumentNullException.ThrowIfNull(result);

        byte[] inputBuffer = ArrayPool<byte>.Shared.Rent(InputBufferSize);
        DecoderState state = new();

        try
        {
            while (true)
            {
                int bytesRead = await providerResponse
                    .ReadAsync(inputBuffer.AsMemory(0, InputBufferSize), ct)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                for (int index = 0; index < bytesRead; index++)
                {
                    state.Process(inputBuffer[index]);

                    if (state.ShouldFlush)
                    {
                        await state.FlushAsync(imageDestination, ct)
                            .ConfigureAwait(false);
                    }
                }
            }

            if (state.Complete())
            {
                result.SetHasImage();
            }

            await state.FlushAsync(imageDestination, ct).ConfigureAwait(false);
            await imageDestination.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            state.Dispose();
            ArrayPool<byte>.Shared.Return(inputBuffer);
        }
    }

    private sealed class DecoderState : IDisposable
    {
        private readonly byte[] _outputBuffer =
            ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        private readonly byte[] _base64Quartet = new byte[4];
        private AnalyzerState _state;
        private int _candidateIndex;
        private int _base64Count;
        private int _outputCount;
        private int _imageCount;
        private long _totalOutputBytes;
        private bool _colonSeen;

        public bool ShouldFlush =>
            _outputCount >= OutputBufferSize - 3;

        public void Process(byte value)
        {
            switch (_state)
            {
                case AnalyzerState.NormalOutsideString:
                    ProcessOutsideString(value);
                    break;
                case AnalyzerState.NormalInsideString:
                    _state = value switch
                    {
                        (byte)'\\' => AnalyzerState.NormalEscape,
                        (byte)'"' => AnalyzerState.NormalOutsideString,
                        _ => AnalyzerState.NormalInsideString
                    };
                    break;
                case AnalyzerState.NormalEscape:
                    _state = AnalyzerState.NormalInsideString;
                    break;
                case AnalyzerState.CandidateDataKey:
                    ProcessCandidate(value);
                    break;
                case AnalyzerState.AfterDataKey:
                    ProcessAfterDataKey(value);
                    break;
                case AnalyzerState.DecodeDataString:
                    ProcessBase64(value);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown JSON image decoder state.");
            }
        }

        public async Task FlushAsync(
            Stream destination,
            CancellationToken ct)
        {
            if (_outputCount == 0)
            {
                return;
            }

            await destination
                .WriteAsync(_outputBuffer.AsMemory(0, _outputCount), ct)
                .ConfigureAwait(false);
            _outputCount = 0;
        }

        public bool Complete()
        {
            if (_state != AnalyzerState.NormalOutsideString
                || _base64Count != 0
                || _imageCount > 1)
            {
                throw new InvalidDataException(
                    "Provider response ended with malformed image data.");
            }

            return _imageCount == 1;
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_outputBuffer);
        }

        private void ProcessOutsideString(byte value)
        {
            if (value == (byte)'"')
            {
                _candidateIndex = 1;
                _state = AnalyzerState.CandidateDataKey;
            }
        }

        private void ProcessCandidate(byte value)
        {
            if (_candidateIndex < DataPropertyName.Length
                && value == DataPropertyName[_candidateIndex])
            {
                _candidateIndex++;

                if (_candidateIndex == DataPropertyName.Length)
                {
                    _colonSeen = false;
                    _state = AnalyzerState.AfterDataKey;
                }

                return;
            }

            _state = value switch
            {
                (byte)'\\' => AnalyzerState.NormalEscape,
                (byte)'"' => AnalyzerState.NormalOutsideString,
                _ => AnalyzerState.NormalInsideString
            };
        }

        private void ProcessAfterDataKey(byte value)
        {
            if (IsJsonWhitespace(value))
            {
                return;
            }

            if (!_colonSeen && value == (byte)':')
            {
                _colonSeen = true;
                return;
            }

            if (_colonSeen && value == (byte)'"')
            {
                if (_imageCount != 0)
                {
                    throw new InvalidDataException(
                        "Provider response contains more than one image.");
                }

                _state = AnalyzerState.DecodeDataString;
                return;
            }

            _state = AnalyzerState.NormalOutsideString;
        }

        private void ProcessBase64(byte value)
        {
            if (value == (byte)'"')
            {
                if (_base64Count != 0)
                {
                    throw new InvalidDataException(
                        "Provider response contains truncated Base64 image data.");
                }

                _imageCount++;
                _state = AnalyzerState.NormalOutsideString;
                return;
            }

            if (IsJsonWhitespace(value))
            {
                return;
            }

            _base64Quartet[_base64Count] = value;
            _base64Count++;

            if (_base64Count < _base64Quartet.Length)
            {
                return;
            }

            OperationStatus status = Base64.DecodeFromUtf8(
                _base64Quartet,
                _outputBuffer.AsSpan(_outputCount),
                out int consumed,
                out int written);

            if (status != OperationStatus.Done
                || consumed != _base64Quartet.Length)
            {
                throw new InvalidDataException(
                    "Provider response contains invalid Base64 image data.");
            }

            _base64Count = 0;
            _outputCount += written;
            _totalOutputBytes += written;

            if (_totalOutputBytes > MaximumImageBytes)
            {
                throw new InvalidDataException(
                    "Provider image exceeds the configured size limit.");
            }
        }

        private static bool IsJsonWhitespace(byte value)
        {
            return value is (byte)' '
                or (byte)'\t'
                or (byte)'\r'
                or (byte)'\n';
        }
    }

    private enum AnalyzerState
    {
        NormalOutsideString,
        NormalInsideString,
        NormalEscape,
        CandidateDataKey,
        AfterDataKey,
        DecodeDataString
    }
}
