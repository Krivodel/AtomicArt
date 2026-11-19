namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

internal static class LocalCodexInstallation
{
    public const string HomeVariable = "CODEX_HOME";
    private static string ExecutableName => OperatingSystem.IsWindows() ? "codex.exe" : "codex";

    public static (string Executable, string HomeDirectory) Resolve(LocalCodexOptions options)
    {
        string? home = Environment.GetEnvironmentVariable(HomeVariable);
        if (string.IsNullOrWhiteSpace(home) && OperatingSystem.IsWindows())
        {
            home = Environment.GetEnvironmentVariable(HomeVariable, EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable(HomeVariable, EnvironmentVariableTarget.Machine);
        }

        return Resolve(options, home, Environment.GetEnvironmentVariable("PATH"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    internal static (string Executable, string HomeDirectory) Resolve(
        LocalCodexOptions options, string? environmentHome, string? searchPath, string userProfile)
    {
        string? folder = ResolveRequestedDirectory(options.Folder);
        string? legacyHome = ResolveRequestedDirectory(options.HomeDirectory);
        string? configuredHome = environmentHome is not null && IsDirectory(environmentHome)
            ? Path.GetFullPath(environmentHome)
            : null;
        string? folderExecutable = FindExecutable(folder);
        string executable = ResolveLegacyExecutable(options.ExecutablePath)
            ?? folderExecutable
            ?? FindExecutable(configuredHome)
            ?? FindOnPath(searchPath)
            ?? throw Unavailable("Codex executable was not found.");
        string home = legacyHome
            ?? ResolveFolderHome(folder, folderExecutable)
            ?? configuredHome
            ?? FindBundledHome(executable)
            ?? Path.Combine(userProfile, ".codex");

        return (executable, home);
    }

    private static string? ResolveFolderHome(string? folder, string? executable)
    {
        if (folder is null || executable is null)
        {
            return folder;
        }

        if (File.Exists(Path.Combine(folder, "auth.json"))
            || File.Exists(Path.Combine(folder, "config.toml")))
        {
            return folder;
        }

        return FindBundledHome(executable);
    }

    private static string? FindBundledHome(string executable)
    {
        DirectoryInfo? root = new FileInfo(executable).Directory?.Parent?.Parent?.Parent;
        if (root is null || !string.Equals(DesktopExecutable(root.FullName), executable, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string home = Path.Combine(root.FullName, HomeVariable);
        return Directory.Exists(home) ? home : null;
    }

    private static string? FindExecutable(string? directory)
    {
        if (directory is null)
        {
            return null;
        }

        string direct = Path.Combine(directory, ExecutableName);
        if (File.Exists(direct))
        {
            return direct;
        }

        string bundled = DesktopExecutable(directory);
        if (File.Exists(bundled))
        {
            return bundled;
        }

        DirectoryInfo folder = new(directory);
        if (folder.Name.Equals(HomeVariable, StringComparison.OrdinalIgnoreCase) && folder.Parent is { } parent)
        {
            string sibling = DesktopExecutable(parent.FullName);
            return File.Exists(sibling) ? sibling : null;
        }

        return null;
    }

    private static string DesktopExecutable(string root)
    {
        return Path.Combine(root, "versions", "current", "resources", ExecutableName);
    }

    private static string? FindOnPath(string? searchPath)
    {
        foreach (string directory in (searchPath ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Path.IsPathFullyQualified(directory))
            {
                string file = Path.Combine(directory, ExecutableName);
                if (File.Exists(file))
                {
                    return Path.GetFullPath(file);
                }
            }
        }

        return null;
    }

    private static string? ResolveRequestedDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return IsDirectory(path) ? Path.GetFullPath(path) : throw Unavailable("Codex folder was not found.");
    }

    private static bool IsDirectory(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && Directory.Exists(path);
    }

    private static string? ResolveLegacyExecutable(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (Path.IsPathFullyQualified(path)
            && (!OperatingSystem.IsWindows() || Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            && File.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        throw Unavailable("Codex executable was not found.");
    }

    private static GenerationAttemptException Unavailable(string message)
    {
        return new GenerationAttemptException(message, GenerationClientFailureCodes.CodexUnavailable, false);
    }
}
