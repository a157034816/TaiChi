using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Collections.Generic;

namespace TaiChi.UpgradeKit.Client;

/// <summary>
/// 升级客户端辅助类
/// </summary>
public class UpgradeClientHelper
{
    /// <summary>
    /// 检查当前程序是否以管理员权限运行
    /// </summary>
    /// <returns>是否以管理员权限运行</returns>
    public static bool IsRunningAsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 请求管理员权限重启当前应用
    /// </summary>
    /// <param name="arguments">启动参数</param>
    /// <returns>是否成功启动提权进程</returns>
    public static bool RestartWithAdminRights(string arguments = "")
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas" // 使用 "runas" 请求管理员权限
            };

            Process.Start(startInfo);

            // 退出当前进程
            Environment.Exit(0);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"请求管理员权限失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查目录权限
    /// </summary>
    /// <param name="directory">要检查的目录</param>
    /// <returns>权限检查结果，包含是否有权限和错误信息</returns>
    public static (bool HasPermission, string ErrorMessage) CheckDirectoryPermission(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return (false, "目录路径不能为空");

        if (!Directory.Exists(directory))
            return (false, $"目录不存在: {directory}");

        try
        {
            // 创建临时文件测试写入权限
            string testFile = Path.Combine(directory, $"permission_test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "Test");
            File.Delete(testFile);

            // 创建临时目录测试创建目录权限
            string testDir = Path.Combine(directory, $"permission_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(testDir);
            Directory.Delete(testDir);

            return (true, string.Empty);
        }
        catch (UnauthorizedAccessException ex)
        {
            return (false, $"权限不足: {ex.Message}");
        }
        catch (IOException ex)
        {
            return (false, $"IO错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"未知错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取无法访问的文件列表
    /// </summary>
    /// <param name="directory">要检查的目录</param>
    /// <returns>无法访问的文件列表</returns>
    public static List<string> GetInaccessibleFiles(string directory)
    {
        List<string> inaccessibleFiles = new List<string>();

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return inaccessibleFiles;

        try
        {
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    // 尝试打开文件以测试访问权限
                    using (var fs = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // 如果能打开文件，则有访问权限
                    }
                }
                catch (Exception)
                {
                    inaccessibleFiles.Add(file);
                }
            }
        }
        catch (Exception)
        {
            // 如果遍历目录时出错，忽略错误
        }

        return inaccessibleFiles;
    }

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

        // 检查权限
        var (hasPermission, errorMessage) = CheckDirectoryPermission(sourceDirectory);
        if (!hasPermission)
            throw new UnauthorizedAccessException($"备份应用失败，源目录权限不足: {errorMessage}");

        var (hasBackupPermission, backupErrorMessage) = CheckDirectoryPermission(Path.GetDirectoryName(backupDirectory));
        if (!hasBackupPermission)
            throw new UnauthorizedAccessException($"备份应用失败，备份目录权限不足: {backupErrorMessage}");

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

            try
            {
                File.Copy(filePath, filePath.Replace(sourceDirectory, backupDirectory), true);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"警告: 无法复制文件 {filePath}: {ex.Message}");
                // 不终止整个备份过程，继续尝试其他文件
            }
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

        // 检查权限
        var (hasPermission, errorMessage) = CheckDirectoryPermission(targetDirectory);
        if (!hasPermission)
            throw new UnauthorizedAccessException($"应用更新失败，目标目录权限不足: {errorMessage}");

        // 检查是否有被锁定的文件
        var inaccessibleFiles = GetInaccessibleFiles(targetDirectory);
        if (inaccessibleFiles.Count > 0)
        {
            var message = $"发现{inaccessibleFiles.Count}个正在使用的文件，无法更新。请关闭相关程序后重试。";
            if (inaccessibleFiles.Count <= 5)
            {
                message += $"\n被锁定的文件: {string.Join("\n", inaccessibleFiles)}";
            }

            throw new IOException(message);
        }

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

                try
                {
                    Directory.Delete(dirPath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 无法删除目录 {dirPath}: {ex.Message}");
                }
            }

            foreach (var filePath in Directory.GetFiles(targetDirectory))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 无法删除文件 {filePath}: {ex.Message}");
                }
            }

            // 复制更新内容到目标目录
            foreach (var dirPath in Directory.GetDirectories(tempDirectory, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(tempDirectory, targetDirectory));
            }

            foreach (var filePath in Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Copy(filePath, filePath.Replace(tempDirectory, targetDirectory), true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 无法复制文件 {filePath}: {ex.Message}");
                }
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

        // 检查权限
        var (hasPermission, errorMessage) = CheckDirectoryPermission(targetDirectory);
        if (!hasPermission)
            throw new UnauthorizedAccessException($"应用增量更新失败，目标目录权限不足: {errorMessage}");

        // 检查是否有被锁定的文件
        var inaccessibleFiles = GetInaccessibleFiles(targetDirectory);
        if (inaccessibleFiles.Count > 0)
        {
            var message = $"发现{inaccessibleFiles.Count}个正在使用的文件，无法更新。请关闭相关程序后重试。";
            if (inaccessibleFiles.Count <= 5)
            {
                message += $"\n被锁定的文件: {string.Join("\n", inaccessibleFiles)}";
            }

            throw new IOException(message);
        }

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
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{updateScriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            var process = Process.Start(startInfo);

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
        // 创建更新器临时目录
        string logDirectory = Path.Combine(Path.GetTempPath(), "TaiChiUpdater_Log");
        Directory.CreateDirectory(logDirectory);

        string scriptPath = Path.Combine(updaterDirectory, "update.bat");

        // 获取当前进程ID
        int currentProcessId = Process.GetCurrentProcess().Id;

        // 构建脚本内容
        StringBuilder scriptContent = new StringBuilder();
        scriptContent.AppendLine("@echo off");
        scriptContent.AppendLine("chcp 65001 >nul"); // 设置UTF-8编码
        scriptContent.AppendLine("setlocal enabledelayedexpansion");

        // 创建日志文件
        string logTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string logFileName = $"update_log_{logTimestamp}.log";
        string logFilePath = Path.Combine(logDirectory, logFileName);

        scriptContent.AppendLine($"set \"LOGFILE={logFilePath}\"");
        scriptContent.AppendLine("echo 更新开始时间: %date% %time% > %LOGFILE%");
        scriptContent.AppendLine("echo ---------------------------------------- >> %LOGFILE%");
        scriptContent.AppendLine();

        // 添加管理员权限检查
        scriptContent.AppendLine(":: 检查是否拥有管理员权限");
        scriptContent.AppendLine("echo 检查管理员权限... >> %LOGFILE% 2>&1");
        scriptContent.AppendLine("NET SESSION >nul 2>&1");
        scriptContent.AppendLine("IF %ERRORLEVEL% NEQ 0 (");
        scriptContent.AppendLine("    echo 需要管理员权限，正在请求提权... >> %LOGFILE%");
        scriptContent.AppendLine("    echo 需要管理员权限，正在请求提权...");
        scriptContent.AppendLine("    powershell -Command \"Start-Process -Verb RunAs -FilePath '%~dpnx0' -ArgumentList 'ELEVATED'\" >> %LOGFILE% 2>&1");
        scriptContent.AppendLine("    exit /b");
        scriptContent.AppendLine(")");
        scriptContent.AppendLine("echo 已获得管理员权限 >> %LOGFILE%");
        scriptContent.AppendLine();
        scriptContent.AppendLine(":: 如果是通过提权重启的脚本，移除参数");
        scriptContent.AppendLine("IF \"%~1\"==\"ELEVATED\" (");
        scriptContent.AppendLine("    echo 通过提权重启脚本 >> %LOGFILE%");
        scriptContent.AppendLine("    goto :continue");
        scriptContent.AppendLine(")");
        scriptContent.AppendLine(":continue");
        scriptContent.AppendLine();

        // 等待主应用退出
        scriptContent.AppendLine($"echo 等待应用退出 (PID: {currentProcessId})...");
        scriptContent.AppendLine($"echo 等待应用退出 (PID: {currentProcessId})... >> %LOGFILE%");
        scriptContent.AppendLine($"ping -n {waitTimeInSeconds} 127.0.0.1 > nul");
        scriptContent.AppendLine($"echo 尝试终止进程 PID: {currentProcessId} >> %LOGFILE%");
        scriptContent.AppendLine($"taskkill /F /PID {currentProcessId} >> %LOGFILE% 2>&1");
        scriptContent.AppendLine($"if %ERRORLEVEL% EQU 0 (");
        scriptContent.AppendLine($"    echo 进程已成功终止 >> %LOGFILE%");
        scriptContent.AppendLine($") else (");
        scriptContent.AppendLine($"    echo 进程可能已经退出，终止命令返回: %ERRORLEVEL% >> %LOGFILE%");
        scriptContent.AppendLine($")");
        scriptContent.AppendLine("ping -n 2 127.0.0.1 > nul");
        scriptContent.AppendLine();

        // 应用更新
        scriptContent.AppendLine("echo 开始应用更新...");
        scriptContent.AppendLine("echo 开始应用更新... >> %LOGFILE%");

        // 创建备份目录
        string backupTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupDir = Path.Combine(appDirectory, "Backups", $"Backup_{backupTimestamp}");
        scriptContent.AppendLine($"echo 创建备份目录: \"{backupDir}\" >> %LOGFILE%");
        scriptContent.AppendLine($"mkdir \"{backupDir}\" >> %LOGFILE% 2>&1");
        scriptContent.AppendLine($"if %ERRORLEVEL% NEQ 0 echo 创建备份目录失败: %ERRORLEVEL% >> %LOGFILE% 2>&1");

        // 执行更新操作
        if (isIncremental)
        {
            // 增量更新操作
            scriptContent.AppendLine($"echo 应用增量更新...");
            scriptContent.AppendLine($"echo 应用增量更新... >> %LOGFILE%");

            // 解压到临时目录
            string tempExtractDir = Path.Combine(updaterDirectory, "temp_extract");
            scriptContent.AppendLine($"echo 创建临时解压目录: \"{tempExtractDir}\" >> %LOGFILE%");
            scriptContent.AppendLine($"mkdir \"{tempExtractDir}\" >> %LOGFILE% 2>&1");

            // 使用PowerShell解压缩ZIP文件（兼容性更好）
            scriptContent.AppendLine($"echo 解压更新包: \"{packagePath}\" >> %LOGFILE%");
            scriptContent.AppendLine($"powershell -command \"Expand-Archive -Path '{packagePath}' -DestinationPath '{tempExtractDir}' -Force\" >> %LOGFILE% 2>&1");
            scriptContent.AppendLine($"if %ERRORLEVEL% NEQ 0 (");
            scriptContent.AppendLine($"  echo 解压更新包失败: %ERRORLEVEL% >> %LOGFILE%");
            scriptContent.AppendLine($"  echo 解压更新包失败，请查看日志文件");
            scriptContent.AppendLine($"  goto :error");
            scriptContent.AppendLine($")");

            // 检查是否存在删除文件列表
            scriptContent.AppendLine($"if exist \"{tempExtractDir}\\deleted_files.txt\" (");
            scriptContent.AppendLine($"  echo 处理需要删除的文件... >> %LOGFILE%");
            scriptContent.AppendLine($"  echo 处理需要删除的文件...");
            scriptContent.AppendLine($"  for /F \"tokens=*\" %%f in ('type \"{tempExtractDir}\\deleted_files.txt\"') do (");
            scriptContent.AppendLine($"    if exist \"{appDirectory}\\%%f\" (");
            scriptContent.AppendLine($"      echo 删除文件: %%f >> %LOGFILE%");
            scriptContent.AppendLine($"      echo 删除文件: %%f");
            scriptContent.AppendLine($"      del /F /Q \"{appDirectory}\\%%f\" >> %LOGFILE% 2>&1");
            scriptContent.AppendLine($"      if %ERRORLEVEL% NEQ 0 echo 删除文件失败: %%f (错误码: %ERRORLEVEL%) >> %LOGFILE%");
            scriptContent.AppendLine($"    )");
            scriptContent.AppendLine($"  )");
            scriptContent.AppendLine($"  del /F /Q \"{tempExtractDir}\\deleted_files.txt\" >> %LOGFILE% 2>&1");
            scriptContent.AppendLine($")");

            // 复制所有更新的文件
            scriptContent.AppendLine($"echo 复制更新文件... >> %LOGFILE%");
            scriptContent.AppendLine($"echo 复制更新文件...");
            scriptContent.AppendLine($"xcopy \"{tempExtractDir}\\*\" \"{appDirectory}\\\" /E /Y /I >> %LOGFILE% 2>&1");
            scriptContent.AppendLine($"if %ERRORLEVEL% NEQ 0 (");
            scriptContent.AppendLine($"  echo 复制更新文件失败: %ERRORLEVEL% >> %LOGFILE%");
            scriptContent.AppendLine($"  echo 复制更新文件失败，请查看日志文件");
            scriptContent.AppendLine($"  goto :error");
            scriptContent.AppendLine($")");
        }
        else
        {
            // 完整更新操作
            scriptContent.AppendLine($"echo 应用完整更新...");
            scriptContent.AppendLine($"echo 应用完整更新... >> %LOGFILE%");

            // 备份应用
            scriptContent.AppendLine($"echo 备份当前应用...");
            scriptContent.AppendLine($"echo 备份当前应用... >> %LOGFILE%");
            scriptContent.AppendLine($"xcopy \"{appDirectory}\\*\" \"{backupDir}\\\" /E /Y /I /EXCLUDE:{appDirectory}\\Backups\\*,{appDirectory}\\Downloads\\* >> %LOGFILE% 2>&1");
            scriptContent.AppendLine($"if %ERRORLEVEL% NEQ 0 (");
            scriptContent.AppendLine($"  echo 备份应用失败: %ERRORLEVEL% >> %LOGFILE%");
            scriptContent.AppendLine($"  echo 备份应用失败，但将继续更新过程");
            scriptContent.AppendLine($")");

            // 解压到临时目录
            string tempExtractDir = Path.Combine(updaterDirectory, "temp_extract");
            scriptContent.AppendLine($"echo 创建临时解压目录: \"{tempExtractDir}\" >> %LOGFILE%");
            scriptContent.AppendLine($"mkdir \"{tempExtractDir}\" >> %LOGFILE% 2>&1");

            // 使用PowerShell解压缩ZIP文件
            scriptContent.AppendLine($"echo 解压更新包: \"{packagePath}\" >> %LOGFILE%");
            scriptContent.AppendLine($"powershell -command \"Expand-Archive -Path '{packagePath}' -DestinationPath '{tempExtractDir}' -Force\" >> %LOGFILE% 2>&1");
            scriptContent.AppendLine($"if %ERRORLEVEL% NEQ 0 (");
            scriptContent.AppendLine($"  echo 解压更新包失败: %ERRORLEVEL% >> %LOGFILE%");
            scriptContent.AppendLine($"  echo 解压更新包失败，请查看日志文件");
            scriptContent.AppendLine($"  goto :error");
            scriptContent.AppendLine($")");

            // 清理目标目录（保留备份和下载目录）
            scriptContent.AppendLine($"echo 清理旧文件...");
            scriptContent.AppendLine($"echo 清理旧文件... >> %LOGFILE%");
            scriptContent.AppendLine($"for /D %%d in (\"{appDirectory}\\*\") do (");
            scriptContent.AppendLine($"  set \"dirname=%%~nxd\"");
            scriptContent.AppendLine($"  if /I NOT \"!dirname!\"==\"Backups\" if /I NOT \"!dirname!\"==\"Downloads\" (");
            scriptContent.AppendLine($"    echo 删除目录: %%d >> %LOGFILE%");
            scriptContent.AppendLine($"    echo 删除目录: %%d");
            scriptContent.AppendLine($"    rmdir /S /Q \"%%d\" >> %LOGFILE% 2>&1");
            scriptContent.AppendLine($"    if %ERRORLEVEL% NEQ 0 echo 删除目录失败: %%d (错误码: %ERRORLEVEL%) >> %LOGFILE%");
            scriptContent.AppendLine($"  )");
            scriptContent.AppendLine($")");

            scriptContent.AppendLine($"for %%f in (\"{appDirectory}\\*.*\") do (");
            scriptContent.AppendLine($"  echo 删除文件: %%f >> %LOGFILE%");
            scriptContent.AppendLine($"  echo 删除文件: %%f");
            scriptContent.AppendLine($"  del /F /Q \"%%f\" >> %LOGFILE% 2>&1");
            scriptContent.AppendLine($"  if %ERRORLEVEL% NEQ 0 echo 删除文件失败: %%f (错误码: %ERRORLEVEL%) >> %LOGFILE%");
            scriptContent.AppendLine($")");

            // 复制更新内容
            scriptContent.AppendLine($"echo 复制更新文件...");
            scriptContent.AppendLine($"echo 复制更新文件... >> %LOGFILE%");
            scriptContent.AppendLine($"xcopy \"{tempExtractDir}\\*\" \"{appDirectory}\\\" /E /Y /I >> %LOGFILE% 2>&1");
            scriptContent.AppendLine($"if %ERRORLEVEL% NEQ 0 (");
            scriptContent.AppendLine($"  echo 复制更新文件失败: %ERRORLEVEL% >> %LOGFILE%");
            scriptContent.AppendLine($"  echo 复制更新文件失败，请查看日志文件");
            scriptContent.AppendLine($"  goto :error");
            scriptContent.AppendLine($")");
        }

        // 清理临时文件
        scriptContent.AppendLine();
        scriptContent.AppendLine("echo 清理临时文件...");
        scriptContent.AppendLine("echo 清理临时文件... >> %LOGFILE%");
        scriptContent.AppendLine($"rmdir /S /Q \"{updaterDirectory}\" >> %LOGFILE% 2>&1");

        // 启动应用程序
        scriptContent.AppendLine();
        scriptContent.AppendLine("echo 更新完成，正在启动应用...");
        scriptContent.AppendLine("echo 更新完成，正在启动应用... >> %LOGFILE%");
        scriptContent.AppendLine($"start \"\" \"{executablePath}\" >> %LOGFILE% 2>&1");
        scriptContent.AppendLine($"if %ERRORLEVEL% NEQ 0 (");
        scriptContent.AppendLine($"  echo 启动应用失败: %ERRORLEVEL% >> %LOGFILE%");
        scriptContent.AppendLine($"  echo 启动应用失败，请手动启动应用");
        scriptContent.AppendLine($")");
        scriptContent.AppendLine();
        scriptContent.AppendLine("echo 更新过程完成时间: %date% %time% >> %LOGFILE%");
        scriptContent.AppendLine("echo ---------------------------------------- >> %LOGFILE%");
        scriptContent.AppendLine("goto :end");
        scriptContent.AppendLine();

        // 添加错误处理标签
        scriptContent.AppendLine(":error");
        scriptContent.AppendLine("echo 更新过程中发生错误 >> %LOGFILE%");
        scriptContent.AppendLine("echo 更新过程中发生错误，请查看日志文件: %LOGFILE%");
        scriptContent.AppendLine("echo 错误发生时间: %date% %time% >> %LOGFILE%");
        scriptContent.AppendLine("echo ---------------------------------------- >> %LOGFILE%");
        scriptContent.AppendLine();

        // 结束标签
        scriptContent.AppendLine(":end");
        scriptContent.AppendLine("echo 日志文件保存在: %LOGFILE%");
        // scriptContent.AppendLine("pause");
        scriptContent.AppendLine("exit");

        // 写入脚本文件 - 使用UTF-8编码保存
        File.WriteAllText(scriptPath, scriptContent.ToString(), new UTF8Encoding(false));

        return scriptPath;
    }
}