using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TaiChi.Core.Utils
{
    public static class StreamHelper
    {
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="stream">数据流</param>
        /// <param name="value">值</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>写入的长度</returns>
        /// <exception cref="ArgumentNullException">stream 为 null 时抛出</exception>
        public static int Write<T>(this Stream stream, T value) where T : struct
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite)
                throw new NotSupportedException("流不支持写入操作");

            byte[] bytes = ConvertHelper.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }

        /// <summary>
        /// 写入byte[]数据
        /// </summary>
        /// <param name="stream">数据流</param>
        /// <param name="bytes">要写入的字节数组</param>
        /// <returns>写入的长度</returns>
        /// <exception cref="ArgumentNullException">stream 或 bytes 为 null 时抛出</exception>
        public static int WriteAndRefLen(this Stream stream, byte[] bytes)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (!stream.CanWrite)
                throw new NotSupportedException("流不支持写入操作");

            if (bytes.Length == 0)
                return 0;

            stream.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }

        /// <summary>
        /// 写入string数据
        /// </summary>
        /// <param name="stream">数据流</param>
        /// <param name="str">要写入的字符串</param>
        /// <param name="encoding">编码方式，默认为UTF8</param>
        /// <returns>写入的长度</returns>
        /// <exception cref="ArgumentNullException">stream 为 null 时抛出</exception>
        public static int WriteString(this Stream stream, string str, Encoding encoding = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite)
                throw new NotSupportedException("流不支持写入操作");

            if (string.IsNullOrEmpty(str))
                return 0;

            encoding = encoding ?? Encoding.UTF8;
            byte[] bytes = encoding.GetBytes(str);
            stream.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }

        /// <summary>
        /// 从流中读取指定类型的值
        /// </summary>
        /// <typeparam name="T">要读取的值的类型</typeparam>
        /// <param name="stream">要读取的流</param>
        /// <exception cref="ArgumentNullException">stream 为 null 时抛出</exception>
        /// <exception cref="NotSupportedException">流不支持读取时抛出</exception>
        /// <exception cref="EndOfStreamException">读取到流末尾时抛出</exception>
        /// <returns>读取的值</returns>
        public static T Read<T>(this Stream stream) where T : struct
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new NotSupportedException("流不支持读取操作");

            int size = Marshal.SizeOf(typeof(T));
            return ReadInternal<T>(stream, size);
        }

        /// <summary>
        /// 从流中读取指定大小的数据并转换为指定类型的值
        /// </summary>
        /// <typeparam name="T">要读取的值的类型</typeparam>
        /// <param name="stream">要读取的流</param>
        /// <param name="size">要读取的字节数</param>
        /// <exception cref="ArgumentNullException">stream 为 null 时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">size 小于等于 0 时抛出</exception>
        /// <exception cref="NotSupportedException">流不支持读取时抛出</exception>
        /// <exception cref="EndOfStreamException">读取到流末尾时抛出</exception>
        /// <returns>读取的值</returns>
        public static T Read<T>(this Stream stream, int size) where T : struct
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "读取大小必须大于0");
            if (!stream.CanRead)
                throw new NotSupportedException("流不支持读取操作");

            return ReadInternal<T>(stream, size);
        }

        /// <summary>
        /// 从流中读取指定长度的字符串
        /// </summary>
        /// <param name="stream">要读取的流</param>
        /// <param name="len">要读取的字节数</param>
        /// <param name="encoding">编码方式，默认为UTF8</param>
        /// <exception cref="ArgumentNullException">stream 为 null 时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">len 小于等于 0 时抛出</exception>
        /// <exception cref="NotSupportedException">流不支持读取时抛出</exception>
        /// <exception cref="EndOfStreamException">读取到流末尾时抛出</exception>
        /// <returns>读取的字符串</returns>
        public static string ReadString(this Stream stream, int len, Encoding encoding = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (len <= 0)
                throw new ArgumentOutOfRangeException(nameof(len), "读取长度必须大于0");
            if (!stream.CanRead)
                throw new NotSupportedException("流不支持读取操作");

            // 读取字符串数据
            byte[] buffer = new byte[len];
            int bytesRead = 0;
            int totalBytesRead = 0;
            
            // 循环读取，确保读取完整的数据
            while (totalBytesRead < len && (bytesRead = stream.Read(buffer, totalBytesRead, len - totalBytesRead)) > 0)
            {
                totalBytesRead += bytesRead;
            }

            if (totalBytesRead != len)
            {
                throw new EndOfStreamException($"读取流时到达流的末尾。请求读取 {len} 字节，但只读取到 {totalBytesRead} 字节。");
            }

            encoding = encoding ?? Encoding.UTF8;
            return encoding.GetString(buffer);
        }

        /// <summary>
        /// 尝试从流中读取指定长度的字符串，如果到达流末尾则返回已读取的部分
        /// </summary>
        /// <param name="stream">要读取的流</param>
        /// <param name="len">要读取的字节数</param>
        /// <param name="encoding">编码方式，默认为UTF8</param>
        /// <returns>读取的字符串</returns>
        public static string TryReadString(this Stream stream, int len, Encoding encoding = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (len <= 0)
                throw new ArgumentOutOfRangeException(nameof(len), "读取长度必须大于0");
            if (!stream.CanRead)
                throw new NotSupportedException("流不支持读取操作");

            byte[] buffer = new byte[len];
            int bytesRead = 0;
            int totalBytesRead = 0;
            
            // 循环读取，确保读取完整的数据或直到流结束
            while (totalBytesRead < len && (bytesRead = stream.Read(buffer, totalBytesRead, len - totalBytesRead)) > 0)
            {
                totalBytesRead += bytesRead;
            }

            encoding = encoding ?? Encoding.UTF8;
            
            if (totalBytesRead == 0)
                return string.Empty;
            
            if (totalBytesRead < len)
            {
                // 调整buffer大小以匹配实际读取的字节数
                byte[] actualBuffer = new byte[totalBytesRead];
                Array.Copy(buffer, actualBuffer, totalBytesRead);
                return encoding.GetString(actualBuffer);
            }
            
            return encoding.GetString(buffer);
        }

        /// <summary>
        /// 将流中的所有数据读取为字节数组
        /// </summary>
        /// <param name="stream">要读取的流</param>
        /// <returns>包含流中所有数据的字节数组</returns>
        public static byte[] ReadToEnd(this Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new NotSupportedException("流不支持读取操作");

            // 如果流支持查找，并且可以确定长度，则直接分配足够大小的缓冲区
            if (stream.CanSeek)
            {
                long length = stream.Length - stream.Position;
                if (length > 0)
                {
                    byte[] buffer = new byte[length];
                    int bytesRead = 0;
                    int totalBytesRead = 0;
                    
                    while (totalBytesRead < length && 
                           (bytesRead = stream.Read(buffer, totalBytesRead, (int)length - totalBytesRead)) > 0)
                    {
                        totalBytesRead += bytesRead;
                    }
                    
                    return buffer;
                }
                return new byte[0];
            }
            
            // 如果流不支持查找，则使用内存流来收集数据
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 内部方法：从流中读取指定大小的数据并转换为指定类型
        /// </summary>
        private static T ReadInternal<T>(Stream stream, int size) where T : struct
        {
            byte[] buffer = new byte[size];
            int bytesRead = 0;
            int totalBytesRead = 0;
            
            // 循环读取，确保读取完整的数据
            while (totalBytesRead < size && (bytesRead = stream.Read(buffer, totalBytesRead, size - totalBytesRead)) > 0)
            {
                totalBytesRead += bytesRead;
            }

            if (totalBytesRead != size)
            {
                throw new EndOfStreamException($"读取流时到达流的末尾。请求读取 {size} 字节，但只读取到 {totalBytesRead} 字节。");
            }

            return ConvertHelper.ByteArrayToStructure<T>(buffer);
        }
    }
}