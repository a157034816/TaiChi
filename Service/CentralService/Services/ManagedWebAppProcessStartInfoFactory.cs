using System.Diagnostics;
using System.IO;

namespace CentralService.Services;

internal static class ManagedWebAppProcessStartInfoFactory
{
    public static ProcessStartInfo Create(string command, IEnumerable<string>? arguments)
    {
        return CreateCore(
            command,
            arguments,
            pathEnv: Environment.GetEnvironmentVariable("PATH"),
            fileExists: File.Exists,
            comspec: Environment.GetEnvironmentVariable("COMSPEC"));
    }

    internal static ProcessStartInfo CreateForTest(
        string command,
        IEnumerable<string>? arguments,
        string? pathEnv,
        Func<string, bool> fileExists,
        string? comspec)
    {
        return CreateCore(command, arguments, pathEnv, fileExists, comspec);
    }

    private static ProcessStartInfo CreateCore(
        string command,
        IEnumerable<string>? arguments,
        string? pathEnv,
        Func<string, bool> fileExists,
        string? comspec)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("command 不能为空。", nameof(command));
        }

        ArgumentNullException.ThrowIfNull(fileExists);

        var normalizedCommand = command.Trim();
        var extension = Path.GetExtension(normalizedCommand);

        if (OperatingSystem.IsWindows() && extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Windows 下无法直接启动 .ps1 脚本，请将 Command 配置为 powershell/pwsh 并通过 -File 调用脚本。");
        }

        var shouldWrapWithCmd = ShouldWrapWithCmdOnWindows(normalizedCommand, extension, pathEnv, fileExists);
        if (shouldWrapWithCmd)
        {
            var cmd = string.IsNullOrWhiteSpace(comspec) ? "cmd.exe" : comspec.Trim();
            var wrappedPsi = new ProcessStartInfo { FileName = cmd };

            wrappedPsi.ArgumentList.Add("/d");
            wrappedPsi.ArgumentList.Add("/s");
            wrappedPsi.ArgumentList.Add("/c");
            wrappedPsi.ArgumentList.Add(normalizedCommand);

            foreach (var argument in arguments ?? Array.Empty<string>())
            {
                wrappedPsi.ArgumentList.Add(argument);
            }

            ApplyDefaults(wrappedPsi);
            return wrappedPsi;
        }

        var directPsi = new ProcessStartInfo { FileName = normalizedCommand };
        foreach (var argument in arguments ?? Array.Empty<string>())
        {
            directPsi.ArgumentList.Add(argument);
        }

        ApplyDefaults(directPsi);
        return directPsi;
    }

    private static void ApplyDefaults(ProcessStartInfo psi)
    {
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;
    }

    internal static bool ShouldWrapWithCmdOnWindows(
        string command,
        string extension,
        string? pathEnv,
        Func<string, bool> fileExists)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(extension))
        {
            if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return false;
        }

        if (LooksLikePath(command))
        {
            return false;
        }

        return !ExistsOnPath(command, pathEnv, fileExists, new[] { ".exe", ".com" });
    }

    internal static bool ExistsOnPath(
        string fileNameWithoutExtension,
        string? pathEnv,
        Func<string, bool> fileExists,
        IReadOnlyList<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return false;
        }

        foreach (var rawDirectory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = rawDirectory.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (var extension in extensions)
            {
                try
                {
                    var candidate = Path.Combine(directory, fileNameWithoutExtension + extension);
                    if (fileExists(candidate))
                    {
                        return true;
                    }
                }
                catch
                {
                    // 忽略无效 PATH 条目。
                }
            }
        }

        return false;
    }

    private static bool LooksLikePath(string command)
    {
        return command.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0
               || command.Contains(':');
    }
}
