using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Lang.Avalonia;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Localization;

public sealed class LocalizationServiceTests : IDisposable
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    private readonly CultureInfo _originalCulture;
    private readonly CultureInfo _originalUiCulture;
    private readonly CultureInfo? _originalDefaultCulture;
    private readonly CultureInfo? _originalDefaultUiCulture;

    public LocalizationServiceTests()
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUiCulture = CultureInfo.CurrentUICulture;
        _originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        _originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    }

    [Fact]
    public void Constructor_WithRussianSystemCulture_RegistersBuiltInBeforeCatalogRefresh()
    {
        SetCurrentCulture(new CultureInfo("ru-RU"));
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(Constructor_WithRussianSystemCulture_RegistersBuiltInBeforeCatalogRefresh));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);

        using LocalizationService service = CreateService(pathProvider);

        service.CurrentLocalization.Should().BeNull();
        service.CurrentCulture?.Name.Should().Be("ru-RU");
        service.Get(CommonLocalizationKeys.Copy).Should().Be("Копировать");
    }

    [Fact]
    public async Task RefreshAvailableLocalizationsAsync_WithEmptyDirectory_LoadsBuiltInsAndCreatesTemplate()
    {
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(RefreshAvailableLocalizationsAsync_WithEmptyDirectory_LoadsBuiltInsAndCreatesTemplate));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        using LocalizationService service = CreateService(pathProvider);

        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);

        service.AvailableLocalizations.Select(option => option.Id).Should().Equal(
            LocalizationConstants.RussianId,
            LocalizationConstants.EnglishId);
        service.AvailableLocalizations.Should().OnlyContain(option => option.IsBuiltIn);
        File.Exists(Path.Combine(
            pathProvider.LocalizationsDirectory,
            LocalizationConstants.TemplateFileName)).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAvailableLocalizationsAsync_WithCustomVariantsUsingSameCulture_KeepsFilenameIdentity()
    {
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(RefreshAvailableLocalizationsAsync_WithCustomVariantsUsingSameCulture_KeepsFilenameIdentity));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        await WriteLocalizationAsync(
            pathProvider,
            "Deutsch",
            "de-DE",
            """{"Common":{"Copy":"Kopieren"}}""");
        await WriteLocalizationAsync(
            pathProvider,
            "Deutsch förmlich",
            "de-DE",
            """{"Common":{"Copy":"Kopieren, bitte"}}""");
        using LocalizationService service = CreateService(pathProvider);

        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);

        service.AvailableLocalizations.Should().Contain(option =>
            option.Id == "Deutsch"
            && option.Culture.Name == "de-DE"
            && !option.IsBuiltIn);
        service.AvailableLocalizations.Should().Contain(option =>
            option.Id == "Deutsch förmlich"
            && option.Culture.Name == "de-DE"
            && !option.IsBuiltIn);

        service.Select("Deutsch");
        service.Get(CommonLocalizationKeys.Copy).Should().Be("Kopieren");

        service.Select("Deutsch förmlich");
        service.Get(CommonLocalizationKeys.Copy).Should().Be("Kopieren, bitte");
    }

    [Fact]
    public async Task Select_WithCustomVariantUsingBuiltInCulture_KeepsFilenameIdentity()
    {
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(Select_WithCustomVariantUsingBuiltInCulture_KeepsFilenameIdentity));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        await WriteLocalizationAsync(
            pathProvider,
            "English formal",
            "en-US",
            """{"Common":{"Copy":"Copy, please"}}""");
        using LocalizationService service = CreateService(pathProvider);
        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);

        service.Select("English formal");

        service.CurrentLocalization?.Id.Should().Be("English formal");
        service.CurrentCulture?.Name.Should().Be("en-US");
        service.Get(CommonLocalizationKeys.Copy).Should().Be("Copy, please");
    }

    [Fact]
    public async Task Select_WithPartialCustomLocalization_UsesEnglishFallbackAndIgnoresUnknownKeys()
    {
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(Select_WithPartialCustomLocalization_UsesEnglishFallbackAndIgnoresUnknownKeys));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        await WriteLocalizationAsync(
            pathProvider,
            "Deutsch",
            "de-DE",
            """{"Common":{"Copy":"Kopieren","NewerOnly":"Neu"}}""");
        RecordingLogger<LocalizationService> logger = new();
        using LocalizationService service = CreateService(pathProvider, logger);
        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);

        service.Select("Deutsch");

        service.Get(CommonLocalizationKeys.Copy).Should().Be("Kopieren");
        service.Get(CommonLocalizationKeys.Error).Should().Be("Error");
        service.Get("Common.NewerOnly").Should().Be("Common.NewerOnly");
        I18nManager.Instance.GetResource(CommonLocalizationKeys.Copy)
            .Should()
            .Be(service.Get(CommonLocalizationKeys.Copy));
        I18nManager.Instance.GetResource(CommonLocalizationKeys.Error)
            .Should()
            .Be(service.Get(CommonLocalizationKeys.Error));
        CultureInfo.CurrentCulture.Name.Should().Be("de-DE");
        1.5m.ToString("0.0", CultureInfo.CurrentCulture).Should().Be("1,5");
        logger.WarningMessages.Should().Contain(message =>
            message.Contains("Common.NewerOnly", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAvailableLocalizationsAsync_WithInvalidFiles_SkipsEachFileAndKeepsValidOnes()
    {
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(RefreshAvailableLocalizationsAsync_WithInvalidFiles_SkipsEachFileAndKeepsValidOnes));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        await WriteLocalizationAsync(
            pathProvider,
            "Valid",
            "uk-UA",
            """{"Common":{"Copy":"Копіювати"}}""");
        await WriteLocalizationAsync(
            pathProvider,
            "Bad schema",
            "uk-UA",
            """{"Common":{"Copy":"Копіювати"}}""",
            schemaVersion: 2);
        await WriteLocalizationAsync(
            pathProvider,
            "Bad culture",
            "not a culture",
            """{"Common":{"Copy":"Copy"}}""");
        await WriteRawLocalizationAsync(
            pathProvider,
            "Bad strings",
            """{"schemaVersion":1,"culture":"en-US","strings":[]}""");
        await WriteLocalizationAsync(
            pathProvider,
            LocalizationConstants.EnglishId,
            "en-US",
            """{"Common":{"Copy":"Override"}}""");
        await WriteRawLocalizationAsync(
            pathProvider,
            string.Empty,
            """{"schemaVersion":1,"culture":"en-US","strings":{}}""");
        byte[] oversizedContent = new byte[LocalizationConstants.MaximumFileBytes + 1];
        await File.WriteAllBytesAsync(
            Path.Combine(pathProvider.LocalizationsDirectory, "Oversized.json"),
            oversizedContent);
        RecordingLogger<LocalizationService> logger = new();
        using LocalizationService service = CreateService(pathProvider, logger);

        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);

        service.AvailableLocalizations.Select(option => option.Id).Should().BeEquivalentTo(
            LocalizationConstants.RussianId,
            LocalizationConstants.EnglishId,
            "Valid");
        logger.WarningCount.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task RefreshAvailableLocalizationsAsync_WithCurrentFileBecomingInvalid_FallsBackToEnglish()
    {
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(RefreshAvailableLocalizationsAsync_WithCurrentFileBecomingInvalid_FallsBackToEnglish));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        await WriteLocalizationAsync(
            pathProvider,
            "Deutsch",
            "de-DE",
            """{"Common":{"Copy":"Kopieren"}}""");
        using LocalizationService service = CreateService(pathProvider);
        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);
        service.Select("Deutsch");
        await WriteRawLocalizationAsync(pathProvider, "Deutsch", "{");

        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);
        service.ReconcileCurrentOrSystemDefault();

        service.CurrentLocalization?.Id.Should().Be(LocalizationConstants.EnglishId);
        service.Get(CommonLocalizationKeys.Copy).Should().Be("Copy");
    }

    [Fact]
    public async Task SelectSavedOrEnglishFallback_WithUnavailableSavedLocalization_SelectsEnglish()
    {
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(SelectSavedOrEnglishFallback_WithUnavailableSavedLocalization_SelectsEnglish));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        using LocalizationService service = CreateService(pathProvider);
        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);

        service.SelectSavedOrEnglishFallback("Deleted localization");

        service.CurrentLocalization?.Id.Should().Be(LocalizationConstants.EnglishId);
        service.Get(CommonLocalizationKeys.Copy).Should().Be("Copy");
    }

    [Fact]
    public async Task ReconcileCurrentOrSystemDefault_WithMatchingCustomSystemCulture_SelectsCustomLocalization()
    {
        SetCurrentCulture(new CultureInfo("ja-JP"));
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(ReconcileCurrentOrSystemDefault_WithMatchingCustomSystemCulture_SelectsCustomLocalization));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        await WriteLocalizationAsync(
            pathProvider,
            "日本語",
            "ja-JP",
            """{"Common":{"Copy":"コピー"}}""");
        using LocalizationService service = CreateService(pathProvider);
        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);

        service.ReconcileCurrentOrSystemDefault();

        service.CurrentLocalization?.Id.Should().Be("日本語");
        service.Get(CommonLocalizationKeys.Copy).Should().Be("コピー");
    }

    [Fact]
    public async Task ReconcileCurrentOrSystemDefault_WithoutMatchingSystemCulture_SelectsEnglish()
    {
        SetCurrentCulture(new CultureInfo("ja-JP"));
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(ReconcileCurrentOrSystemDefault_WithoutMatchingSystemCulture_SelectsEnglish));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        using LocalizationService service = CreateService(pathProvider);
        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);

        service.ReconcileCurrentOrSystemDefault();

        service.CurrentLocalization?.Id.Should().Be(LocalizationConstants.EnglishId);
    }

    [Fact]
    public async Task RefreshAvailableLocalizationsAsync_WithUnchangedTemplate_DoesNotRewriteTemplate()
    {
        string rootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            nameof(RefreshAvailableLocalizationsAsync_WithUnchangedTemplate_DoesNotRewriteTemplate));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        using LocalizationService service = CreateService(pathProvider);
        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);
        string templatePath = Path.Combine(
            pathProvider.LocalizationsDirectory,
            LocalizationConstants.TemplateFileName);
        string template = await File.ReadAllTextAsync(templatePath, Utf8WithoutBom);
        DateTime fixedWriteTimeUtc = new(
            2020,
            1,
            2,
            3,
            4,
            5,
            DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(templatePath, fixedWriteTimeUtc);
        DateTime storedWriteTimeUtc = File.GetLastWriteTimeUtc(templatePath);

        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);

        template.Should().Contain("\"Copy\": \"Copy\"");
        template.Should().Contain("…");
        template.Should().NotContain("\\u2026");
        File.GetLastWriteTimeUtc(templatePath).Should().Be(storedWriteTimeUtc);
        service.AvailableLocalizations.Should().NotContain(option =>
            option.Id == Path.GetFileNameWithoutExtension(
                LocalizationConstants.TemplateFileName));

        using JsonDocument document = JsonDocument.Parse(template);
        document.RootElement.GetProperty("schemaVersion").GetInt32()
            .Should().Be(LocalizationConstants.SchemaVersion);
        document.RootElement.GetProperty("culture").GetString()
            .Should().Be("en-US");
    }

    [Fact]
    public async Task RefreshAvailableLocalizationsAsync_AfterDataRootSwitch_UsesNewLocalizationDirectory()
    {
        string firstRootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            "DataRootSwitchFirst");
        string secondRootDirectory = DesktopTestDirectories.CreateCleanDirectory(
            "DataRootSwitchSecond");
        AtomicArtDataPathProvider pathProvider = new(firstRootDirectory);
        await WriteLocalizationAsync(
            pathProvider,
            "First",
            "de-DE",
            """{"Common":{"Copy":"First"}}""");
        using LocalizationService service = CreateService(pathProvider);
        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);
        service.Select("First");
        pathProvider.SwitchRootDirectory(secondRootDirectory);
        await WriteLocalizationAsync(
            pathProvider,
            "Second",
            "de-DE",
            """{"Common":{"Copy":"Second"}}""");

        await service.RefreshAvailableLocalizationsAsync(CancellationToken.None);
        service.ReconcileCurrentOrSystemDefault();

        service.AvailableLocalizations.Should().Contain(option => option.Id == "Second");
        service.AvailableLocalizations.Should().NotContain(option => option.Id == "First");
        service.CurrentLocalization?.Id.Should().Be(LocalizationConstants.EnglishId);
        File.Exists(Path.Combine(
            pathProvider.LocalizationsDirectory,
            LocalizationConstants.TemplateFileName)).Should().BeTrue();
    }

    [Fact]
    public void BuiltInLocalizationCatalog_WithDeclaredKeys_ContainsExactlyTheApplicationKeySet()
    {
        IReadOnlySet<string> declaredKeys = GetDeclaredLocalizationKeys();
        BuiltInLocalizationCatalog builtIns = BuiltInLocalizationCatalog.Current;

        builtIns.English.Strings.Keys.Should().BeEquivalentTo(declaredKeys);
        builtIns.Russian.Strings.Keys.Should().BeEquivalentTo(declaredKeys);
    }

    public void Dispose()
    {
        BuiltInLocalizationCatalog builtIns = BuiltInLocalizationCatalog.Current;
        LocalizationTextResolver textResolver = new(
            builtIns.English,
            builtIns.English);
        LocalizationLangPlugin plugin = new(textResolver);
        bool registered = I18nManager.Instance.Register(
            plugin,
            builtIns.English.Culture,
            out string? error);

        if (!registered)
        {
            throw new InvalidOperationException(
                $"Test localization reset failed: {error}");
        }

        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultUiCulture;
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    private static IReadOnlySet<string> GetDeclaredLocalizationKeys()
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        Type[] keyOwners =
        [
            typeof(CommonLocalizationKeys),
            typeof(ShellLocalizationKeys),
            typeof(GenerationUiLocalizationKeys),
            typeof(GalleryLocalizationKeys),
            typeof(SettingsLocalizationKeys),
            typeof(UpdateLocalizationKeys),
            typeof(GenerationLocalizationKeys)
        ];

        foreach (Type keyOwner in keyOwners)
        {
            CollectDeclaredLocalizationKeys(keyOwner, keys);
        }

        return keys;
    }

    private static void CollectDeclaredLocalizationKeys(
        Type type,
        ISet<string> keys)
    {
        foreach (FieldInfo field in type.GetFields(
                     BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral
                && field.FieldType == typeof(string)
                && field.GetRawConstantValue() is string key)
            {
                keys.Add(key);
            }
        }

        foreach (Type nestedType in type.GetNestedTypes(BindingFlags.Public))
        {
            CollectDeclaredLocalizationKeys(nestedType, keys);
        }
    }

    private static LocalizationService CreateService(
        AtomicArtDataPathProvider pathProvider,
        RecordingLogger<LocalizationService>? logger = null)
    {
        return new LocalizationService(
            pathProvider,
            TestApiConfiguration.CreateTrustedFileStreamFactory(),
            logger ?? new RecordingLogger<LocalizationService>(),
            new WeakReferenceMessenger());
    }

    private static async Task WriteLocalizationAsync(
        AtomicArtDataPathProvider pathProvider,
        string localizationId,
        string cultureName,
        string stringsJson,
        int schemaVersion = LocalizationConstants.SchemaVersion)
    {
        string json = $$"""
            {
              "schemaVersion": {{schemaVersion}},
              "culture": "{{cultureName}}",
              "strings": {{stringsJson}}
            }
            """;

        await WriteRawLocalizationAsync(pathProvider, localizationId, json);
    }

    private static async Task WriteRawLocalizationAsync(
        AtomicArtDataPathProvider pathProvider,
        string localizationId,
        string json)
    {
        Directory.CreateDirectory(pathProvider.LocalizationsDirectory);
        string fileName = string.Concat(
            localizationId,
            LocalizationConstants.JsonExtension);
        string filePath = Path.Combine(
            pathProvider.LocalizationsDirectory,
            fileName);

        await File.WriteAllTextAsync(filePath, json, Utf8WithoutBom);
    }

    private static void SetCurrentCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
