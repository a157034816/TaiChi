using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TaiChi.Core.Utils
{
    /// <summary>
    /// 提供各种数据类型转换的辅助方法
    /// </summary>
    public static class ConvertHelper
    {
        /// <summary>
        /// 将字节数组转换为指定的结构体类型
        /// </summary>
        /// <typeparam name="T">目标结构体类型</typeparam>
        /// <param name="bytes">要转换的字节数组</param>
        /// <returns>转换后的结构体实例</returns>
        /// <exception cref="ArgumentNullException">字节数组为null时抛出</exception>
        /// <exception cref="ArgumentException">字节数组长度与结构体大小不匹配时抛出</exception>
        public static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes), "字节数组不能为null。");
            }

            int size = Marshal.SizeOf(typeof(T));
            if (size != bytes.Length)
            {
                throw new ArgumentException($"字节数组的长度({bytes.Length})必须与结构体的大小({size})一致。", nameof(bytes));
            }

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(bytes, 0, ptr, size);
                return (T)Marshal.PtrToStructure(ptr, typeof(T));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"将字节数组转换为{typeof(T).Name}结构体时发生错误。", ex);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        /// <summary>
        /// 将结构体转换为字节数组
        /// </summary>
        /// <typeparam name="T">源结构体类型</typeparam>
        /// <param name="structure">要转换的结构体</param>
        /// <returns>转换后的字节数组</returns>
        /// <exception cref="ArgumentException">不支持的结构体类型时抛出</exception>
        public static byte[] StructureToByteArray<T>(T structure) where T : struct
        {
            int size = Marshal.SizeOf(structure);
            byte[] bytes = new byte[size];
            IntPtr ptr = IntPtr.Zero;
            
            try
            {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(structure, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
                return bytes;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"将{typeof(T).Name}结构体转换为字节数组时发生错误。", ex);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        /// <summary>
        /// 获取基本类型值的字节数组表示
        /// </summary>
        /// <typeparam name="T">值的类型</typeparam>
        /// <param name="value">要转换的值</param>
        /// <returns>转换后的字节数组</returns>
        /// <exception cref="ArgumentException">不支持的类型时抛出</exception>
        public static byte[] GetBytes<T>(T value) where T : struct
        {
            // 根据类型转换并获取字节数组
            if (value is bool b)
                return BitConverter.GetBytes(b);
            else if (value is byte bt)
                return new byte[] { bt };
            else if (value is sbyte sbt)
                return new byte[] { (byte)sbt };
            else if (value is char c)
                return BitConverter.GetBytes(c);
            else if (value is int i)
                return BitConverter.GetBytes(i);
            else if (value is uint ui)
                return BitConverter.GetBytes(ui);
            else if (value is short sh)
                return BitConverter.GetBytes(sh);
            else if (value is ushort ush)
                return BitConverter.GetBytes(ush);
            else if (value is long l)
                return BitConverter.GetBytes(l);
            else if (value is ulong ul)
                return BitConverter.GetBytes(ul);
            else if (value is float f)
                return BitConverter.GetBytes(f);
            else if (value is double d)
                return BitConverter.GetBytes(d);
            else if (value is decimal dec)
                return decimal.GetBits(dec).SelectMany(i => BitConverter.GetBytes(i)).ToArray();
            else if (value is DateTime dt)
                return BitConverter.GetBytes(dt.Ticks);
            else if (value is Guid guid)
                return guid.ToByteArray();
            else
            {
                try
                {
                    // 尝试使用结构体转换方法
                    return StructureToByteArray(value);
                }
                catch
                {
                    throw new ArgumentException($"不支持类型 {typeof(T).Name} 的转换。");
                }
            }
        }

        /// <summary>
        /// 从字节数组转换为指定的基本类型值
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="bytes">字节数组</param>
        /// <returns>转换后的值</returns>
        /// <exception cref="ArgumentNullException">字节数组为null时抛出</exception>
        /// <exception cref="ArgumentException">字节数组长度不正确或不支持的类型时抛出</exception>
        public static T FromBytes<T>(byte[] bytes) where T : struct
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes), "字节数组不能为null。");
            }

            Type type = typeof(T);

            if (type == typeof(bool))
            {
                if (bytes.Length < 1)
                    throw new ArgumentException("字节数组长度不足以转换为bool类型。", nameof(bytes));
                return (T)(object)BitConverter.ToBoolean(bytes, 0);
            }
            else if (type == typeof(byte))
            {
                if (bytes.Length < 1)
                    throw new ArgumentException("字节数组长度不足以转换为byte类型。", nameof(bytes));
                return (T)(object)bytes[0];
            }
            else if (type == typeof(sbyte))
            {
                if (bytes.Length < 1)
                    throw new ArgumentException("字节数组长度不足以转换为sbyte类型。", nameof(bytes));
                return (T)(object)(sbyte)bytes[0];
            }
            else if (type == typeof(char))
            {
                if (bytes.Length < 2)
                    throw new ArgumentException("字节数组长度不足以转换为char类型。", nameof(bytes));
                return (T)(object)BitConverter.ToChar(bytes, 0);
            }
            else if (type == typeof(int))
            {
                if (bytes.Length < 4)
                    throw new ArgumentException("字节数组长度不足以转换为int类型。", nameof(bytes));
                return (T)(object)BitConverter.ToInt32(bytes, 0);
            }
            else if (type == typeof(uint))
            {
                if (bytes.Length < 4)
                    throw new ArgumentException("字节数组长度不足以转换为uint类型。", nameof(bytes));
                return (T)(object)BitConverter.ToUInt32(bytes, 0);
            }
            else if (type == typeof(short))
            {
                if (bytes.Length < 2)
                    throw new ArgumentException("字节数组长度不足以转换为short类型。", nameof(bytes));
                return (T)(object)BitConverter.ToInt16(bytes, 0);
            }
            else if (type == typeof(ushort))
            {
                if (bytes.Length < 2)
                    throw new ArgumentException("字节数组长度不足以转换为ushort类型。", nameof(bytes));
                return (T)(object)BitConverter.ToUInt16(bytes, 0);
            }
            else if (type == typeof(long))
            {
                if (bytes.Length < 8)
                    throw new ArgumentException("字节数组长度不足以转换为long类型。", nameof(bytes));
                return (T)(object)BitConverter.ToInt64(bytes, 0);
            }
            else if (type == typeof(ulong))
            {
                if (bytes.Length < 8)
                    throw new ArgumentException("字节数组长度不足以转换为ulong类型。", nameof(bytes));
                return (T)(object)BitConverter.ToUInt64(bytes, 0);
            }
            else if (type == typeof(float))
            {
                if (bytes.Length < 4)
                    throw new ArgumentException("字节数组长度不足以转换为float类型。", nameof(bytes));
                return (T)(object)BitConverter.ToSingle(bytes, 0);
            }
            else if (type == typeof(double))
            {
                if (bytes.Length < 8)
                    throw new ArgumentException("字节数组长度不足以转换为double类型。", nameof(bytes));
                return (T)(object)BitConverter.ToDouble(bytes, 0);
            }
            else if (type == typeof(decimal))
            {
                if (bytes.Length < 16)
                    throw new ArgumentException("字节数组长度不足以转换为decimal类型。", nameof(bytes));
                
                int[] bits = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    bits[i] = BitConverter.ToInt32(bytes, i * 4);
                }
                return (T)(object)new decimal(bits);
            }
            else if (type == typeof(DateTime))
            {
                if (bytes.Length < 8)
                    throw new ArgumentException("字节数组长度不足以转换为DateTime类型。", nameof(bytes));
                long ticks = BitConverter.ToInt64(bytes, 0);
                return (T)(object)new DateTime(ticks);
            }
            else if (type == typeof(Guid))
            {
                if (bytes.Length < 16)
                    throw new ArgumentException("字节数组长度不足以转换为Guid类型。", nameof(bytes));
                return (T)(object)new Guid(bytes);
            }
            else
            {
                // 尝试使用结构体转换方法
                return ByteArrayToStructure<T>(bytes);
            }
        }

        /// <summary>
        /// 将字节数组转换为十六进制字符串
        /// </summary>
        /// <param name="bytes">要转换的字节数组</param>
        /// <param name="separator">十六进制值之间的分隔符（可选）</param>
        /// <returns>十六进制字符串</returns>
        public static string BytesToHexString(byte[] bytes, string separator = "")
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder(bytes.Length * (2 + separator.Length));
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
                if (i < bytes.Length - 1 && !string.IsNullOrEmpty(separator))
                    sb.Append(separator);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 将十六进制字符串转换为字节数组
        /// </summary>
        /// <param name="hexString">十六进制字符串</param>
        /// <returns>字节数组</returns>
        /// <exception cref="ArgumentException">无效的十六进制字符串时抛出</exception>
        public static byte[] HexStringToBytes(string hexString)
        {
            if (string.IsNullOrWhiteSpace(hexString))
                return new byte[0];

            // 移除所有非十六进制字符（空格、连字符等）
            hexString = new string(hexString.Where(c => char.IsDigit(c) || 
                                                       (c >= 'a' && c <= 'f') || 
                                                       (c >= 'A' && c <= 'F')).ToArray());

            if (hexString.Length % 2 != 0)
                throw new ArgumentException("十六进制字符串长度必须为偶数。", nameof(hexString));

            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                string byteValue = hexString.Substring(i * 2, 2);
                try
                {
                    bytes[i] = Convert.ToByte(byteValue, 16);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"无法将字符串'{byteValue}'转换为字节。", ex);
                }
            }

            return bytes;
        }

        /// <summary>
        /// 将字节数组转换为Base64字符串
        /// </summary>
        /// <param name="bytes">要转换的字节数组</param>
        /// <returns>Base64字符串</returns>
        public static string BytesToBase64String(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 将Base64字符串转换为字节数组
        /// </summary>
        /// <param name="base64String">Base64字符串</param>
        /// <returns>字节数组</returns>
        /// <exception cref="FormatException">无效的Base64字符串时抛出</exception>
        public static byte[] Base64StringToBytes(string base64String)
        {
            if (string.IsNullOrWhiteSpace(base64String))
                return new byte[0];

            try
            {
                return Convert.FromBase64String(base64String);
            }
            catch (FormatException ex)
            {
                throw new FormatException("提供的字符串不是有效的Base64格式。", ex);
            }
        }
    }
}