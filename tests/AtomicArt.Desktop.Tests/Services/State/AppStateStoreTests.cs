using System.Text.Json;

using Microsoft.Extensions.Logging;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.State;

public sealed class AppStateStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsDefaultState()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(typeof(AppStateStoreTests));

        try
        {
            AppStateStore store = CreateStore(rootDirectory);
            TestStateSection section = new();

            TestState state = await store.LoadAsync<TestState>(section, CancellationToken.None);

            state.Value.Should().Be(TestStateSection.DefaultValue);
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadAsync_WithInvalidJson_ReturnsDefaultStateAndLogsWarning()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(typeof(AppStateStoreTests));

        try
        {
            LoadFailureTestContext context = CreateLoadFailureContext(rootDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(
                    context.PathProvider.StateDirectory,
                    context.Section.FileName),
                "{ invalid json",
                CancellationToken.None);

            await AssertLoadFailureAsync(context);
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WithValidState_WritesUtf8JsonEnvelope()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(typeof(AppStateStoreTests));

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            AppStateStore store = CreateStore(pathProvider);
            TestStateSection section = new();
            TestState state = new("saved");

            await store.SaveAsync(section, state, CancellationToken.None);

            string statePath = Path.Combine(pathProvider.StateDirectory, section.FileName);
            byte[] bytes = await File.ReadAllBytesAsync(statePath, CancellationToken.None);
            bytes.Take(3).Should().NotEqual([0xEF, 0xBB, 0xBF]);
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            root.GetProperty("schemaVersion").GetInt32().Should().Be(section.SchemaVersion);
            root.GetProperty("savedAtUtc").GetDateTimeOffset().Offset.Should().Be(TimeSpan.Zero);
            root.GetProperty("payload").GetProperty("value").GetString().Should().Be(state.Value);
            Directory.GetFiles(pathProvider.StateDirectory, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenWriteFails_KeepsPreviousStateFile()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(typeof(AppStateStoreTests));

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            AppStateStore store = CreateStore(pathProvider);
            TestStateSection section = new();
            string statePath = Path.Combine(pathProvider.StateDirectory, section.FileName);
            await store.SaveAsync(section, new TestState("previous"), CancellationToken.None);
            
            await using FileStream lockedStateFile = new(
                statePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            Func<Task> act = () => store.SaveAsync(section, new TestState("partial"), CancellationToken.None);

            await act.Should().ThrowAsync<IOException>();
            lockedStateFile.Dispose();
            TestState state = await store.LoadAsync<TestState>(section, CancellationToken.None);
            state.Value.Should().Be("previous");
            Directory.GetFiles(pathProvider.StateDirectory, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WithSectionFileName_WritesSectionOwnedFileName()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(typeof(AppStateStoreTests));

        try
        {
            AtomicArtDataPathProvider pathProvider = new(rootDirectory);
            AppStateStore store = CreateStore(pathProvider);
            TestStateSection section = new()
            {
                OwnedFileName = "owned-section-name.json"
            };

            await store.SaveAsync(section, new TestState("saved"), CancellationToken.None);

            File.Exists(Path.Combine(pathProvider.StateDirectory, section.FileName)).Should().BeTrue();
            File.Exists(Path.Combine(pathProvider.StateDirectory, string.Concat(section.Key, ".json")))
                .Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task LoadAsync_WithUnsupportedSchemaVersion_ReturnsDefaultStateAndLogsWarning()
    {
        string rootDirectory = TestDirectories.GetUniqueDirectoryPath(typeof(AppStateStoreTests));

        try
        {
            LoadFailureTestContext context = CreateLoadFailureContext(rootDirectory);
            StateEnvelope<TestState> envelope = new StateEnvelope<TestState>
            {
                SchemaVersion = context.Section.SchemaVersion + 1,
                SavedAtUtc = new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero),
                Payload = new TestState("unsupported")
            };
            string statePath = Path.Combine(
                context.PathProvider.StateDirectory,
                context.Section.FileName);
            await using (FileStream stream = File.Create(statePath))
            {
                await JsonSerializer.SerializeAsync(stream, envelope, cancellationToken: CancellationToken.None);
            }

            await AssertLoadFailureAsync(context);
        }
        finally
        {
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    private static LoadFailureTestContext CreateLoadFailureContext(
        string rootDirectory)
    {
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        RecordingLogger<AppStateStore> logger = new();
        AppStateStore store = CreateStore(pathProvider, logger);
        TestStateSection section = new();
        Directory.CreateDirectory(pathProvider.StateDirectory);

        return new LoadFailureTestContext(
            pathProvider,
            logger,
            store,
            section);
    }

    private static AppStateStore CreateStore(string rootDirectory)
    {
        return CreateStore(new AtomicArtDataPathProvider(rootDirectory));
    }

    private static AppStateStore CreateStore(
        AtomicArtDataPathProvider pathProvider,
        ILogger<AppStateStore>? logger = null)
    {
        return new AppStateStore(
            pathProvider,
            logger ?? new RecordingLogger<AppStateStore>());
    }

    private static async Task AssertLoadFailureAsync(
        LoadFailureTestContext context)
    {
        TestState state = await context.Store.LoadAsync<TestState>(
            context.Section,
            CancellationToken.None);

        state.Value.Should().Be(TestStateSection.DefaultValue);
        context.Logger.WarningCount.Should().Be(1);
    }

    private sealed class LoadFailureTestContext
    {
        public AtomicArtDataPathProvider PathProvider { get; }
        public RecordingLogger<AppStateStore> Logger { get; }
        public AppStateStore Store { get; }
        public TestStateSection Section { get; }

        public LoadFailureTestContext(
            AtomicArtDataPathProvider pathProvider,
            RecordingLogger<AppStateStore> logger,
            AppStateStore store,
            TestStateSection section)
        {
            ArgumentNullException.ThrowIfNull(pathProvider);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(section);

            PathProvider = pathProvider;
            Logger = logger;
            Store = store;
            Section = section;
        }
    }

    private sealed class TestStateSection : TestStateSectionTestDouble
    {
        public override object DeserializePayload(
            int schemaVersion,
            JsonElement payload,
            JsonSerializerOptions options)
        {
            TestState? state = payload.Deserialize<TestState>(options);

            return state ?? CreateDefaultState();
        }
    }
}
