using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TaiChi.IO.File
{
    public static class FileHelper
    {
        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="encoding">编码格式，默认为UTF-8</param>
        /// <returns>文件内容字符串</returns>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">读取文件失败</exception>
        public static string ReadFile(string filePath, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            encoding ??= Encoding.UTF8;
            
            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            try
            {
                using (StreamReader sr = new StreamReader(filePath, encoding))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException)
                {
                    throw;
                }
                throw new IOException($"读取文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步读取文件内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="encoding">编码格式，默认为UTF-8</param>
        /// <returns>文件内容字符串</returns>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">读取文件失败</exception>
        public static async Task<string> ReadFileAsync(string filePath, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            encoding ??= Encoding.UTF8;
            
            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            try
            {
                using (StreamReader sr = new StreamReader(filePath, encoding))
                {
                    return await sr.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException)
                {
                    throw;
                }
                throw new IOException($"读取文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 写入文件内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="content">要写入的内容</param>
        /// <param name="encoding">编码格式，默认为UTF-8</param>
        /// <param name="append">是否追加内容，默认为覆盖</param>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        /// <exception cref="IOException">写入文件失败</exception>
        public static void WriteFile(string filePath, string content, Encoding? encoding = null, bool append = false)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            encoding ??= Encoding.UTF8;
            
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (StreamWriter sw = new StreamWriter(filePath, append, encoding))
                {
                    sw.Write(content ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"写入文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 写入二进制数据到文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="data">要写入的二进制数据</param>
        /// <param name="append">是否追加内容，默认为覆盖</param>
        /// <exception cref="ArgumentNullException">文件路径为空或数据为null</exception>
        /// <exception cref="IOException">写入文件失败</exception>
        public static void WriteFile(string filePath, byte[] data, bool append = false)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "数据不能为null");
            }

            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (FileStream fs = new FileStream(filePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write))
                {
                    fs.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"写入文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步写入文件内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="content">要写入的内容</param>
        /// <param name="encoding">编码格式，默认为UTF-8</param>
        /// <param name="append">是否追加内容，默认为覆盖</param>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        /// <exception cref="IOException">写入文件失败</exception>
        public static async Task WriteFileAsync(string filePath, string content, Encoding? encoding = null, bool append = false)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            encoding ??= Encoding.UTF8;
            
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (StreamWriter sw = new StreamWriter(filePath, append, encoding))
                {
                    await sw.WriteAsync(content ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"写入文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步写入二进制数据到文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="data">要写入的二进制数据</param>
        /// <param name="append">是否追加内容，默认为覆盖</param>
        /// <exception cref="ArgumentNullException">文件路径为空或数据为null</exception>
        /// <exception cref="IOException">写入文件失败</exception>
        public static async Task WriteFileAsync(string filePath, byte[] data, bool append = false)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "数据不能为null");
            }

            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (FileStream fs = new FileStream(filePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write))
                {
                    await fs.WriteAsync(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"写入文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 复制文件
        /// </summary>
        /// <param name="sourceFile">源文件路径</param>
        /// <param name="destinationFile">目标文件路径</param>
        /// <param name="overwrite">如果目标文件存在，是否覆盖</param>
        /// <exception cref="ArgumentNullException">源文件或目标文件路径为空</exception>
        /// <exception cref="FileNotFoundException">源文件不存在</exception>
        /// <exception cref="IOException">复制文件失败</exception>
        public static void CopyFile(string sourceFile, string destinationFile, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(sourceFile))
            {
                throw new ArgumentNullException(nameof(sourceFile), "源文件路径不能为空");
            }

            if (string.IsNullOrEmpty(destinationFile))
            {
                throw new ArgumentNullException(nameof(destinationFile), "目标文件路径不能为空");
            }

            if (!System.IO.File.Exists(sourceFile))
            {
                throw new FileNotFoundException("源文件不存在", sourceFile);
            }

            try
            {
                // 确保目标目录存在
                string directory = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                System.IO.File.Copy(sourceFile, destinationFile, overwrite);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is ArgumentNullException)
                {
                    throw;
                }
                throw new IOException($"复制文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 移动文件
        /// </summary>
        /// <param name="sourceFile">源文件路径</param>
        /// <param name="destinationFile">目标文件路径</param>
        /// <param name="overwrite">如果目标文件存在，是否覆盖</param>
        /// <exception cref="ArgumentNullException">源文件或目标文件路径为空</exception>
        /// <exception cref="FileNotFoundException">源文件不存在</exception>
        /// <exception cref="IOException">移动文件失败</exception>
        public static void MoveFile(string sourceFile, string destinationFile, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(sourceFile))
            {
                throw new ArgumentNullException(nameof(sourceFile), "源文件路径不能为空");
            }

            if (string.IsNullOrEmpty(destinationFile))
            {
                throw new ArgumentNullException(nameof(destinationFile), "目标文件路径不能为空");
            }

            if (!System.IO.File.Exists(sourceFile))
            {
                throw new FileNotFoundException("源文件不存在", sourceFile);
            }

            try
            {
                // 确保目标目录存在
                string directory = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (overwrite && System.IO.File.Exists(destinationFile))
                {
                    System.IO.File.Delete(destinationFile);
                }

                System.IO.File.Move(sourceFile, destinationFile);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is ArgumentNullException)
                {
                    throw;
                }
                throw new IOException($"移动文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        /// <exception cref="IOException">删除文件失败</exception>
        public static void DeleteFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                if (ex is ArgumentNullException)
                {
                    throw;
                }
                throw new IOException($"删除文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件是否存在</returns>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        public static bool FileExists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            return System.IO.File.Exists(filePath);
        }

        /// <summary>
        /// 获取文件大小（以字节为单位）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件大小</returns>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">获取文件大小失败</exception>
        public static long GetFileSize(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            try
            {
                return new FileInfo(filePath).Length;
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is ArgumentNullException)
                {
                    throw;
                }
                throw new IOException($"获取文件大小失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建目录（如果不存在）
        /// </summary>
        /// <param name="directoryPath">目录路径</param>
        /// <exception cref="ArgumentNullException">目录路径为空</exception>
        /// <exception cref="IOException">创建目录失败</exception>
        public static void CreateDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath), "目录路径不能为空");
            }

            try
            {
                Directory.CreateDirectory(directoryPath); //如果已存在，则不执行任何操作
            }
            catch (Exception ex)
            {
                if (ex is ArgumentNullException)
                {
                    throw;
                }
                throw new IOException($"创建目录失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 删除目录及其所有子目录和文件
        /// </summary>
        /// <param name="directoryPath">目录路径</param>
        /// <exception cref="ArgumentNullException">目录路径为空</exception>
        /// <exception cref="IOException">删除目录失败</exception>
        public static void DeleteDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath), "目录路径不能为空");
            }

            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                }
            }
            catch (Exception ex)
            {
                if (ex is ArgumentNullException)
                {
                    throw;
                }
                throw new IOException($"删除目录失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 检查目录是否存在
        /// </summary>
        /// <param name="directoryPath">目录路径</param>
        /// <returns>目录是否存在</returns>
        /// <exception cref="ArgumentNullException">目录路径为空</exception>
        public static bool DirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath), "目录路径不能为空");
            }

            return Directory.Exists(directoryPath);
        }
        
        /// <summary>
        /// 获取文件的最后修改时间
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>最后修改时间</returns>
        /// <exception cref="ArgumentNullException">文件路径为空</exception>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="IOException">获取修改时间失败</exception>
        public static DateTime GetLastModifiedTime(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            try
            {
                return System.IO.File.GetLastWriteTime(filePath);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is ArgumentNullException)
                {
                    throw;
                }
                throw new IOException($"获取文件修改时间失败: {ex.Message}", ex);
            }
        }
    }
}