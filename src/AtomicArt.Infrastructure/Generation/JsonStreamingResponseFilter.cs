using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace AtomicArt.Infrastructure.Generation;

internal sealed class JsonStreamingResponseFilter : IDisposable
{
    private static readonly byte[] Quote = "\""u8.ToArray();
    private static readonly SearchValues<byte> JsonWhitespace = SearchValues.Create(" \t\r\n"u8);
    private static readonly SearchValues<byte> StringTerminators =
        SearchValues.Create(new byte[] { (byte)'"', (byte)'\\' });

    private readonly (byte[] PropertyName, byte[] ReplacementValue, bool Sanitize)[] _replacements;
    private readonly int _maximumFilteredResponseBytes;
    private readonly Func<string, Exception> _createInvalidResponseException;
    private readonly MemoryStream _filteredResponse = new();
    private readonly List<byte> _candidate = [];
    private AnalyzerState _state;
    private int _candidateProperty = -1;
    private int _skippedProperty = -1;
    private bool _colonSeen;

    public JsonStreamingResponseFilter(
        IReadOnlyDictionary<string, (string ReplacementValue, bool Sanitize)> replacements,
        int maximumFilteredResponseBytes,
        Func<string, Exception> createInvalidResponseException)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFilteredResponseBytes, 1);
        _createInvalidResponseException = createInvalidResponseException
            ?? throw new ArgumentNullException(nameof(createInvalidResponseException));
        _maximumFilteredResponseBytes = maximumFilteredResponseBytes;
        _replacements = replacements.Select(replacement => (
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(replacement.Key)),
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(replacement.Value.ReplacementValue)),
            replacement.Value.Sanitize)).ToArray();
    }

    public void Append(ReadOnlySpan<byte> content)
    {
        ClientResponseWriter clientResponseWriter = new(Span<byte>.Empty, false);
        Append(content, ref clientResponseWriter);
    }

    public int AppendAndSanitize(Span<byte> content)
    {
        ClientResponseWriter clientResponseWriter = new(content, true);
        Append(content, ref clientResponseWriter);

        return clientResponseWriter.WrittenCount;
    }

    public ReadOnlyMemory<byte> Complete()
    {
        FlushCandidate();

        if (_state != AnalyzerState.NormalOutsideString)
        {
            throw _createInvalidResponseException("The generation provider returned malformed JSON.");
        }

        return _filteredResponse.GetBuffer().AsMemory(0, checked((int)_filteredResponse.Length));
    }

    public void Dispose()
    {
        _filteredResponse.Dispose();
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
            _candidateProperty = FindCandidatePropertyIndex();

            if (_candidateProperty >= 0)
            {
                if (_candidate.Count == _replacements[_candidateProperty].PropertyName.Length)
                {
                    _colonSeen = false;
                    _state = AnalyzerState.AfterSkippedKey;
                }

                continue;
            }

            FlushCandidate();
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
            _candidateProperty = -1;
            _state = AnalyzerState.SkipStringValue;

            return consumedBytes;
        }

        AppendCandidate(content.Slice(whitespaceLength, 1));
        clientResponseWriter.Write(content.Slice(whitespaceLength, 1));
        FlushCandidate();
        _candidateProperty = -1;
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
            if (_replacements[_skippedProperty].Sanitize)
            {
                clientResponseWriter.Write(
                    content.Slice(terminatorIndex, 1));
            }

            _skippedProperty = -1;
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
        AppendCandidate(Quote);
        clientResponseWriter.Write(Quote);
        _candidateProperty = -1;
        _state = AnalyzerState.CandidateSkippedKey;
    }

    private int FindCandidatePropertyIndex()
    {
        ReadOnlySpan<byte> candidate = CollectionsMarshal.AsSpan(_candidate);

        for (int index = 0; index < _replacements.Length; index++)
        {
            if (_replacements[index].PropertyName.AsSpan().StartsWith(candidate))
            {
                return index;
            }
        }

        return -1;
    }

    private void WriteReplacementValue()
    {
        WriteBytes(_replacements[_candidateProperty].ReplacementValue);
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
        if (!_replacements[_skippedProperty].Sanitize)
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
            throw _createInvalidResponseException("The generation provider response metadata exceeded its limit.");
        }
    }

    private void EnsureFilteredResponseCapacity(int additionalBytes)
    {
        if (_filteredResponse.Length + additionalBytes
            > _maximumFilteredResponseBytes)
        {
            throw _createInvalidResponseException("The generation provider response metadata exceeded its limit.");
        }
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
