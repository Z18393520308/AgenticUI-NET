using System.Security.Cryptography;

namespace AgenticUI.Remote;

/// <summary>Windows 使用当前用户 DPAPI；其他平台仅用于开发测试，使用用户私有目录。</summary>
internal static class ProtectedLocalFile
{
    internal static byte[] Read(string path)
    {
        var data = File.ReadAllBytes(path);
        return
#if NET8_0_OR_GREATER
            OperatingSystem.IsWindows()
#else
            Environment.OSVersion.Platform == PlatformID.Win32NT
#endif
            ? ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser) : data;
    }

    internal static void Write(string path, byte[] data)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
#if NET8_0_OR_GREATER
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#endif
        var bytes =
#if NET8_0_OR_GREATER
            OperatingSystem.IsWindows()
#else
            Environment.OSVersion.Platform == PlatformID.Win32NT
#endif
            ? ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser) : data;
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
#if NET8_0_OR_GREATER
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temporary, options)) { stream.Write(bytes); stream.Flush(true); }
#else
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
#endif
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
}
