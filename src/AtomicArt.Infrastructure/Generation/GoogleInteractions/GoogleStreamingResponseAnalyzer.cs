using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;

using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleStreamingResponseAnalyzer
{
    private static readonly byte[] DataPropertyName = "\"data\""u8.ToArray();
    private static readonly byte[] ReplacementDataProperty =
        "\"data\":\"AA==\""u8.ToArray();

    private readonly GoogleInteractionsResponseParser _responseParser;
    private readonly GoogleInteractionsFailureClassifier _failureClassifier;
    private readonly int _maximumFilteredResponseBytes;
    private readonly int _maximumStructureDepth;
    private readonly int _maximumDiagnosticTextCharacters;
    private readonly MemoryStream _filteredResponse = new();
    private readonly List<byte> _candidate = [];
    private AnalyzerState _state;
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
        foreach (byte value in content)
        {
            ProcessByte(value);
        }
    }

    public ProviderGenerationSummary Complete()
    {
        FlushCandidate();

        if (_state is AnalyzerState.SkipDataString
            or AnalyzerState.SkipDataEscape
            or AnalyzerState.NormalInsideString
            or AnalyzerState.NormalEscape
            or AnalyzerState.CandidateDataKey
            or AnalyzerState.AfterDataKey)
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider returned malformed JSON.");
        }

        string filteredJson = Encoding.UTF8.GetString(
            _filteredResponse.GetBuffer(),
            0,
            checked((int)_filteredResponse.Length));

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
            GoogleInteractionsResult result =
                _responseParser.Parse(filteredJson);
            string? state = ExtractState(root);

            return new ProviderGenerationSummary(
                state,
                result.Images.Count,
                result.Images.Select(image => image.ContentType).ToList(),
                result.Usage);
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

    private void ProcessByte(byte value)
    {
        switch (_state)
        {
            case AnalyzerState.NormalOutsideString:
                ProcessOutsideString(value);
                break;
            case AnalyzerState.NormalInsideString:
                WriteByte(value);
                _state = value switch
                {
                    (byte)'\\' => AnalyzerState.NormalEscape,
                    (byte)'"' => AnalyzerState.NormalOutsideString,
                    _ => AnalyzerState.NormalInsideString
                };
                break;
            case AnalyzerState.NormalEscape:
                WriteByte(value);
                _state = AnalyzerState.NormalInsideString;
                break;
            case AnalyzerState.CandidateDataKey:
                ProcessCandidate(value);
                break;
            case AnalyzerState.AfterDataKey:
                ProcessAfterDataKey(value);
                break;
            case AnalyzerState.SkipDataString:
                _state = value switch
                {
                    (byte)'\\' => AnalyzerState.SkipDataEscape,
                    (byte)'"' => AnalyzerState.NormalOutsideString,
                    _ => AnalyzerState.SkipDataString
                };
                break;
            case AnalyzerState.SkipDataEscape:
                _state = AnalyzerState.SkipDataString;
                break;
            default:
                throw new InvalidOperationException("Unknown analyzer state.");
        }
    }

    private void ProcessOutsideString(byte value)
    {
        if (value != (byte)'"')
        {
            WriteByte(value);
            return;
        }

        _candidate.Clear();
        _candidate.Add(value);
        _candidateIndex = 1;
        _state = AnalyzerState.CandidateDataKey;
    }

    private void ProcessCandidate(byte value)
    {
        _candidate.Add(value);

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

        FlushCandidate();
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
            _candidate.Add(value);
            return;
        }

        if (!_colonSeen && value == (byte)':')
        {
            _candidate.Add(value);
            _colonSeen = true;
            return;
        }

        if (_colonSeen && value == (byte)'"')
        {
            WriteBytes(ReplacementDataProperty);
            _candidate.Clear();
            _state = AnalyzerState.SkipDataString;
            return;
        }

        _candidate.Add(value);
        FlushCandidate();
        _state = AnalyzerState.NormalOutsideString;
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

    private void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _filteredResponse.WriteByte(value);
    }

    private void WriteBytes(ReadOnlySpan<byte> values)
    {
        EnsureCapacity(values.Length);
        _filteredResponse.Write(values);
    }

    private void EnsureCapacity(int additionalBytes)
    {
        if (_filteredResponse.Length + additionalBytes
            > _maximumFilteredResponseBytes)
        {
            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider response metadata exceeded its limit.");
        }
    }

    private static string? ExtractState(JsonElement root)
    {
        if (GoogleInteractionsJsonElementReader.TryGetStringProperty(
                root,
                "status",
                out string? status))
        {
            return status;
        }

        return GoogleInteractionsJsonElementReader.TryGetStringProperty(
            root,
            "state",
            out string? state)
            ? state
            : null;
    }

    private static bool IsJsonWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
    }

    private enum AnalyzerState
    {
        NormalOutsideString,
        NormalInsideString,
        NormalEscape,
        CandidateDataKey,
        AfterDataKey,
        SkipDataString,
        SkipDataEscape
    }
}
