using System.Text.Json;

using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal abstract class TestStateSectionTestDouble : IStateSection
{
    public const string DefaultValue = "default";

    public string Key => "test";
    public string FileName => OwnedFileName;
    public int SchemaVersion => 1;
    public Type PayloadType => typeof(TestState);
    public string OwnedFileName { get; init; } = "test.json";

    public object CreateDefaultPayload()
    {
        return CreateDefaultState();
    }

    public abstract object DeserializePayload(
        int schemaVersion,
        JsonElement payload,
        JsonSerializerOptions options);

    protected static TestState CreateDefaultState()
    {
        return new TestState(DefaultValue);
    }
}
