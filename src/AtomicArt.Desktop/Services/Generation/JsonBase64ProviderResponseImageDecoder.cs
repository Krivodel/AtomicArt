using System.Buffers;
using System.Buffers.Text;
using System.Text;

using Microsoft.Extensions.Options;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class JsonBase64ProviderResponseImageDecoder
    : IProviderResponseImageDecoder
{
    private static readonly byte[] B64JsonPropertyName = "\"b64_json\""u8.ToArray();
    private static readonly byte[] ContentPropertyName = "\"content\""u8.ToArray();
    private static readonly byte[] DataPropertyName = "\"data\""u8.ToArray();
    private static readonly byte[] ImageUrlPropertyName = "\"image_url\""u8.ToArray();
    private static readonly byte[] UrlPropertyName = "\"url\""u8.ToArray();

    private readonly int _inputBufferSize;
    private readonly long _maximumImageBytes;
    private readonly int _outputBufferSize;

    public JsonBase64ProviderResponseImageDecoder(
        IOptions<GenerationClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _inputBufferSize = options.Value.Base64DecoderInputBufferSize;
        _maximumImageBytes =
            options.Value.MaxDecodedProviderResponseImageBytes;
        _outputBufferSize = options.Value.Base64DecoderOutputBufferSize;
    }

    public bool CanDecode(string providerId, string contentType)
    {
        return (string.Equals(
                    providerId,
                    GenerationProviderIds.Google,
                    StringComparison.Ordinal)
                || string.Equals(
                    providerId,
                    GenerationProviderIds.Test,
                    StringComparison.Ordinal)
                || string.Equals(
                    providerId,
                    GenerationProviderIds.OpenRouter,
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

        byte[] inputBuffer = ArrayPool<byte>.Shared.Rent(_inputBufferSize);
        DecoderState state = new(
            _outputBufferSize,
            _maximumImageBytes);

        try
        {
            while (true)
            {
                int bytesRead = await providerResponse
                    .ReadAsync(inputBuffer.AsMemory(0, _inputBufferSize), ct)
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
        private readonly byte[] _outputBuffer;
        private readonly byte[] _base64Quartet = new byte[4];
        private readonly byte[] _dataUrlPrefix = new byte[128];
        private readonly long _maximumImageBytes;
        private readonly int _outputBufferSize;
        private AnalyzerState _state;
        private int _candidateIndex;
        private int _base64Count;
        private int _outputCount;
        private int _imageCount;
        private long _totalOutputBytes;
        private bool _colonSeen;
        private bool _expectsDataUrl;
        private bool _hasDataUrlPrefix;
        private int _dataUrlPrefixLength;
        private ImageDataProperty _candidateProperty;

        public bool ShouldFlush =>
            _outputCount >= _outputBufferSize - 3;

        public DecoderState(
            int outputBufferSize,
            long maximumImageBytes)
        {
            _outputBuffer = ArrayPool<byte>.Shared.Rent(outputBufferSize);
            _maximumImageBytes = maximumImageBytes;
            _outputBufferSize = outputBufferSize;
        }

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
                _candidateProperty = ImageDataProperty.None;
                _state = AnalyzerState.CandidateDataKey;
            }
        }

        private void ProcessCandidate(byte value)
        {
            if (_candidateIndex == 1)
            {
                _candidateProperty = value switch
                {
                    (byte)'d' => ImageDataProperty.Data,
                    (byte)'b' => ImageDataProperty.B64Json,
                    (byte)'c' => ImageDataProperty.Content,
                    (byte)'i' => ImageDataProperty.ImageUrl,
                    (byte)'u' => ImageDataProperty.Url,
                    _ => ImageDataProperty.None
                };
            }

            ReadOnlySpan<byte> candidatePropertyName = GetCandidatePropertyName();

            if (_candidateIndex < candidatePropertyName.Length
                && value == candidatePropertyName[_candidateIndex])
            {
                _candidateIndex++;

                if (_candidateIndex == candidatePropertyName.Length)
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
                _expectsDataUrl = _candidateProperty is ImageDataProperty.Content
                    or ImageDataProperty.ImageUrl
                    or ImageDataProperty.Url;

                if (!_expectsDataUrl && _imageCount != 0)
                {
                    throw new InvalidDataException(
                        "Provider response contains more than one image.");
                }

                _hasDataUrlPrefix = false;
                _dataUrlPrefixLength = 0;
                _state = AnalyzerState.DecodeDataString;
                return;
            }

            _state = AnalyzerState.NormalOutsideString;
        }

        private void ProcessBase64(byte value)
        {
            if (_expectsDataUrl && !_hasDataUrlPrefix)
            {
                ProcessDataUrlPrefix(value);
                return;
            }

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

            if (_totalOutputBytes > _maximumImageBytes)
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

        private ReadOnlySpan<byte> GetCandidatePropertyName()
        {
            return _candidateProperty switch
            {
                ImageDataProperty.Data => DataPropertyName,
                ImageDataProperty.B64Json => B64JsonPropertyName,
                ImageDataProperty.Content => ContentPropertyName,
                ImageDataProperty.ImageUrl => ImageUrlPropertyName,
                ImageDataProperty.Url => UrlPropertyName,
                _ => []
            };
        }

        private void ProcessDataUrlPrefix(byte value)
        {
            if (value == (byte)'"')
            {
                _state = AnalyzerState.NormalOutsideString;
                return;
            }

            if (_dataUrlPrefixLength == _dataUrlPrefix.Length)
            {
                throw new InvalidDataException("Provider image data URL prefix is too long.");
            }

            _dataUrlPrefix[_dataUrlPrefixLength] = value;
            _dataUrlPrefixLength++;

            if (value != (byte)',')
            {
                return;
            }

            string prefix = Encoding.ASCII.GetString(_dataUrlPrefix, 0, _dataUrlPrefixLength);

            if (!prefix.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                || !prefix.EndsWith(";base64,", StringComparison.OrdinalIgnoreCase))
            {
                _state = AnalyzerState.NormalInsideString;
                return;
            }

            if (_imageCount != 0)
            {
                _state = AnalyzerState.NormalInsideString;
                return;
            }

            _hasDataUrlPrefix = true;
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

    private enum ImageDataProperty
    {
        None,
        Data,
        B64Json,
        Content,
        ImageUrl,
        Url
    }
}
