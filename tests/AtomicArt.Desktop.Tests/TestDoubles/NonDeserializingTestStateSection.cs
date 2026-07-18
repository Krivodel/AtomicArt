using System.Text.Json;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal sealed class NonDeserializingTestStateSection : TestStateSectionTestDouble
{
    public override object DeserializePayload(
        int schemaVersion,
        JsonElement payload,
        JsonSerializerOptions options)
    {
        throw new NotSupportedException("State deserialization is not used by this test.");
    }
}
