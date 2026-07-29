namespace AtomicArt.Desktop.Resources;

public static class UpdateLocalizationKeys
{
    public const string Title = "Updates.Title";
    public const string AvailableFormat = "Updates.AvailableFormat";

    public static class Actions
    {
        public const string Install = "Updates.Actions.Install";
        public const string WaitAndInstall = "Updates.Actions.WaitAndInstall";
        public const string Later = "Updates.Actions.Later";
    }

    public static class States
    {
        public const string WaitingForGeneration =
            "Updates.States.WaitingForGeneration";
        public const string Downloading = "Updates.States.Downloading";
        public const string Restarting = "Updates.States.Restarting";
    }

    public static class Errors
    {
        public const string CheckFailed = "Updates.Errors.CheckFailed";
        public const string InstallFailed = "Updates.Errors.InstallFailed";
    }
}
