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
        int consumedBytes = 0;

        while (consumedBytes < content.Length)
        {
            ReadOnlySpan<byte> remainingContent = content[consumedBytes..];
            consumedBytes += _state switch
            {
                AnalyzerState.NormalOutsideString
                    => ProcessOutsideString(remainingContent),
                AnalyzerState.NormalInsideString
                    => ProcessInsideString(remainingContent),
                AnalyzerState.NormalEscape
                    => ProcessNormalEscape(remainingContent),
                AnalyzerState.CandidateSkippedKey
                    => ProcessCandidate(remainingContent),
                AnalyzerState.AfterSkippedKey
                    => ProcessAfterSkippedKey(remainingContent),
                AnalyzerState.SkipStringValue
                    => SkipStringValue(remainingContent),
                AnalyzerState.SkipStringEscape
                    => SkipStringEscape(remainingContent),
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
                foreach (JsonProperty property in element.EnumerateObject())
                {
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

    private int ProcessOutsideString(ReadOnlySpan<byte> content)
    {
        int quoteIndex = content.IndexOf((byte)'"');

        if (quoteIndex < 0)
        {
            WriteBytes(content);

            return content.Length;
        }

        WriteBytes(content[..quoteIndex]);
        StartCandidate();

        return quoteIndex + 1;
    }

    private int ProcessInsideString(ReadOnlySpan<byte> content)
    {
        int terminatorIndex = content.IndexOfAny(StringTerminators);

        if (terminatorIndex < 0)
        {
            WriteBytes(content);

            return content.Length;
        }

        int consumedBytes = terminatorIndex + 1;
        byte terminator = content[terminatorIndex];

        WriteBytes(content[..consumedBytes]);
        _state = terminator == (byte)'\\'
            ? AnalyzerState.NormalEscape
            : AnalyzerState.NormalOutsideString;

        return consumedBytes;
    }

    private int ProcessNormalEscape(ReadOnlySpan<byte> content)
    {
        WriteBytes(content[..1]);
        _state = AnalyzerState.NormalInsideString;

        return 1;
    }

    private int ProcessCandidate(ReadOnlySpan<byte> content)
    {
        int consumedBytes = 0;

        while (consumedBytes < content.Length
            && _state == AnalyzerState.CandidateSkippedKey)
        {
            byte value = content[consumedBytes];
            AppendCandidate(content.Slice(consumedBytes, 1));
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

    private int ProcessAfterSkippedKey(ReadOnlySpan<byte> content)
    {
        int whitespaceLength = content.IndexOfAnyExcept(JsonWhitespace);

        if (whitespaceLength < 0)
        {
            AppendCandidate(content);

            return content.Length;
        }

        AppendCandidate(content[..whitespaceLength]);
        byte value = content[whitespaceLength];
        int consumedBytes = whitespaceLength + 1;

        if (!_colonSeen && value == (byte)':')
        {
            AppendCandidate(content.Slice(whitespaceLength, 1));
            _colonSeen = true;

            return consumedBytes;
        }

        if (_colonSeen && value == (byte)'"')
        {
            FlushCandidate();
            WriteReplacementValue();
            _candidateProperty = SkippedStringProperty.None;
            _state = AnalyzerState.SkipStringValue;

            return consumedBytes;
        }

        AppendCandidate(content.Slice(whitespaceLength, 1));
        FlushCandidate();
        _candidateProperty = SkippedStringProperty.None;
        _state = AnalyzerState.NormalOutsideString;

        return consumedBytes;
    }

    private int SkipStringValue(ReadOnlySpan<byte> content)
    {
        int terminatorIndex = content.IndexOfAny(StringTerminators);

        if (terminatorIndex < 0)
        {
            return content.Length;
        }

        _state = content[terminatorIndex] == (byte)'\\'
            ? AnalyzerState.SkipStringEscape
            : AnalyzerState.NormalOutsideString;

        return terminatorIndex + 1;
    }

    private int SkipStringEscape(ReadOnlySpan<byte> content)
    {
        _state = AnalyzerState.SkipStringValue;

        return 1;
    }

    private void StartCandidate()
    {
        _candidate.Clear();
        AppendCandidate(DataPropertyName.AsSpan(0, 1));
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
}
