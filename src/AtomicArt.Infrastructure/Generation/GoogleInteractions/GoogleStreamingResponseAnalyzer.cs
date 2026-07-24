using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleStreamingResponseAnalyzer
{
    private static readonly byte[] DataPropertyName = CreateQuotedPropertyName(
        GoogleInteractionsContentContract.DataPropertyName);
    private static readonly byte[] SignaturePropertyName =
        CreateQuotedPropertyName(
            GoogleInteractionsContentContract.SignaturePropertyName);
    private static readonly byte[] ReplacementDataValue = "\"AA==\""u8.ToArray();
    private static readonly byte[] ReplacementSignatureValue = "\"\""u8.ToArray();
    private static readonly SearchValues<byte> JsonWhitespace =
        SearchValues.Create(" \t\r\n"u8);
    private static readonly SearchValues<byte> StringTerminators =
        SearchValues.Create(new byte[] { (byte)'"', (byte)'\\' });

    private readonly GoogleInteractionsResponseParser _responseParser;
    private readonly GoogleInteractionsFailureClassifier _failureClassifier;
    private readonly int _maximumFilteredResponseBytes;
    private readonly int _maximumStructureDepth;
    private readonly int _maximumDiagnosticTextCharacters;
    private readonly MemoryStream _filteredResponse = new();
    private readonly List<byte> _candidate = [];
    private AnalyzerState _state;
    private SkippedStringProperty _candidateProperty;
    private SkippedStringProperty _skippedProperty;
    private int _candidateIndex;
    private bool _colonSeen;

    public GoogleStreamingResponseAnalyzer(
        GoogleInteractionsResponseParser responseParser,
        GoogleInteractionsFailureClassifier failureClassifier,
        int maximumFilteredResponseBytes =
            GoogleInteractionsOptions.DefaultMaxAnalyzedMetadataBytes,
        int maximumStructureDepth =
            GoogleInteractionsOptions.DefaultMaxResponseStructureDepth,
        int maximumDiagnosticTextCharacters =
            GoogleInteractionsOptions.DefaultMaxDiagnosticTextCharacters)
    {
        _responseParser = responseParser
            ?? throw new ArgumentNullException(nameof(responseParser));
        _failureClassifier = failureClassifier
            ?? throw new ArgumentNullException(nameof(failureClassifier));
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumFilteredResponseBytes,
            1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumStructureDepth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumDiagnosticTextCharacters,
            1);
        _maximumFilteredResponseBytes = maximumFilteredResponseBytes;
        _maximumStructureDepth = maximumStructureDepth;
        _maximumDiagnosticTextCharacters =
            maximumDiagnosticTextCharacters;
    }

    public void Append(ReadOnlySpan<byte> content)
    {
        ClientResponseWriter clientResponseWriter = new([], false);
        Append(content, ref clientResponseWriter);
    }

    public int AppendAndSanitize(Span<byte> content)
    {
        ClientResponseWriter clientResponseWriter = new(content, true);
        Append(content, ref clientResponseWriter);

        return clientResponseWriter.WrittenCount;
    }

    private void Append(
        ReadOnlySpan<byte> content,
        ref ClientResponseWriter clientResponseWriter)
    {
        int consumedBytes = 0;

        while (consumedBytes < content.Length)
        {
            ReadOnlySpan<byte> remainingContent = content[consumedBytes..];
            consumedBytes += _state switch
            {
                AnalyzerState.NormalOutsideString
                    => ProcessOutsideString(
                        remainingContent,
                        ref clientResponseWriter),
                AnalyzerState.NormalInsideString
                    => ProcessInsideString(
                        remainingContent,
                        ref clientResponseWriter),
                AnalyzerState.NormalEscape
                    => ProcessNormalEscape(
                        remainingContent,
                        ref clientResponseWriter),
                AnalyzerState.CandidateSkippedKey
                    => ProcessCandidate(
                        remainingContent,
                        ref clientResponseWriter),
                AnalyzerState.AfterSkippedKey
                    => ProcessAfterSkippedKey(
                        remainingContent,
                        ref clientResponseWriter),
                AnalyzerState.SkipStringValue
                    => SkipStringValue(
                        remainingContent,
                        ref clientResponseWriter),
                AnalyzerState.SkipStringEscape
                    => SkipStringEscape(
                        remainingContent,
                        ref clientResponseWriter),
                _ => throw new InvalidOperationException(
                    "Unknown analyzer state.")
            };
        }
    }

    public ProviderGenerationSummary Complete()
    {
        FlushCandidate();

        if (_state is AnalyzerState.SkipStringValue
            or AnalyzerState.SkipStringEscape
            or AnalyzerState.NormalInsideString
            or AnalyzerState.NormalEscape
            or AnalyzerState.CandidateSkippedKey
            or AnalyzerState.AfterSkippedKey)
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider returned malformed JSON.");
        }

        ReadOnlyMemory<byte> filteredJson = _filteredResponse
            .GetBuffer()
            .AsMemory(0, checked((int)_filteredResponse.Length));

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                filteredJson,
                new JsonDocumentOptions
                {
                    MaxDepth = _maximumStructureDepth
                });
            JsonElement root = document.RootElement;

            ThrowIfDiagnosticTextExceedsLimit(root);
            ThrowIfTemporaryInternalError(root);

            return _responseParser.ParseFilteredMetadata(root);
        }
        catch (JsonException exception)
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider returned malformed JSON.",
                false,
                exception);
        }
    }

    private void ThrowIfTemporaryInternalError(JsonElement root)
    {
        if (_failureClassifier.IsTemporaryInternalError(root))
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InternalError,
                "The generation provider returned a temporary internal error.",
                true);
        }
    }

    private void ThrowIfDiagnosticTextExceedsLimit(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                bool isTextContent =
                    GoogleInteractionsContentContract.IsTextContent(element);

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (isTextContent
                        && property.NameEquals(
                            GoogleInteractionsContentContract
                                .TextPropertyName))
                    {
                        continue;
                    }

                    ThrowIfDiagnosticTextExceedsLimit(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    ThrowIfDiagnosticTextExceedsLimit(item);
                }

                break;
            case JsonValueKind.String:
                if ((element.GetString()?.Length ?? 0)
                    > _maximumDiagnosticTextCharacters)
                {
                    throw new GoogleInteractionsException(
                        ImageGenerationProviderFailureKind.InvalidResponse,
                        "The generation provider response contains diagnostic text that exceeds its limit.");
                }

                break;
        }
    }

    private int ProcessOutsideString(
        ReadOnlySpan<byte> content,
        ref ClientResponseWriter clientResponseWriter)
    {
        int quoteIndex = content.IndexOf((byte)'"');

        if (quoteIndex < 0)
        {
            WriteRetainedBytes(content, ref clientResponseWriter);

            return content.Length;
        }

        WriteRetainedBytes(
            content[..quoteIndex],
            ref clientResponseWriter);
        StartCandidate(ref clientResponseWriter);

        return quoteIndex + 1;
    }

    private int ProcessInsideString(
        ReadOnlySpan<byte> content,
        ref ClientResponseWriter clientResponseWriter)
    {
        int terminatorIndex = content.IndexOfAny(StringTerminators);

        if (terminatorIndex < 0)
        {
            WriteRetainedBytes(content, ref clientResponseWriter);

            return content.Length;
        }

        int consumedBytes = terminatorIndex + 1;
        byte terminator = content[terminatorIndex];

        WriteRetainedBytes(
            content[..consumedBytes],
            ref clientResponseWriter);
        _state = terminator == (byte)'\\'
            ? AnalyzerState.NormalEscape
            : AnalyzerState.NormalOutsideString;

        return consumedBytes;
    }

    private int ProcessNormalEscape(
        ReadOnlySpan<byte> content,
        ref ClientResponseWriter clientResponseWriter)
    {
        WriteRetainedBytes(content[..1], ref clientResponseWriter);
        _state = AnalyzerState.NormalInsideString;

        return 1;
    }

    private int ProcessCandidate(
        ReadOnlySpan<byte> content,
        ref ClientResponseWriter clientResponseWriter)
    {
        int consumedBytes = 0;

        while (consumedBytes < content.Length
            && _state == AnalyzerState.CandidateSkippedKey)
        {
            byte value = content[consumedBytes];
            AppendCandidate(content.Slice(consumedBytes, 1));
            clientResponseWriter.Write(content.Slice(consumedBytes, 1));
            consumedBytes++;

            if (_candidateIndex == 1)
            {
                _candidateProperty = value switch
                {
                    (byte)'d' => SkippedStringProperty.Data,
                    (byte)'s' => SkippedStringProperty.Signature,
                    _ => SkippedStringProperty.None
                };
            }

            ReadOnlySpan<byte> propertyName = GetCandidatePropertyName();

            if (_candidateIndex < propertyName.Length
                && value == propertyName[_candidateIndex])
            {
                _candidateIndex++;

                if (_candidateIndex == propertyName.Length)
                {
                    _colonSeen = false;
                    _state = AnalyzerState.AfterSkippedKey;
                }

                continue;
            }

            FlushCandidate();
            _candidateProperty = SkippedStringProperty.None;
            _state = value switch
            {
                (byte)'\\' => AnalyzerState.NormalEscape,
                (byte)'"' => AnalyzerState.NormalOutsideString,
                _ => AnalyzerState.NormalInsideString
            };
        }

        return consumedBytes;
    }

    private int ProcessAfterSkippedKey(
        ReadOnlySpan<byte> content,
        ref ClientResponseWriter clientResponseWriter)
    {
        int whitespaceLength = content.IndexOfAnyExcept(JsonWhitespace);

        if (whitespaceLength < 0)
        {
            AppendCandidate(content);
            clientResponseWriter.Write(content);

            return content.Length;
        }

        AppendCandidate(content[..whitespaceLength]);
        clientResponseWriter.Write(content[..whitespaceLength]);
        byte value = content[whitespaceLength];
        int consumedBytes = whitespaceLength + 1;

        if (!_colonSeen && value == (byte)':')
        {
            AppendCandidate(content.Slice(whitespaceLength, 1));
            clientResponseWriter.Write(
                content.Slice(whitespaceLength, 1));
            _colonSeen = true;

            return consumedBytes;
        }

        if (_colonSeen && value == (byte)'"')
        {
            FlushCandidate();
            WriteReplacementValue();
            clientResponseWriter.Write(
                content.Slice(whitespaceLength, 1));
            _skippedProperty = _candidateProperty;
            _candidateProperty = SkippedStringProperty.None;
            _state = AnalyzerState.SkipStringValue;

            return consumedBytes;
        }

        AppendCandidate(content.Slice(whitespaceLength, 1));
        clientResponseWriter.Write(content.Slice(whitespaceLength, 1));
        FlushCandidate();
        _candidateProperty = SkippedStringProperty.None;
        _state = AnalyzerState.NormalOutsideString;

        return consumedBytes;
    }

    private int SkipStringValue(
        ReadOnlySpan<byte> content,
        ref ClientResponseWriter clientResponseWriter)
    {
        int terminatorIndex = content.IndexOfAny(StringTerminators);

        if (terminatorIndex < 0)
        {
            WriteSkippedValueBytes(content, ref clientResponseWriter);

            return content.Length;
        }

        int consumedBytes = terminatorIndex + 1;
        byte terminator = content[terminatorIndex];

        WriteSkippedValueBytes(
            content[..consumedBytes],
            ref clientResponseWriter);

        if (terminator == (byte)'\\')
        {
            _state = AnalyzerState.SkipStringEscape;
        }
        else
        {
            if (_skippedProperty == SkippedStringProperty.Signature)
            {
                clientResponseWriter.Write(
                    content.Slice(terminatorIndex, 1));
            }

            _skippedProperty = SkippedStringProperty.None;
            _state = AnalyzerState.NormalOutsideString;
        }

        return consumedBytes;
    }

    private int SkipStringEscape(
        ReadOnlySpan<byte> content,
        ref ClientResponseWriter clientResponseWriter)
    {
        WriteSkippedValueBytes(content[..1], ref clientResponseWriter);
        _state = AnalyzerState.SkipStringValue;

        return 1;
    }

    private void StartCandidate(
        ref ClientResponseWriter clientResponseWriter)
    {
        _candidate.Clear();
        AppendCandidate(DataPropertyName.AsSpan(0, 1));
        clientResponseWriter.Write(DataPropertyName.AsSpan(0, 1));
        _candidateProperty = SkippedStringProperty.None;
        _candidateIndex = 1;
        _state = AnalyzerState.CandidateSkippedKey;
    }

    private ReadOnlySpan<byte> GetCandidatePropertyName()
    {
        return _candidateProperty switch
        {
            SkippedStringProperty.Data => DataPropertyName,
            SkippedStringProperty.Signature => SignaturePropertyName,
            _ => []
        };
    }

    private void WriteReplacementValue()
    {
        WriteBytes(_candidateProperty switch
        {
            SkippedStringProperty.Data => ReplacementDataValue,
            SkippedStringProperty.Signature => ReplacementSignatureValue,
            _ => throw new InvalidOperationException(
                "Unknown skipped string property.")
        });
    }

    private void WriteRetainedBytes(
        ReadOnlySpan<byte> values,
        ref ClientResponseWriter clientResponseWriter)
    {
        WriteBytes(values);
        clientResponseWriter.Write(values);
    }

    private void WriteSkippedValueBytes(
        ReadOnlySpan<byte> values,
        ref ClientResponseWriter clientResponseWriter)
    {
        if (_skippedProperty == SkippedStringProperty.Data)
        {
            clientResponseWriter.Write(values);
        }
    }

    private void AppendCandidate(ReadOnlySpan<byte> values)
    {
        EnsureCandidateCapacity(values.Length);

        int previousCount = _candidate.Count;
        int requiredCount = previousCount + values.Length;

        _candidate.EnsureCapacity(requiredCount);
        CollectionsMarshal.SetCount(_candidate, requiredCount);
        values.CopyTo(CollectionsMarshal.AsSpan(_candidate)[previousCount..]);
    }

    private void FlushCandidate()
    {
        if (_candidate.Count == 0)
        {
            return;
        }

        WriteBytes(CollectionsMarshal.AsSpan(_candidate));
        _candidate.Clear();
    }

    private void WriteBytes(ReadOnlySpan<byte> values)
    {
        EnsureFilteredResponseCapacity(values.Length);
        _filteredResponse.Write(values);
    }

    private void EnsureCandidateCapacity(int additionalBytes)
    {
        if (_filteredResponse.Length + _candidate.Count + additionalBytes
            > _maximumFilteredResponseBytes)
        {
            throw CreateMetadataLimitException();
        }
    }

    private void EnsureFilteredResponseCapacity(int additionalBytes)
    {
        if (_filteredResponse.Length + additionalBytes
            > _maximumFilteredResponseBytes)
        {
            throw CreateMetadataLimitException();
        }
    }

    private static GoogleInteractionsException CreateMetadataLimitException()
    {
        return new GoogleInteractionsException(
            ImageGenerationProviderFailureKind.InvalidResponse,
            "The generation provider response metadata exceeded its limit.");
    }

    private static byte[] CreateQuotedPropertyName(string propertyName)
    {
        return Encoding.UTF8.GetBytes(string.Concat("\"", propertyName, "\""));
    }

    private enum AnalyzerState
    {
        NormalOutsideString,
        NormalInsideString,
        NormalEscape,
        CandidateSkippedKey,
        AfterSkippedKey,
        SkipStringValue,
        SkipStringEscape
    }

    private enum SkippedStringProperty
    {
        None,
        Data,
        Signature
    }

    private ref struct ClientResponseWriter
    {
        public int WrittenCount { get; private set; }

        private readonly Span<byte> _destination;
        private readonly bool _enabled;

        public ClientResponseWriter(Span<byte> destination, bool enabled)
        {
            _destination = destination;
            _enabled = enabled;
        }

        public void Write(ReadOnlySpan<byte> values)
        {
            if (!_enabled)
            {
                return;
            }

            if (values.Length > _destination.Length - WrittenCount)
            {
                throw new InvalidOperationException(
                    "The sanitized response exceeded its input block.");
            }

            values.CopyTo(_destination[WrittenCount..]);
            WrittenCount += values.Length;
        }
    }
}
