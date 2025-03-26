using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Reflection;

namespace TaiChi.UpgradeKit.Client;

/// <summary>
/// 升级客户端辅助类
/// </summary>
public class UpgradeClientHelper
{
    /// <summary>
    /// 备份应用
    /// </summary>
    /// <param name="sourceDirectory">源目录</param>
    /// <param name="backupDirectory">备份目录</param>
    public static void BackupApplication(string sourceDirectory, string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
            throw new ArgumentException("源目录不能为空", nameof(sourceDirectory));

        if (string.IsNullOrWhiteSpace(backupDirectory))
            throw new ArgumentException("备份目录不能为空", nameof(backupDirectory));

        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"源目录不存在: {sourceDirectory}");

        // 确保备份目录存在
        Directory.CreateDirectory(backupDirectory);

        // 复制所有文件和目录
        foreach (var dirPath in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            // 排除备份和下载目录
            if (dirPath.Contains("\\Backups\\") || dirPath.Contains("\\Downloads\\"))
                continue;

            Directory.CreateDirectory(dirPath.Replace(sourceDirectory, backupDirectory));
        }

        foreach (var filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            // 排除备份和下载目录
            if (filePath.Contains("\\Backups\\") || filePath.Contains("\\Downloads\\"))
                continue;

            File.Copy(filePath, filePath.Replace(sourceDirectory, backupDirectory), true);
        }

        Console.WriteLine($"已备份应用到: {backupDirectory}");
    }

    /// <summary>
    /// 应用完整更新
    /// </summary>
    /// <param name="packagePath">更新包路径</param>
    /// <param name="targetDirectory">目标目录</param>
    public static void ApplyFullUpdate(string packagePath, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("更新包路径不能为空", nameof(packagePath));

        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("目标目录不能为空", nameof(targetDirectory));

        if (!File.Exists(packagePath))
            throw new FileNotFoundException($"更新包不存在: {packagePath}");

        if (!Directory.Exists(targetDirectory))
            throw new DirectoryNotFoundException($"目标目录不存在: {targetDirectory}");

        // 创建临时解压目录
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            // 解压更新包
            ZipFile.ExtractToDirectory(packagePath, tempDirectory, true);

            // 排除的目录
            var excludeDirs = new[] { "Backups", "Downloads" };

            // 清理目标目录（保留备份和下载目录）
            foreach (var dirPath in Directory.GetDirectories(targetDirectory))
            {
                var dirName = new DirectoryInfo(dirPath).Name;
                if (Array.IndexOf(excludeDirs, dirName) >= 0)
                    continue;

                Directory.Delete(dirPath, true);
            }

            foreach (var filePath in Directory.GetFiles(targetDirectory))
            {
                File.Delete(filePath);
            }

            // 复制更新内容到目标目录
            foreach (var dirPath in Directory.GetDirectories(tempDirectory, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(tempDirectory, targetDirectory));
            }

            foreach (var filePath in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
            {
                File.Copy(filePath, filePath.Replace(tempDirectory, targetDirectory), true);
            }

            Console.WriteLine("已成功应用完整更新");
        }
        finally
        {
            // 清理临时目录
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }
    }

    /// <summary>
    /// 应用增量更新
    /// </summary>
    /// <param name="packagePath">更新包路径</param>
    /// <param name="targetDirectory">目标目录</param>
    public static void ApplyIncrementalUpdate(string packagePath, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("更新包路径不能为空", nameof(packagePath));

        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("目标目录不能为空", nameof(targetDirectory));

        if (!File.Exists(packagePath))
            throw new FileNotFoundException($"更新包不存在: {packagePath}");

        if (!Directory.Exists(targetDirectory))
            throw new DirectoryNotFoundException($"目标目录不存在: {targetDirectory}");

        // 创建临时解压目录
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            // 解压更新包
            ZipFile.ExtractToDirectory(packagePath, tempDirectory, true);

            // 检查是否存在删除文件列表
            string deleteListPath = Path.Combine(tempDirectory, "deleted_files.txt");
            if (File.Exists(deleteListPath))
            {
                // 处理需要删除的文件
                string[] filesToDelete = File.ReadAllLines(deleteListPath);
                foreach (var relativePath in filesToDelete)
                {
                    string fullPath = Path.Combine(targetDirectory, relativePath.Trim());
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                }

                // 删除删除文件列表
                File.Delete(deleteListPath);
            }

            // 复制更新内容到目标目录
            foreach (var dirPath in Directory.GetDirectories(tempDirectory, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(tempDirectory, targetDirectory));
            }

            foreach (var filePath in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
            {
                File.Copy(filePath, filePath.Replace(tempDirectory, targetDirectory), true);
            }

            Console.WriteLine("已成功应用增量更新");
        }
        finally
        {
            // 清理临时目录
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }
    }

    /// <summary>
    /// 计算文件MD5校验和
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>MD5校验和字符串</returns>
    public static string CalculateChecksum(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("文件不存在", filePath);

        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 启动自我更新流程
    /// </summary>
    /// <param name="packagePath">更新包路径</param>
    /// <param name="appDirectory">应用目录</param>
    /// <param name="executablePath">应用程序可执行文件路径</param>
    /// <param name="isIncremental">是否增量更新</param>
    /// <param name="waitTimeInSeconds">等待时间（秒）</param>
    /// <returns>是否成功启动更新流程</returns>
    public static bool StartSelfUpdate(string packagePath, string appDirectory, string executablePath, bool isIncremental = false, int waitTimeInSeconds = 3)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("更新包路径不能为空", nameof(packagePath));

            if (string.IsNullOrWhiteSpace(appDirectory))
                throw new ArgumentException("应用目录不能为空", nameof(appDirectory));

            if (string.IsNullOrWhiteSpace(executablePath))
                throw new ArgumentException("可执行文件路径不能为空", nameof(executablePath));

            if (!File.Exists(packagePath))
                throw new FileNotFoundException($"更新包不存在: {packagePath}");

            if (!File.Exists(executablePath))
                throw new FileNotFoundException($"可执行文件不存在: {executablePath}");

            // 创建更新器临时目录
            string updaterDirectory = Path.Combine(Path.GetTempPath(), "TaiChiUpdater_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(updaterDirectory);

            // 创建更新脚本
            string updateScriptPath = CreateUpdateScript(updaterDirectory, packagePath, appDirectory, executablePath, isIncremental, waitTimeInSeconds);

            // 启动更新脚本
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{updateScriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"启动自我更新失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 创建更新脚本
    /// </summary>
    private static string CreateUpdateScript(string updaterDirectory, string packagePath, string appDirectory, string executablePath, bool isIncremental, int waitTimeInSeconds)
    {
        string scriptPath = Path.Combine(updaterDirectory, "update.bat");

        // 获取当前进程ID
        int currentProcessId = Process.GetCurrentProcess().Id;

        // 构建脚本内容
        StringBuilder scriptContent = new StringBuilder();
        scriptContent.AppendLine("@echo off");
        scriptContent.AppendLine("setlocal enabledelayedexpansion");
        scriptContent.AppendLine();

        // 等待主应用退出
        scriptContent.AppendLine($"echo 等待应用退出 (PID: {currentProcessId})...");
        scriptContent.AppendLine($"ping -n {waitTimeInSeconds} 127.0.0.1 > nul");
        scriptContent.AppendLine($"taskkill /F /PID {currentProcessId} 2>nul");
        scriptContent.AppendLine("ping -n 2 127.0.0.1 > nul");
        scriptContent.AppendLine();

        // 应用更新
        scriptContent.AppendLine("echo 开始应用更新...");

        // 创建备份目录
        string backupTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupDir = Path.Combine(appDirectory, "Backups", $"Backup_{backupTimestamp}");
        scriptContent.AppendLine($"mkdir \"{backupDir}\" 2>nul");

        // 执行更新操作
        if (isIncremental)
        {
            // 增量更新操作
            scriptContent.AppendLine($"echo 应用增量更新...");

            // 解压到临时目录
            string tempExtractDir = Path.Combine(updaterDirectory, "temp_extract");
            scriptContent.AppendLine($"mkdir \"{tempExtractDir}\" 2>nul");

            // 使用PowerShell解压缩ZIP文件（兼容性更好）
            scriptContent.AppendLine($"powershell -command \"Expand-Archive -Path '{packagePath}' -DestinationPath '{tempExtractDir}' -Force\"");

            // 检查是否存在删除文件列表
            scriptContent.AppendLine($"if exist \"{tempExtractDir}\\deleted_files.txt\" (");
            scriptContent.AppendLine($"  echo 处理需要删除的文件...");
            scriptContent.AppendLine($"  for /F \"tokens=*\" %%f in ('type \"{tempExtractDir}\\deleted_files.txt\"') do (");
            scriptContent.AppendLine($"    if exist \"{appDirectory}\\%%f\" (");
            scriptContent.AppendLine($"      echo 删除文件: %%f");
            scriptContent.AppendLine($"      del /F /Q \"{appDirectory}\\%%f\"");
            scriptContent.AppendLine($"    )");
            scriptContent.AppendLine($"  )");
            scriptContent.AppendLine($"  del /F /Q \"{tempExtractDir}\\deleted_files.txt\"");
            scriptContent.AppendLine($")");

            // 复制所有更新的文件
            scriptContent.AppendLine($"echo 复制更新文件...");
            scriptContent.AppendLine($"xcopy \"{tempExtractDir}\\*\" \"{appDirectory}\\\" /E /Y /I");
        }
        else
        {
            // 完整更新操作
            scriptContent.AppendLine($"echo 应用完整更新...");

            // 备份应用
            scriptContent.AppendLine($"echo 备份当前应用...");
            scriptContent.AppendLine($"xcopy \"{appDirectory}\\*\" \"{backupDir}\\\" /E /Y /I /EXCLUDE:{appDirectory}\\Backups\\*,{appDirectory}\\Downloads\\*");

            // 解压到临时目录
            string tempExtractDir = Path.Combine(updaterDirectory, "temp_extract");
            scriptContent.AppendLine($"mkdir \"{tempExtractDir}\" 2>nul");

            // 使用PowerShell解压缩ZIP文件
            scriptContent.AppendLine($"powershell -command \"Expand-Archive -Path '{packagePath}' -DestinationPath '{tempExtractDir}' -Force\"");

            // 清理目标目录（保留备份和下载目录）
            scriptContent.AppendLine($"echo 清理旧文件...");
            scriptContent.AppendLine($"for /D %%d in (\"{appDirectory}\\*\") do (");
            scriptContent.AppendLine($"  set \"dirname=%%~nxd\"");
            scriptContent.AppendLine($"  if /I NOT \"!dirname!\"==\"Backups\" if /I NOT \"!dirname!\"==\"Downloads\" (");
            scriptContent.AppendLine($"    echo 删除目录: %%d");
            scriptContent.AppendLine($"    rmdir /S /Q \"%%d\"");
            scriptContent.AppendLine($"  )");
            scriptContent.AppendLine($")");

            scriptContent.AppendLine($"for %%f in (\"{appDirectory}\\*.*\") do (");
            scriptContent.AppendLine($"  echo 删除文件: %%f");
            scriptContent.AppendLine($"  del /F /Q \"%%f\"");
            scriptContent.AppendLine($")");

            // 复制更新内容
            scriptContent.AppendLine($"echo 复制更新文件...");
            scriptContent.AppendLine($"xcopy \"{tempExtractDir}\\*\" \"{appDirectory}\\\" /E /Y /I");
        }

        // 清理临时文件
        scriptContent.AppendLine();
        scriptContent.AppendLine("echo 清理临时文件...");
        scriptContent.AppendLine($"rmdir /S /Q \"{updaterDirectory}\" 2>nul");

        // 启动应用程序
        scriptContent.AppendLine();
        scriptContent.AppendLine("echo 更新完成，正在启动应用...");
        scriptContent.AppendLine($"start \"\" \"{executablePath}\"");
        scriptContent.AppendLine();
        scriptContent.AppendLine("exit");

        // 写入脚本文件
        File.WriteAllText(scriptPath, scriptContent.ToString());

        return scriptPath;
    }
}
