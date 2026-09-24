namespace AtomicArt.Desktop.Resources;

public static class Dlss5LocalizationKeys
{
    public const string Title = "Dlss5.Title";
    public const string SelectImage = "Dlss5.SelectImage";
    public const string PasteImage = "Dlss5.PasteImage";
    public const string Off = "Dlss5.Off";
    public const string On = "Dlss5.On";
    public const string Downloading = "Dlss5.Downloading";
    public const string Starting = "Dlss5.Starting";

    public static class Errors
    {
        public const string SourceFailed = "Dlss5.Errors.SourceFailed";
        public const string InstallFailed = "Dlss5.Errors.InstallFailed";
        public const string RenderFailed = "Dlss5.Errors.RenderFailed";
    }

    public static class Install
    {
        public const string Title = "Dlss5.Install.Title";
        public const string Message = "Dlss5.Install.Message";
        public const string Confirm = "Dlss5.Install.Confirm";
    }

    public static class InstallCompleted
    {
        public const string Title = "Dlss5.InstallCompleted.Title";
        public const string Message = "Dlss5.InstallCompleted.Message";
        public const string Okay = "Dlss5.InstallCompleted.Okay";
        public const string Launch = "Dlss5.InstallCompleted.Launch";
    }

    public static class Controls
    {
        public const string Style = "Dlss5.Controls.Style";
        public const string GeneralIntensity = "Dlss5.Controls.GeneralIntensity";
        public const string LocalStructureIntensity = "Dlss5.Controls.LocalStructureIntensity";
        public const string SkinStructureStrength = "Dlss5.Controls.SkinStructureStrength";
        public const string LocalToneStrength = "Dlss5.Controls.LocalToneStrength";
        public const string Default = "Dlss5.Controls.Default";
        public const string Natural = "Dlss5.Controls.Natural";
        public const string Cinematic = "Dlss5.Controls.Cinematic";

        public static IReadOnlyList<string> StyleOptionKeys { get; } =
            Array.AsReadOnly(new string[] { Default, Natural, Cinematic });
    }
}
