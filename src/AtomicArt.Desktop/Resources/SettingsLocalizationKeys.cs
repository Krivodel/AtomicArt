namespace AtomicArt.Desktop.Resources;

public static class SettingsLocalizationKeys
{
    public const string Title = "Settings.Title";

    public static class Sections
    {
        public const string Connection = "Settings.Sections.Connection";
        public const string Appearance = "Settings.Sections.Appearance";
        public const string StorageAndPerformance =
            "Settings.Sections.StorageAndPerformance";
    }

    public static class Language
    {
        public const string Label = "Settings.Language.Label";
        public const string SearchPlaceholder =
            "Settings.Language.SearchPlaceholder";
    }

    public static class ApiBaseAddress
    {
        public const string Label = "Settings.ApiBaseAddress.Label";
        public const string Placeholder = "Settings.ApiBaseAddress.Placeholder";
        public const string Invalid = "Settings.ApiBaseAddress.Invalid";
    }

    public static class GoogleApiKey
    {
        public const string Label = "Settings.GoogleApiKey.Label";
    }

    public static class Appearance
    {
        public const string ScaleLabel = "Settings.Appearance.ScaleLabel";
        public const string PromptTextSizeLabel =
            "Settings.Appearance.PromptTextSizeLabel";
    }

    public static class GpuCache
    {
        public const string Label = "Settings.GpuCache.Label";
        public const string RestartNotice = "Settings.GpuCache.RestartNotice";
        public const string MegabytesFormat = "Settings.GpuCache.MegabytesFormat";
    }

    public static class DataRoot
    {
        public const string Label = "Settings.DataRoot.Label";
        public const string PickerTitle = "Settings.DataRoot.PickerTitle";
        public const string InitialSelectionMessage =
            "Settings.DataRoot.InitialSelectionMessage";
        public const string Preparing = "Settings.DataRoot.Preparing";
        public const string Copying = "Settings.DataRoot.Copying";
        public const string Verifying = "Settings.DataRoot.Verifying";
        public const string Switching = "Settings.DataRoot.Switching";
        public const string Cleaning = "Settings.DataRoot.Cleaning";
        public const string Completed = "Settings.DataRoot.Completed";
        public const string MigrationFailed = "Settings.DataRoot.MigrationFailed";
        public const string CleanupFailed = "Settings.DataRoot.CleanupFailed";
    }
}
