using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TaiChi.Upgrade.Shared;

namespace TaiChi.UpgradeKit.Server
{
    /// <summary>
    /// 升级服务器辅助类
    /// </summary>
    public class UpgradeServerHelper
    {
        /// <summary>
        /// 比较两个版本号
        /// </summary>
        /// <param name="version1">版本1</param>
        /// <param name="version2">版本2</param>
        /// <returns>如果version1大于version2，返回>0；如果version1小于version2，返回<0；如果相等，返回0</returns>
        public static int CompareVersions(Version version1, Version version2)
        {
            if (version1 == null)
                throw new ArgumentNullException(nameof(version1));

            if (version2 == null)
                throw new ArgumentNullException(nameof(version2));

            // 按主版本号、次版本号、构建号顺序比较
            var result = version1.Major.CompareTo(version2.Major);
            if (result != 0) return result;

            result = version1.Minor.CompareTo(version2.Minor);
            if (result != 0) return result;

            result = version1.Build.CompareTo(version2.Build);
            if (result != 0) return result;

            return version1.Revision.CompareTo(version2.Revision);
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
        /// 创建增量补丁包
        /// </summary>
        /// <param name="sourceDir">源目录</param>
        /// <param name="targetDir">目标目录</param>
        /// <param name="outputPath">输出路径</param>
        /// <returns>增量包路径</returns>
        public static string CreateIncrementalPackage(string sourceDir, string targetDir, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                throw new ArgumentException("源目录无效", nameof(sourceDir));

            if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
                throw new ArgumentException("目标目录无效", nameof(targetDir));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("输出路径不能为空", nameof(outputPath));

            // 确保输出目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("无效的输出路径"));

            // 创建临时目录
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // 查找目标目录中的所有文件
                foreach (var filePath in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = filePath.Substring(targetDir.Length).TrimStart(Path.DirectorySeparatorChar);
                    var sourceFilePath = Path.Combine(sourceDir, relativePath);
                    var tempFilePath = Path.Combine(tempDir, relativePath);

                    // 确保临时文件的目录存在
                    Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath) ?? throw new InvalidOperationException("无效的临时文件路径"));

                    // 如果源文件不存在或者与目标文件不同，则添加到增量包
                    if (!File.Exists(sourceFilePath) || !FilesAreEqual(sourceFilePath, filePath))
                    {
                        File.Copy(filePath, tempFilePath, true);
                    }
                }

                // 创建删除文件列表
                var deleteListPath = Path.Combine(tempDir, "deleted_files.txt");
                using (var writer = new StreamWriter(deleteListPath))
                {
                    foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = filePath.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
                        var targetFilePath = Path.Combine(targetDir, relativePath);

                        if (!File.Exists(targetFilePath))
                        {
                            writer.WriteLine(relativePath);
                        }
                    }
                }

                // 创建增量包
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                ZipFile.CreateFromDirectory(tempDir, outputPath);
                return outputPath;
            }
            finally
            {
                // 清理临时目录
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// 比较两个文件是否相同
        /// </summary>
        /// <param name="path1">文件1路径</param>
        /// <param name="path2">文件2路径</param>
        /// <returns>是否相同</returns>
        private static bool FilesAreEqual(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
                return false;

            if (!File.Exists(path1) || !File.Exists(path2))
                return false;

            var file1Info = new FileInfo(path1);
            var file2Info = new FileInfo(path2);

            // 如果文件大小不同，则文件不同
            if (file1Info.Length != file2Info.Length)
                return false;

            // 比较文件内容
            using var file1 = File.OpenRead(path1);
            using var file2 = File.OpenRead(path2);

            const int bufferSize = 4096;
            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];

            while (true)
            {
                int count1 = file1.Read(buffer1, 0, bufferSize);
                int count2 = file2.Read(buffer2, 0, bufferSize);

                if (count1 != count2)
                    return false;

                if (count1 == 0)
                    return true;

                for (int i = 0; i < count1; i++)
                {
                    if (buffer1[i] != buffer2[i])
                        return false;
                }
            }
        }

        /// <summary>
        /// 创建完整包
        /// </summary>
        /// <param name="sourceDir">源目录</param>
        /// <param name="outputPath">输出路径</param>
        /// <returns>完整包路径</returns>
        public static string CreateFullPackage(string sourceDir, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                throw new ArgumentException("源目录无效", nameof(sourceDir));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("输出路径不能为空", nameof(outputPath));

            // 确保输出目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("无效的输出路径"));

            // 创建完整包
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            ZipFile.CreateFromDirectory(sourceDir, outputPath);
            return outputPath;
        }
    }
}
