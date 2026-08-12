using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.SingleInstance;

internal sealed class SingleInstanceIdentity
{
    public string LockFilePath { get; }
    public string PipeName { get; }

    private const int IdentitySuffixLength = 24;
    private const string LockFileExtension = ".lock";
    private const string PipeNamePrefix = "AtomicArt-";

    public SingleInstanceIdentity(
        string lockFilePath,
        string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        LockFilePath = Path.GetFullPath(lockFilePath);
        PipeName = pipeName;
    }

    public static SingleInstanceIdentity CreateDefault()
    {
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            localApplicationData = Path.GetTempPath();
        }

        string coordinationDirectory = Path.Combine(
            localApplicationData,
            AtomicArtPathNames.RootDirectory,
            AtomicArtPathNames.SingleInstanceCoordinationDirectory);
        string identitySuffix = CreateIdentitySuffix();

        return new SingleInstanceIdentity(
            Path.Combine(
                coordinationDirectory,
                identitySuffix + LockFileExtension),
            PipeNamePrefix + identitySuffix);
    }

    private static string CreateIdentitySuffix()
    {
        string userProfile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        string sessionIdentity = GetSessionIdentity();
        byte[] identityBytes = Encoding.UTF8.GetBytes(
            $"{userProfile}|{sessionIdentity}");
        byte[] identityHash = SHA256.HashData(identityBytes);

        return Convert
            .ToHexString(identityHash)
            .Substring(0, IdentitySuffixLength);
    }

    private static string GetSessionIdentity()
    {
        if (OperatingSystem.IsWindows())
        {
            using Process process = Process.GetCurrentProcess();

            return process.SessionId.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        return GetEnvironmentValue("WAYLAND_DISPLAY")
            ?? GetEnvironmentValue("DISPLAY")
            ?? GetEnvironmentValue("XDG_SESSION_ID")
            ?? "default";
    }

    private static string? GetEnvironmentValue(string variableName)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }
}
