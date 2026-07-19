using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Win32.SafeHandles;

using Pica.Viewer.Services;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Services.Paths;

internal static class TrustedPathGuard
{
    private const string FailureMessageFormat =
        "{0} must stay inside {1} and must not contain reparse points.";
    private const int WindowsErrorFileNotFound = 2;
    private const int WindowsErrorPathNotFound = 3;
    private const int WindowsErrorNotFound = 1168;
    private const int InitialFinalPathBufferLength = 512;
    private const string WindowsNativePathPrefix = @"\??\";
    private const string WindowsUncPathPrefix = @"\\";
    private const string WindowsNativeUncPathPrefix = @"\??\UNC\";
    private const uint WindowsDeleteAccess = 0x00010000;
    private const uint WindowsGenericReadAccess = 0x80000000;
    private const uint WindowsFileListDirectoryAccess = 0x00000001;
    private const uint WindowsFileShareRead = 0x00000001;
    private const uint WindowsFileShareWrite = 0x00000002;
    private const uint WindowsFileFlagBackupSemantics = 0x02000000;
    private const uint WindowsOpenExisting = 3;

    public static string CreateFailureMessage(
        string pathDescription,
        string trustedDirectoryDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedDirectoryDescription);

        return string.Format(
            FailureMessageFormat,
            pathDescription,
            trustedDirectoryDescription);
    }

    public static bool IsInsideDirectory(string trustedDirectory, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string trustedRoot = Path.GetFullPath(trustedDirectory);
        string fullPath = Path.GetFullPath(path);
        string relativePath = Path.GetRelativePath(trustedRoot, fullPath);

        return !Path.IsPathRooted(relativePath)
            && !StartsWithParentDirectorySegment(relativePath);
    }

    public static void EnsureInsideDirectory(
        string trustedDirectory,
        string path,
        string failureMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        if (!IsInsideDirectory(trustedDirectory, path))
        {
            throw new IOException(failureMessage);
        }
    }

    public static void EnsureTrustedDirectoryExists(
        IAtomicArtDataPathProvider pathProvider,
        string directoryPath,
        string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);

        EnsureTrustedDirectoryExists(
            directoryPath,
            pathProvider.EnsureDirectoryExists,
            failureMessage);
    }

    public static void EnsureTrustedDirectoryExists(
        string directoryPath,
        Action<string> ensureDirectoryExists,
        string failureMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(ensureDirectoryExists);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        EnsureNoReparsePointInExistingPath(directoryPath, failureMessage);
        ensureDirectoryExists(directoryPath);
        EnsureNoReparsePointInExistingPath(directoryPath, failureMessage);

        DirectoryInfo directory = new(directoryPath);

        if ((directory.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
        {
            throw new IOException(failureMessage);
        }
    }

    public static void EnsureTrustedWriteTarget(
        string trustedDirectory,
        string path,
        string failureMessage)
    {
        EnsureInsideDirectory(trustedDirectory, path, failureMessage);
        EnsureNoReparsePointInExistingPath(path, failureMessage);
    }

    public static void EnsureOpenedFileRemainsInsideDirectory(
        string trustedDirectory,
        SafeFileHandle handle,
        string failureMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedDirectory);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        string finalPath = GetFinalPath(handle);
        EnsureInsideDirectory(trustedDirectory, finalPath, failureMessage);
    }

    public static FileStream CreateTrustedNewFileForWrite(
        string trustedDirectory,
        string path,
        string failureMessage)
    {
        EnsureTrustedWriteTarget(trustedDirectory, path, failureMessage);

        FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.WriteThrough);

        try
        {
            EnsureOpenedFileRemainsInsideDirectory(
                trustedDirectory,
                stream.SafeFileHandle,
                failureMessage);

            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public static bool TryOpenTrustedExistingFileForRead(
        string path,
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory,
        string failureMessage,
        out FileStream? stream,
        out string? trustedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(trustedDirectories);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        stream = null;
        trustedPath = null;

        if (!TryResolveTrustedExistingFilePath(
            path,
            trustedDirectories,
            trustedRootDirectory,
            out string? resolvedPath)
            || resolvedPath is null)
        {
            return false;
        }

        FileStream openedStream = new(
            resolvedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        try
        {
            trustedPath = ResolveTrustedOpenedFilePath(
                trustedDirectories,
                trustedRootDirectory,
                openedStream.SafeFileHandle,
                failureMessage);
            stream = openedStream;
            return true;
        }
        catch
        {
            openedStream.Dispose();
            throw;
        }
    }

    public static void ReplaceTrustedFile(
        string trustedDirectory,
        string tempPath,
        string targetPath,
        string failureMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        if (OperatingSystem.IsWindows())
        {
            ReplaceTrustedFileByWindowsHandle(
                trustedDirectory,
                tempPath,
                targetPath,
                failureMessage);

            return;
        }

        throw new PlatformNotSupportedException(
            "Trusted file replacement by opened handle is supported only on Windows.");
    }

    public static bool TryResolveTrustedExistingFilePath(
        string path,
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory,
        out string? trustedPath)
    {
        return TryResolveTrustedPath(
            path,
            trustedDirectories,
            trustedRootDirectory,
            TrustedPathResolutionMode.ExistingFile,
            out trustedPath);
    }

    public static bool TryResolveTrustedPathForDeletion(
        string path,
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory,
        out string? trustedPath)
    {
        return TryResolveTrustedPath(
            path,
            trustedDirectories,
            trustedRootDirectory,
            TrustedPathResolutionMode.Deletion,
            out trustedPath);
    }

    public static void DeleteTrustedFileIfExists(
        string path,
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory,
        Action<string> validateResolvedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(trustedDirectories);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedRootDirectory);
        ArgumentNullException.ThrowIfNull(validateResolvedPath);

        if (!TryResolveTrustedPathForDeletion(
            path,
            trustedDirectories,
            trustedRootDirectory,
            out string? trustedPath)
            || trustedPath is null)
        {
            throw new InvalidOperationException("Trusted file path is not safe for deletion.");
        }

        DeleteTrustedFileByHandle(
            trustedPath,
            trustedDirectories,
            trustedRootDirectory,
            validateResolvedPath);
    }

    private static string ValidateAndGetFullPath(
        string path,
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(trustedDirectories);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedRootDirectory);

        return Path.GetFullPath(path);
    }

    private static bool TryResolveTrustedPath(
        string path,
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory,
        TrustedPathResolutionMode mode,
        out string? trustedPath)
    {
        string fullPath = ValidateAndGetFullPath(path, trustedDirectories, trustedRootDirectory);
        trustedPath = null;

        if (mode is TrustedPathResolutionMode.ExistingFile)
        {
            if (!File.Exists(fullPath)
                || !TryGetContainingDirectory(trustedDirectories, fullPath, out string? _))
            {
                return false;
            }

            FileInfo fileInfo = new(fullPath);

            if ((fileInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory
                || IsReparsePoint(fileInfo)
                || HasReparsePointBetween(trustedRootDirectory, fullPath))
            {
                return false;
            }
        }
        else
        {
            if (!TryGetContainingDirectory(trustedDirectories, fullPath, out string? _)
                || HasReparsePointBetween(trustedRootDirectory, fullPath))
            {
                return false;
            }

            if (Directory.Exists(fullPath))
            {
                return false;
            }

            if (File.Exists(fullPath) && IsReparsePoint(new FileInfo(fullPath)))
            {
                return false;
            }
        }

        trustedPath = fullPath;
        return true;
    }

    private static bool IsReparsePoint(FileInfo fileInfo)
    {
        return (fileInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
    }

    private static string ResolveTrustedOpenedFilePath(
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory,
        SafeFileHandle handle,
        string failureMessage)
    {
        string openedPath = GetFinalPath(handle);

        if (!TryGetContainingDirectory(trustedDirectories, openedPath, out string? _)
            || HasReparsePointBetween(trustedRootDirectory, openedPath))
        {
            throw new IOException(failureMessage);
        }

        return openedPath;
    }

    private static string ResolveTrustedOpenedDirectoryPath(
        string trustedDirectory,
        SafeFileHandle handle,
        string failureMessage)
    {
        string openedPath = GetFinalPath(handle);
        string probePath = Path.Combine(openedPath, ".trusted-directory-probe");

        EnsureInsideDirectory(trustedDirectory, openedPath, failureMessage);

        if (HasReparsePointBetween(trustedDirectory, probePath))
        {
            throw new IOException(failureMessage);
        }

        return openedPath;
    }

    private static void ReplaceTrustedFileByWindowsHandle(
        string trustedDirectory,
        string tempPath,
        string targetPath,
        string failureMessage)
    {
        string trustedRootDirectory = Path.GetFullPath(trustedDirectory);
        string tempFullPath = Path.GetFullPath(tempPath);
        string targetFullPath = Path.GetFullPath(targetPath);
        string targetDirectoryPath = GetTargetDirectoryPath(targetFullPath, failureMessage);

        EnsureInsideDirectory(trustedRootDirectory, tempFullPath, failureMessage);
        EnsureInsideDirectory(trustedRootDirectory, targetFullPath, failureMessage);
        EnsureInsideDirectory(trustedRootDirectory, targetDirectoryPath, failureMessage);
        EnsureNoReparsePointInExistingPath(targetDirectoryPath, failureMessage);

        string[] trustedDirectories = [trustedRootDirectory];
        using SafeFileHandle tempHandle = OpenWindowsFileHandleForMutation(tempFullPath);
        ThrowIfWindowsHandleInvalid(tempHandle, "Failed to open trusted temporary file for replacement.");

        ResolveTrustedOpenedFilePath(
            trustedDirectories,
            trustedRootDirectory,
            tempHandle,
            failureMessage);

        using SafeFileHandle targetDirectoryHandle = OpenWindowsDirectoryHandle(targetDirectoryPath);
        ThrowIfWindowsHandleInvalid(targetDirectoryHandle, "Failed to open trusted target directory for replacement.");

        ResolveTrustedOpenedDirectoryPath(
            trustedRootDirectory,
            targetDirectoryHandle,
            failureMessage);
        RenameWindowsHandle(
            tempHandle,
            targetFullPath);
    }

    private static void DeleteTrustedFileByHandle(
        string trustedPath,
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory,
        Action<string> validateResolvedPath)
    {
        if (OperatingSystem.IsWindows())
        {
            DeleteTrustedFileByWindowsHandle(
                trustedPath,
                trustedDirectories,
                trustedRootDirectory,
                validateResolvedPath);

            return;
        }

        throw new PlatformNotSupportedException(
            "Trusted file deletion by opened handle is supported only on Windows.");
    }

    private static void DeleteTrustedFileByWindowsHandle(
        string trustedPath,
        IReadOnlyCollection<string> trustedDirectories,
        string trustedRootDirectory,
        Action<string> validateResolvedPath)
    {
        using SafeFileHandle handle = OpenWindowsFileHandleForMutation(trustedPath);

        if (handle.IsInvalid)
        {
            int errorCode = Marshal.GetLastWin32Error();

            if (IsWindowsMissingFileError(errorCode))
            {
                return;
            }

            throw new IOException(
                $"Failed to open trusted file for deletion. Win32 error: {errorCode}.");
        }

        string resolvedPath = ResolveTrustedOpenedFilePath(
            trustedDirectories,
            trustedRootDirectory,
            handle,
            "Trusted file resolved outside trusted directories before deletion.");
        validateResolvedPath(resolvedPath);
        MarkWindowsHandleForDeletion(handle);
    }

    private static SafeFileHandle OpenWindowsFileHandleForMutation(string trustedPath)
    {
        return CreateFile(
            trustedPath,
            WindowsDeleteAccess | WindowsGenericReadAccess,
            WindowsFileShareRead | WindowsFileShareWrite,
            IntPtr.Zero,
            WindowsOpenExisting,
            0,
            IntPtr.Zero);
    }

    private static SafeFileHandle OpenWindowsDirectoryHandle(string trustedDirectory)
    {
        return CreateFile(
            trustedDirectory,
            WindowsFileListDirectoryAccess,
            WindowsFileShareRead | WindowsFileShareWrite,
            IntPtr.Zero,
            WindowsOpenExisting,
            WindowsFileFlagBackupSemantics,
            IntPtr.Zero);
    }

    private static void MarkWindowsHandleForDeletion(SafeFileHandle handle)
    {
        FileDispositionInfo dispositionInfo = new()
        {
            DeleteFile = true
        };
        uint dispositionInfoSize = (uint)Marshal.SizeOf<FileDispositionInfo>();

        if (!SetFileInformationByHandle(
            handle,
            FileInfoByHandleClass.FileDispositionInfo,
            ref dispositionInfo,
            dispositionInfoSize))
        {
            throw new IOException(
                $"Failed to mark trusted file for deletion. Win32 error: {Marshal.GetLastWin32Error()}.");
        }
    }

    private static bool IsWindowsMissingFileError(int errorCode)
    {
        return errorCode is WindowsErrorFileNotFound
            or WindowsErrorPathNotFound
            or WindowsErrorNotFound;
    }

    private static void RenameWindowsHandle(
        SafeFileHandle sourceHandle,
        string targetPath)
    {
        string nativeTargetPath = ToWindowsNativePath(targetPath);
        byte[] targetFileNameBytes = Encoding.Unicode.GetBytes(string.Concat(nativeTargetPath, '\0'));
        int fileNameOffset = (int)Marshal.OffsetOf<FileRenameInfo>(nameof(FileRenameInfo.FileName));
        int bufferSize = fileNameOffset + targetFileNameBytes.Length;
        IntPtr renameBuffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            FileRenameInfo renameInfo = new()
            {
                ReplaceIfExists = 1,
                RootDirectory = IntPtr.Zero,
                FileNameLength = (uint)(targetFileNameBytes.Length - sizeof(char)),
                FileName = '\0'
            };

            Marshal.StructureToPtr(renameInfo, renameBuffer, fDeleteOld: false);
            Marshal.Copy(
                targetFileNameBytes,
                0,
                IntPtr.Add(renameBuffer, fileNameOffset),
                targetFileNameBytes.Length);

            if (!SetFileInformationByHandle(
                sourceHandle,
                FileInfoByHandleClass.FileRenameInfo,
                renameBuffer,
                (uint)bufferSize))
            {
                throw new IOException(
                    $"Failed to replace trusted file. Win32 error: {Marshal.GetLastWin32Error()}.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(renameBuffer);
        }
    }

    private static void ThrowIfWindowsHandleInvalid(SafeFileHandle handle, string message)
    {
        if (handle.IsInvalid)
        {
            throw new IOException($"{message} Win32 error: {Marshal.GetLastWin32Error()}.");
        }
    }

    private static string GetTargetDirectoryPath(string targetFullPath, string failureMessage)
    {
        string? directoryPath = Path.GetDirectoryName(targetFullPath);

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new IOException(failureMessage);
        }

        return directoryPath;
    }

    private static string ToWindowsNativePath(string path)
    {
        if (path.StartsWith(WindowsUncPathPrefix, StringComparison.Ordinal))
        {
            return string.Concat(WindowsNativeUncPathPrefix, path.AsSpan(WindowsUncPathPrefix.Length));
        }

        return string.Concat(WindowsNativePathPrefix, path);
    }

    private static void EnsureNoReparsePointInExistingPath(
        string path,
        string failureMessage)
    {
        string fullPath = Path.GetFullPath(path);
        FileSystemInfo? segment = GetDeepestExistingSegment(fullPath);

        while (segment is not null)
        {
            if ((segment.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                throw new IOException(failureMessage);
            }

            segment = GetParentSegment(segment);
        }
    }

    private static bool TryGetContainingDirectory(
        IReadOnlyCollection<string> trustedDirectories,
        string fullPath,
        out string? trustedDirectory)
    {
        trustedDirectory = null;

        foreach (string candidateDirectory in trustedDirectories)
        {
            if (IsInsideDirectory(candidateDirectory, fullPath))
            {
                trustedDirectory = candidateDirectory;
                return true;
            }
        }

        return false;
    }

    private static bool HasReparsePointBetween(string trustedDirectory, string fullPath)
    {
        DirectoryInfo? directory = new FileInfo(fullPath).Directory;
        string trustedRoot = Path.GetFullPath(trustedDirectory);

        while (directory is not null)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                return true;
            }

            if (string.Equals(directory.FullName, trustedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            directory = directory.Parent;
        }

        return true;
    }

    private static FileSystemInfo? GetDeepestExistingSegment(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            return new FileInfo(fullPath);
        }

        if (Directory.Exists(fullPath))
        {
            return new DirectoryInfo(fullPath);
        }

        DirectoryInfo? directory = new FileInfo(fullPath).Directory;

        while (directory is not null)
        {
            if (directory.Exists)
            {
                return directory;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static FileSystemInfo? GetParentSegment(FileSystemInfo segment)
    {
        if (segment is DirectoryInfo directory)
        {
            return directory.Parent;
        }

        return ((FileInfo)segment).Directory;
    }

    private static bool StartsWithParentDirectorySegment(string relativePath)
    {
        string[] segments = relativePath.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        return segments.Length > 0
            && segments[0].Equals("..", StringComparison.Ordinal);
    }

    private static string GetFinalPath(SafeFileHandle handle)
    {
        if (OperatingSystem.IsWindows())
        {
            return GetWindowsFinalPath(handle);
        }

        if (OperatingSystem.IsLinux())
        {
            return GetLinuxFinalPath(handle);
        }

        throw new PlatformNotSupportedException(
            "Trusted opened file path verification is supported only on Windows and Linux.");
    }

    private static string GetLinuxFinalPath(SafeFileHandle handle)
    {
        long fileDescriptor = handle.DangerousGetHandle().ToInt64();

        if (fileDescriptor < 0)
        {
            throw new IOException("Failed to resolve file path from an invalid file descriptor.");
        }

        string descriptorPath = Path.Combine("/proc/self/fd", fileDescriptor.ToString());
        FileSystemInfo? target = new FileInfo(descriptorPath).ResolveLinkTarget(returnFinalTarget: true);

        if (target is null)
        {
            throw new IOException("Failed to resolve file path from the opened file descriptor.");
        }

        return Path.GetFullPath(target.FullName);
    }

    private static string GetWindowsFinalPath(SafeFileHandle handle)
    {
        StringBuilder buffer = new(InitialFinalPathBufferLength);
        uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);

        if (length == 0)
        {
            throw CreateWindowsFinalPathResolutionException();
        }

        if (length >= buffer.Capacity)
        {
            buffer = new StringBuilder((int)length + 1);
            length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);

            if (length == 0)
            {
                throw CreateWindowsFinalPathResolutionException();
            }
        }

        return NormalizeFinalPath(buffer.ToString(0, (int)length));
    }

    private static IOException CreateWindowsFinalPathResolutionException()
    {
        return new IOException(
            $"Failed to resolve file path. Win32 error: {Marshal.GetLastWin32Error()}.");
    }

    private static string NormalizeFinalPath(string finalPath)
    {
        const string devicePathPrefix = @"\\?\";
        const string deviceUncPathPrefix = @"\\?\UNC\";

        if (finalPath.StartsWith(deviceUncPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(@"\\", finalPath.AsSpan(deviceUncPathPrefix.Length));
        }

        if (finalPath.StartsWith(devicePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return finalPath[devicePathPrefix.Length..];
        }

        return finalPath;
    }

    [DllImport(WindowsNativeLibraryNames.Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle hFile,
        FileInfoByHandleClass fileInformationClass,
        ref FileDispositionInfo lpFileInformation,
        uint dwBufferSize);

    [DllImport(WindowsNativeLibraryNames.Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle hFile,
        FileInfoByHandleClass fileInformationClass,
        IntPtr lpFileInformation,
        uint dwBufferSize);

    [DllImport(WindowsNativeLibraryNames.Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    private enum FileInfoByHandleClass
    {
        FileRenameInfo = 3,
        FileDispositionInfo = 4
    }

    private enum TrustedPathResolutionMode
    {
        ExistingFile,
        Deletion
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileRenameInfo
    {
        public int ReplaceIfExists;
        public IntPtr RootDirectory;
        public uint FileNameLength;
        public char FileName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool DeleteFile;
    }
}
