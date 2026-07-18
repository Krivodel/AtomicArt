using Pica.Protocol;

namespace Pica.Client;

public sealed class StandardLocationPicaExecutableSource : IPicaExecutableSource
{
    public IEnumerable<string> GetCandidatePaths()
    {
        if (OperatingSystem.IsMacOS())
        {
            foreach (string candidatePath in GetMacOsCandidatePaths())
            {
                yield return candidatePath;
            }

            yield break;
        }

        if (OperatingSystem.IsLinux())
        {
            foreach (string candidatePath in GetLinuxCandidatePaths())
            {
                yield return candidatePath;
            }

            yield break;
        }

        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (!string.IsNullOrWhiteSpace(localApplicationData))
        {
            yield return Path.Combine(
                localApplicationData,
                "Programs",
                PicaProtocolConstants.ApplicationName,
                PicaProtocolConstants.ExecutableName);
        }

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(
                programFiles,
                PicaProtocolConstants.ApplicationName,
                PicaProtocolConstants.ExecutableName);
        }
    }

    private static IEnumerable<string> GetMacOsCandidatePaths()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string bundleExecutablePath = Path.Combine(
            $"{PicaProtocolConstants.ApplicationName}.app",
            "Contents",
            "MacOS",
            PicaProtocolConstants.ExecutableName);

        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, "Applications", bundleExecutablePath);
        }

        yield return Path.Combine(Path.DirectorySeparatorChar.ToString(), "Applications", bundleExecutablePath);
    }

    private static IEnumerable<string> GetLinuxCandidatePaths()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(
                userProfile,
                ".local",
                "bin",
                PicaProtocolConstants.ExecutableName);
            yield return Path.Combine(
                userProfile,
                ".local",
                "share",
                PicaProtocolConstants.ApplicationName,
                PicaProtocolConstants.ExecutableName);
        }

        yield return Path.Combine(
            Path.DirectorySeparatorChar.ToString(),
            "usr",
            "local",
            "bin",
            PicaProtocolConstants.ExecutableName);
        yield return Path.Combine(
            Path.DirectorySeparatorChar.ToString(),
            "usr",
            "bin",
            PicaProtocolConstants.ExecutableName);
        yield return Path.Combine(
            Path.DirectorySeparatorChar.ToString(),
            "opt",
            PicaProtocolConstants.ApplicationName,
            PicaProtocolConstants.ExecutableName);
    }
}
