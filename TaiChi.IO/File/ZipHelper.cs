using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TaiChi.IO.File
{
    /// <summary>
    /// Zip压缩文件帮助类
    /// 提供文件和文件夹的压缩与解压缩功能，支持同步和异步操作
    /// 包含单文件压缩、多文件压缩、文件夹压缩、文件解压、获取压缩文件内容等功能
    /// </summary>
    public class ZipHelper
    {
        #region 压缩

        /// <summary>
        /// 将单个文件压缩到zip文件中
        /// </summary>
        /// <param name="sourceFilePath">源文件路径，需要是一个有效的文件路径</param>
        /// <param name="zipFilePath">目标zip文件路径，如果文件已存在将被覆盖</param>
        /// <param name="compressionLevel">压缩级别，默认为Optimal（最佳压缩率和性能的平衡）</param>
        /// <returns>压缩操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法会将指定的单个文件压缩到一个新的zip文件中
        /// 如果源文件不存在，将返回false
        /// 如果压缩过程中发生异常，将捕获异常并返回false
        /// </remarks>
        public static bool CompressFile(string sourceFilePath, string zipFilePath, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            try
            {
                if (!System.IO.File.Exists(sourceFilePath))
                    return false;

                using (FileStream zipToCreate = new FileStream(zipFilePath, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create))
                    {
                        string fileName = Path.GetFileName(sourceFilePath);
                        ZipArchiveEntry entry = archive.CreateEntry(fileName, compressionLevel);
                        using (Stream entryStream = entry.Open())
                        using (FileStream fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read))
                        {
                            fs.CopyTo(entryStream);
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 将单个文件压缩到zip文件中（异步方法）
        /// </summary>
        /// <param name="sourceFilePath">源文件路径，需要是一个有效的文件路径</param>
        /// <param name="zipFilePath">目标zip文件路径，如果文件已存在将被覆盖</param>
        /// <param name="compressionLevel">压缩级别，默认为Optimal（最佳压缩率和性能的平衡）</param>
        /// <param name="cancellationToken">取消令牌，用于取消异步操作</param>
        /// <returns>异步任务，返回压缩操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法是CompressFile的异步版本，适用于需要避免阻塞主线程的场景
        /// 如果源文件不存在，将返回false
        /// 如果压缩过程中发生异常，将捕获异常并返回false
        /// 如果操作被取消，将返回false
        /// </remarks>
        public static async Task<bool> CompressFileAsync(string sourceFilePath, string zipFilePath, 
            CompressionLevel compressionLevel = CompressionLevel.Optimal, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!System.IO.File.Exists(sourceFilePath))
                    return false;

                using (FileStream zipToCreate = new FileStream(zipFilePath, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create))
                    {
                        string fileName = Path.GetFileName(sourceFilePath);
                        ZipArchiveEntry entry = archive.CreateEntry(fileName, compressionLevel);
                        using (Stream entryStream = entry.Open())
                        using (FileStream fs = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read))
                        {
                            await fs.CopyToAsync(entryStream, 81920, cancellationToken);
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 将多个文件压缩到一个zip文件中
        /// </summary>
        /// <param name="sourceFilePaths">源文件路径列表，包含所有需要压缩的文件路径</param>
        /// <param name="zipFilePath">目标zip文件路径，如果文件已存在将被覆盖</param>
        /// <param name="compressionLevel">压缩级别，默认为Optimal（最佳压缩率和性能的平衡）</param>
        /// <returns>压缩操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法会将多个文件压缩到同一个zip文件中
        /// 如果列表中某个文件不存在，将跳过该文件继续处理其他文件
        /// 如果压缩过程中发生异常，将捕获异常并返回false
        /// 压缩后的文件在zip中保持原始文件名，不包含路径信息
        /// </remarks>
        public static bool CompressFiles(IEnumerable<string> sourceFilePaths, string zipFilePath, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            try
            {
                using (FileStream zipToCreate = new FileStream(zipFilePath, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create))
                    {
                        foreach (string filePath in sourceFilePaths)
                        {
                            if (!System.IO.File.Exists(filePath))
                                continue;

                            string fileName = Path.GetFileName(filePath);
                            ZipArchiveEntry entry = archive.CreateEntry(fileName, compressionLevel);
                            using (Stream entryStream = entry.Open())
                            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                            {
                                fs.CopyTo(entryStream);
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 将多个文件压缩到一个zip文件中（异步方法）
        /// </summary>
        /// <param name="sourceFilePaths">源文件路径列表，包含所有需要压缩的文件路径</param>
        /// <param name="zipFilePath">目标zip文件路径，如果文件已存在将被覆盖</param>
        /// <param name="compressionLevel">压缩级别，默认为Optimal（最佳压缩率和性能的平衡）</param>
        /// <param name="cancellationToken">取消令牌，用于取消异步操作</param>
        /// <returns>异步任务，返回压缩操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法是CompressFiles的异步版本，适用于需要避免阻塞主线程的场景
        /// 如果列表中某个文件不存在，将跳过该文件继续处理其他文件
        /// 如果压缩过程中发生异常，将捕获异常并返回false
        /// 如果操作被取消，将返回false
        /// 压缩后的文件在zip中保持原始文件名，不包含路径信息
        /// </remarks>
        public static async Task<bool> CompressFilesAsync(IEnumerable<string> sourceFilePaths, string zipFilePath, 
            CompressionLevel compressionLevel = CompressionLevel.Optimal, CancellationToken cancellationToken = default)
        {
            try
            {
                using (FileStream zipToCreate = new FileStream(zipFilePath, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create))
                    {
                        foreach (string filePath in sourceFilePaths)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return false;

                            if (!System.IO.File.Exists(filePath))
                                continue;

                            string fileName = Path.GetFileName(filePath);
                            ZipArchiveEntry entry = archive.CreateEntry(fileName, compressionLevel);
                            using (Stream entryStream = entry.Open())
                            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                            {
                                await fs.CopyToAsync(entryStream, 81920, cancellationToken);
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 压缩整个文件夹到zip文件
        /// </summary>
        /// <param name="sourceDirPath">源文件夹路径，需要是一个有效的目录路径</param>
        /// <param name="zipFilePath">目标zip文件路径，如果文件已存在将被覆盖</param>
        /// <param name="includeBaseDirectory">是否包含基目录，如果为true则会在zip中创建一个与源文件夹同名的根目录</param>
        /// <param name="compressionLevel">压缩级别，默认为Optimal（最佳压缩率和性能的平衡）</param>
        /// <returns>压缩操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法会将整个文件夹（包括所有子文件夹和文件）压缩到一个zip文件中
        /// 如果源文件夹不存在，将返回false
        /// 如果压缩过程中发生异常，将捕获异常并返回false
        /// </remarks>
        public static bool CompressDirectory(string sourceDirPath, string zipFilePath, bool includeBaseDirectory = false, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            try
            {
                if (!Directory.Exists(sourceDirPath))
                    return false;

                ZipFile.CreateFromDirectory(sourceDirPath, zipFilePath, compressionLevel, includeBaseDirectory);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 压缩整个文件夹到zip文件（异步方法）
        /// </summary>
        /// <param name="sourceDirPath">源文件夹路径，需要是一个有效的目录路径</param>
        /// <param name="zipFilePath">目标zip文件路径，如果文件已存在将被覆盖</param>
        /// <param name="includeBaseDirectory">是否包含基目录，如果为true则会在zip中创建一个与源文件夹同名的根目录</param>
        /// <param name="compressionLevel">压缩级别，默认为Optimal（最佳压缩率和性能的平衡）</param>
        /// <param name="cancellationToken">取消令牌，用于取消异步操作</param>
        /// <returns>异步任务，返回压缩操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法是CompressDirectory的异步版本，通过Task.Run在后台线程执行
        /// 如果源文件夹不存在，将返回false
        /// 如果压缩过程中发生异常，将捕获异常并返回false
        /// 如果操作被取消，将返回false
        /// </remarks>
        public static async Task<bool> CompressDirectoryAsync(string sourceDirPath, string zipFilePath, 
            bool includeBaseDirectory = false, CompressionLevel compressionLevel = CompressionLevel.Optimal, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(sourceDirPath) || cancellationToken.IsCancellationRequested)
                        return false;

                    ZipFile.CreateFromDirectory(sourceDirPath, zipFilePath, compressionLevel, includeBaseDirectory);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }, cancellationToken);
        }

        #endregion

        #region 解压缩

        /// <summary>
        /// 解压zip文件到指定目录
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，需要是一个有效的zip文件</param>
        /// <param name="extractPath">解压目标路径，如果目录不存在将被创建</param>
        /// <param name="overwrite">是否覆盖已存在的文件，默认为true</param>
        /// <returns>解压操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法会将zip文件中的所有内容解压到指定目录
        /// 如果zip文件不存在，将返回false
        /// 如果overwrite为true，会覆盖目标路径中已存在的同名文件
        /// 如果overwrite为false且目标路径中存在同名文件，会使用ZipFile.ExtractToDirectory方法（此方法在遇到同名文件时会抛出异常）
        /// 如果解压过程中发生异常，将捕获异常并返回false
        /// </remarks>
        public static bool ExtractToDirectory(string zipFilePath, string extractPath, bool overwrite = true)
        {
            try
            {
                if (!System.IO.File.Exists(zipFilePath))
                    return false;

                if (!Directory.Exists(extractPath))
                    Directory.CreateDirectory(extractPath);

                if (overwrite)
                {
                    using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destinationPath = Path.Combine(extractPath, entry.FullName);
                            string destinationDirectory = Path.GetDirectoryName(destinationPath);

                            if (!Directory.Exists(destinationDirectory))
                                Directory.CreateDirectory(destinationDirectory);

                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                entry.ExtractToFile(destinationPath, true);
                            }
                        }
                    }
                }
                else
                {
                    ZipFile.ExtractToDirectory(zipFilePath, extractPath);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 解压zip文件到指定目录（异步方法）
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，需要是一个有效的zip文件</param>
        /// <param name="extractPath">解压目标路径，如果目录不存在将被创建</param>
        /// <param name="overwrite">是否覆盖已存在的文件，默认为true</param>
        /// <param name="cancellationToken">取消令牌，用于取消异步操作</param>
        /// <returns>异步任务，返回解压操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法是ExtractToDirectory的异步版本，通过Task.Run在后台线程执行
        /// 如果zip文件不存在，将返回false
        /// 如果overwrite为true，会覆盖目标路径中已存在的同名文件
        /// 如果overwrite为false且目标路径中存在同名文件，会使用ZipFile.ExtractToDirectory方法
        /// 如果解压过程中发生异常，将捕获异常并返回false
        /// 如果操作被取消，将返回false
        /// </remarks>
        public static async Task<bool> ExtractToDirectoryAsync(string zipFilePath, string extractPath, 
            bool overwrite = true, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!System.IO.File.Exists(zipFilePath) || cancellationToken.IsCancellationRequested)
                        return false;

                    if (!Directory.Exists(extractPath))
                        Directory.CreateDirectory(extractPath);

                    if (overwrite)
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    return false;

                                string destinationPath = Path.Combine(extractPath, entry.FullName);
                                string destinationDirectory = Path.GetDirectoryName(destinationPath);

                                if (!Directory.Exists(destinationDirectory))
                                    Directory.CreateDirectory(destinationDirectory);

                                if (!string.IsNullOrEmpty(entry.Name))
                                {
                                    entry.ExtractToFile(destinationPath, true);
                                }
                            }
                        }
                    }
                    else
                    {
                        ZipFile.ExtractToDirectory(zipFilePath, extractPath);
                    }

                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 从zip文件中提取单个文件
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，需要是一个有效的zip文件</param>
        /// <param name="entryName">要提取的条目名称，必须与zip文件中的条目名称完全匹配（包括路径）</param>
        /// <param name="destinationPath">目标文件路径，如果目录不存在将被创建</param>
        /// <param name="overwrite">是否覆盖已存在的文件，默认为true</param>
        /// <returns>提取操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法会从zip文件中提取指定的单个文件到目标路径
        /// 如果zip文件不存在，将返回false
        /// 如果指定的条目在zip文件中不存在，将返回false
        /// 如果提取过程中发生异常，将捕获异常并返回false
        /// </remarks>
        public static bool ExtractFile(string zipFilePath, string entryName, string destinationPath, bool overwrite = true)
        {
            try
            {
                if (!System.IO.File.Exists(zipFilePath))
                    return false;

                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    ZipArchiveEntry entry = archive.GetEntry(entryName);
                    if (entry == null)
                        return false;

                    string destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDirectory))
                        Directory.CreateDirectory(destinationDirectory);

                    entry.ExtractToFile(destinationPath, overwrite);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 从zip文件中提取单个文件（异步方法）
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，需要是一个有效的zip文件</param>
        /// <param name="entryName">要提取的条目名称，必须与zip文件中的条目名称完全匹配（包括路径）</param>
        /// <param name="destinationPath">目标文件路径，如果目录不存在将被创建</param>
        /// <param name="overwrite">是否覆盖已存在的文件，默认为true</param>
        /// <param name="cancellationToken">取消令牌，用于取消异步操作</param>
        /// <returns>异步任务，返回提取操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法是ExtractFile的异步版本，通过Task.Run在后台线程执行
        /// 如果zip文件不存在，将返回false
        /// 如果指定的条目在zip文件中不存在，将返回false
        /// 如果提取过程中发生异常，将捕获异常并返回false
        /// 如果操作被取消，将返回false
        /// </remarks>
        public static async Task<bool> ExtractFileAsync(string zipFilePath, string entryName, string destinationPath, 
            bool overwrite = true, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!System.IO.File.Exists(zipFilePath) || cancellationToken.IsCancellationRequested)
                        return false;

                    using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                    {
                        ZipArchiveEntry entry = archive.GetEntry(entryName);
                        if (entry == null)
                            return false;

                        string destinationDirectory = Path.GetDirectoryName(destinationPath);
                        if (!Directory.Exists(destinationDirectory))
                            Directory.CreateDirectory(destinationDirectory);

                        entry.ExtractToFile(destinationPath, overwrite);
                        return true;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 获取zip文件中的所有条目名称
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，需要是一个有效的zip文件</param>
        /// <returns>条目名称列表，包含zip文件中所有文件和文件夹的完整路径，失败返回空列表</returns>
        /// <remarks>
        /// 此方法会返回zip文件中所有条目的完整路径名称列表
        /// 如果zip文件不存在，将返回空列表
        /// 如果获取过程中发生异常，将捕获异常并返回空列表
        /// 返回的列表包含文件和文件夹（如果zip中包含空文件夹）
        /// </remarks>
        public static List<string> GetEntries(string zipFilePath)
        {
            try
            {
                if (!System.IO.File.Exists(zipFilePath))
                    return new List<string>();

                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    return archive.Entries.Select(e => e.FullName).ToList();
                }
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 获取zip文件中的所有条目名称（异步方法）
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，需要是一个有效的zip文件</param>
        /// <param name="cancellationToken">取消令牌，用于取消异步操作</param>
        /// <returns>异步任务，返回条目名称列表，包含zip文件中所有文件和文件夹的完整路径，失败返回空列表</returns>
        /// <remarks>
        /// 此方法是GetEntries的异步版本，通过Task.Run在后台线程执行
        /// 如果zip文件不存在，将返回空列表
        /// 如果获取过程中发生异常，将捕获异常并返回空列表
        /// 如果操作被取消，将返回空列表
        /// 返回的列表包含文件和文件夹（如果zip中包含空文件夹）
        /// </remarks>
        public static async Task<List<string>> GetEntriesAsync(string zipFilePath, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!System.IO.File.Exists(zipFilePath) || cancellationToken.IsCancellationRequested)
                        return new List<string>();

                    using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                    {
                        return archive.Entries.Select(e => e.FullName).ToList();
                    }
                }
                catch (Exception)
                {
                    return new List<string>();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 从zip文件中读取指定文件的内容为字符串
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，需要是一个有效的zip文件</param>
        /// <param name="entryName">要读取的文件名称，必须与zip文件中的条目名称完全匹配（包括路径）</param>
        /// <param name="encoding">读取文件时使用的编码，默认为UTF8</param>
        /// <returns>文件内容的字符串，如果文件不存在或读取失败则返回null</returns>
        /// <remarks>
        /// 此方法会从zip文件中读取指定文件的内容并返回为字符串
        /// 如果zip文件不存在，将返回null
        /// 如果指定的条目在zip文件中不存在，将返回null
        /// 如果读取过程中发生异常，将捕获异常并返回null
        /// </remarks>
        public static string ReadFileContent(string zipFilePath, string entryName, Encoding encoding = null)
        {
            try
            {
                if (!System.IO.File.Exists(zipFilePath))
                    return null;

                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    ZipArchiveEntry entry = archive.GetEntry(entryName);
                    if (entry == null)
                        return null;

                    using (Stream stream = entry.Open())
                    using (StreamReader reader = new StreamReader(stream, encoding ?? Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 从zip文件中读取指定文件的内容为字符串（异步方法）
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，需要是一个有效的zip文件</param>
        /// <param name="entryName">要读取的文件名称，必须与zip文件中的条目名称完全匹配（包括路径）</param>
        /// <param name="encoding">读取文件时使用的编码，默认为UTF8</param>
        /// <param name="cancellationToken">取消令牌，用于取消异步操作</param>
        /// <returns>异步任务，返回文件内容的字符串，如果文件不存在或读取失败则返回null</returns>
        /// <remarks>
        /// 此方法是ReadFileContent的异步版本
        /// 如果zip文件不存在，将返回null
        /// 如果指定的条目在zip文件中不存在，将返回null
        /// 如果读取过程中发生异常，将捕获异常并返回null
        /// 如果操作被取消，将返回null
        /// </remarks>
        public static async Task<string> ReadFileContentAsync(string zipFilePath, string entryName, 
            Encoding encoding = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!System.IO.File.Exists(zipFilePath))
                    return null;

                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    ZipArchiveEntry entry = archive.GetEntry(entryName);
                    if (entry == null)
                        return null;

                    using (Stream stream = entry.Open())
                    using (StreamReader reader = new StreamReader(stream, encoding ?? Encoding.UTF8))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 向zip文件中添加或更新文件
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，如果不存在将被创建</param>
        /// <param name="entryName">要添加的文件在zip中的路径名称</param>
        /// <param name="content">要写入的文件内容</param>
        /// <param name="encoding">写入文件时使用的编码，默认为UTF8</param>
        /// <returns>操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法会向zip文件中添加或更新指定的文件
        /// 如果zip文件不存在，将创建新的zip文件
        /// 如果指定的条目在zip文件中已存在，将被覆盖
        /// 如果操作过程中发生异常，将捕获异常并返回false
        /// </remarks>
        public static bool AddOrUpdateFile(string zipFilePath, string entryName, string content, Encoding encoding = null)
        {
            try
            {
                bool isNewFile = !System.IO.File.Exists(zipFilePath);
                using (FileStream zipStream = new FileStream(zipFilePath, isNewFile ? FileMode.Create : FileMode.Open))
                {
                    using (ZipArchive archive = new ZipArchive(zipStream, isNewFile ? ZipArchiveMode.Create : ZipArchiveMode.Update))
                    {
                        // 如果条目已存在，先删除
                        ZipArchiveEntry existingEntry = archive.GetEntry(entryName);
                        if (existingEntry != null)
                            existingEntry.Delete();

                        ZipArchiveEntry entry = archive.CreateEntry(entryName);
                        using (Stream entryStream = entry.Open())
                        using (StreamWriter writer = new StreamWriter(entryStream, encoding ?? Encoding.UTF8))
                        {
                            writer.Write(content);
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 向zip文件中添加或更新文件（异步方法）
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，如果不存在将被创建</param>
        /// <param name="entryName">要添加的文件在zip中的路径名称</param>
        /// <param name="content">要写入的文件内容</param>
        /// <param name="encoding">写入文件时使用的编码，默认为UTF8</param>
        /// <param name="cancellationToken">取消令牌，用于取消异步操作</param>
        /// <returns>异步任务，返回操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法是AddOrUpdateFile的异步版本
        /// 如果zip文件不存在，将创建新的zip文件
        /// 如果指定的条目在zip文件中已存在，将被覆盖
        /// 如果操作过程中发生异常，将捕获异常并返回false
        /// 如果操作被取消，将返回false
        /// </remarks>
        public static async Task<bool> AddOrUpdateFileAsync(string zipFilePath, string entryName, string content, 
            Encoding encoding = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                bool isNewFile = !System.IO.File.Exists(zipFilePath);
                using (FileStream zipStream = new FileStream(zipFilePath, isNewFile ? FileMode.Create : FileMode.Open))
                {
                    using (ZipArchive archive = new ZipArchive(zipStream, isNewFile ? ZipArchiveMode.Create : ZipArchiveMode.Update))
                    {
                        // 如果条目已存在，先删除
                        ZipArchiveEntry existingEntry = archive.GetEntry(entryName);
                        if (existingEntry != null)
                            existingEntry.Delete();

                        ZipArchiveEntry entry = archive.CreateEntry(entryName);
                        using (Stream entryStream = entry.Open())
                        using (StreamWriter writer = new StreamWriter(entryStream, encoding ?? Encoding.UTF8))
                        {
                            await writer.WriteAsync(content);
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 在zip文件中创建文件夹
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，如果不存在将被创建</param>
        /// <param name="folderPath">要创建的文件夹路径，必须以斜杠结尾</param>
        /// <returns>操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法会在zip文件中创建指定的文件夹
        /// 如果zip文件不存在，将创建新的zip文件
        /// 如果指定的文件夹路径不以斜杠结尾，将自动添加斜杠
        /// 如果操作过程中发生异常，将捕获异常并返回false
        /// </remarks>
        public static bool CreateDirectory(string zipFilePath, string folderPath)
        {
            try
            {
                // 确保文件夹路径以斜杠结尾
                if (!folderPath.EndsWith("/"))
                    folderPath += "/";

                bool isNewFile = !System.IO.File.Exists(zipFilePath);
                using (FileStream zipStream = new FileStream(zipFilePath, isNewFile ? FileMode.Create : FileMode.Open))
                {
                    using (ZipArchive archive = new ZipArchive(zipStream, isNewFile ? ZipArchiveMode.Create : ZipArchiveMode.Update))
                    {
                        // 创建空条目作为文件夹
                        archive.CreateEntry(folderPath);
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 在zip文件中创建文件夹（异步方法）
        /// </summary>
        /// <param name="zipFilePath">zip文件路径，如果不存在将被创建</param>
        /// <param name="folderPath">要创建的文件夹路径，必须以斜杠结尾</param>
        /// <param name="cancellationToken">取消令牌，用于取消异步操作</param>
        /// <returns>异步任务，返回操作是否成功，成功返回true，失败返回false</returns>
        /// <remarks>
        /// 此方法是CreateDirectory的异步版本
        /// 如果zip文件不存在，将创建新的zip文件
        /// 如果指定的文件夹路径不以斜杠结尾，将自动添加斜杠
        /// 如果操作过程中发生异常，将捕获异常并返回false
        /// 如果操作被取消，将返回false
        /// </remarks>
        public static async Task<bool> CreateDirectoryAsync(string zipFilePath, string folderPath, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    // 确保文件夹路径以斜杠结尾
                    if (!folderPath.EndsWith("/"))
                        folderPath += "/";

                    bool isNewFile = !System.IO.File.Exists(zipFilePath);
                    using (FileStream zipStream = new FileStream(zipFilePath, isNewFile ? FileMode.Create : FileMode.Open))
                    {
                        using (ZipArchive archive = new ZipArchive(zipStream, isNewFile ? ZipArchiveMode.Create : ZipArchiveMode.Update))
                        {
                            // 创建空条目作为文件夹
                            archive.CreateEntry(folderPath);
                        }
                    }
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }, cancellationToken);
        }

        #endregion
    }
}