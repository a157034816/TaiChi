using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaiChi.Core.Utils;

namespace TaiChi.SocketKit.Utils
{
    /// <summary>
    /// 消息包构造器
    /// 用于构建二进制消息包，支持结构体和字符串的序列化
    /// 消息包格式：
    /// - 4字节：总长度（不包含自身4字节）
    /// - 4字节：参数数量
    /// - 参数列表：每个参数由长度和数据组成
    /// </summary>
    public class MessagePackBuilder : IDisposable
    {
        /// <summary>
        /// 消息包总长度（不包含长度字段本身的4字节）
        /// </summary>
        private int _len = 0;
        
        /// <summary>
        /// 参数数量
        /// </summary>
        private int _paramsCount = 0;
        
        /// <summary>
        /// 内存流，用于构建二进制数据
        /// </summary>
        private MemoryStream _ms;
        
        /// <summary>
        /// 标记是否已释放资源
        /// </summary>
        private bool _disposed = false;

#if DEBUG
        /// <summary>
        /// 调试模式下保存参数列表，便于调试
        /// </summary>
        private List<object> _params = new List<object>();
#endif

        /// <summary>
        /// 初始化消息包构造器
        /// </summary>
        public MessagePackBuilder()
        {
            _ms = new MemoryStream();
            _len += _ms.Write<int>(0); // 写入长度占位符（除前4位以外占用的字节数）
            _len += _ms.Write<int>(0); // 写入参数数量占位符
        }

        /// <summary>
        /// 写入结构体类型的参数
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="value">要写入的值</param>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public void Write<T>(T value) where T : struct
        {
            ThrowIfDisposed();
            
            byte[] bytes = ConvertHelper.GetBytes(value);
            _paramsCount++;
            _len += _ms.WriteAndRefLen(BitConverter.GetBytes(bytes.Length)); // 写入参数长度
            _len += _ms.WriteAndRefLen(bytes); // 写入参数数据
        
#if DEBUG
            _params.Add(value);
#endif
        }

        /// <summary>
        /// 写入字符串类型的参数
        /// </summary>
        /// <param name="str">要写入的字符串</param>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public void WriteString(string str)
        {
            ThrowIfDisposed();
            
            if (str == null)
                str = string.Empty;
                
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            _paramsCount++;
            _len += _ms.WriteAndRefLen(BitConverter.GetBytes(bytes.Length)); // 写入参数长度
            _len += _ms.WriteAndRefLen(bytes); // 写入参数数据
            
#if DEBUG
            _params.Add(str);
#endif
        }

        /// <summary>
        /// 获取构建好的二进制数据
        /// </summary>
        /// <returns>包含所有数据的字节数组</returns>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public byte[] GetData()
        {
            ThrowIfDisposed();
            
            var position = _ms.Position;
            _ms.Seek(0, SeekOrigin.Begin);
            _ms.Write(BitConverter.GetBytes(_len)); // 写入总长度
            _ms.Write(BitConverter.GetBytes(_paramsCount)); // 写入参数数量
            _ms.Seek(position, SeekOrigin.Begin);

            return _ms.ToArray();
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MessagePackBuilder), "消息包构造器已释放");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _ms?.Dispose();
                _ms = null;
                _disposed = true;
                
#if DEBUG
                _params?.Clear();
                _params = null;
#endif
            }
        }
        
#if DEBUG
        /// <summary>
        /// 获取调试信息
        /// </summary>
        /// <returns>包含参数信息的字符串</returns>
        public override string ToString()
        {
            return $"参数数量: {_paramsCount}, 总长度: {_len}, 参数: {string.Join(", ", _params)}";
        }
#endif
    }
}