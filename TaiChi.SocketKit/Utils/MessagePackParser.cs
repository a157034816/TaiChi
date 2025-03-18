using System;
using System.IO;
using System.Threading.Tasks;
using TaiChi.Core.Utils;

namespace TaiChi.SocketKit.Utils
{
    /// <summary>
    /// 消息包解析器
    /// 用于解析由MessagePackBuilder构建的二进制消息包
    /// 消息包格式：
    /// - 4字节：总长度（不包含自身4字节）
    /// - 4字节：参数数量
    /// - 参数列表：每个参数由长度和数据组成
    /// </summary>
    public class MessagePackParser : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 内存流，用于读取二进制数据
        /// </summary>
        private MemoryStream _ms;
        
        /// <summary>
        /// 消息包总长度（不包含长度字段本身的4字节）
        /// </summary>
        private readonly int _len;
        
        /// <summary>
        /// 剩余参数数量
        /// </summary>
        private int _parameterCount;
        
        /// <summary>
        /// 标记是否已释放资源
        /// </summary>
        private bool _disposed = false;
        
        /// <summary>
        /// 当前已读取的参数索引
        /// </summary>
        public int CurrentParameterIndex { get; private set; }
        
        /// <summary>
        /// 参数总数
        /// </summary>
        public int TotalParameterCount { get; private set; }

        /// <summary>
        /// 初始化消息包解析器
        /// </summary>
        /// <param name="data">要解析的二进制数据</param>
        /// <exception cref="ArgumentNullException">data为null时抛出</exception>
        /// <exception cref="ArgumentException">data长度不足以包含头部信息时抛出</exception>
        public MessagePackParser(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data), "解析数据不能为null");
                
            if (data.Length < 8) // 至少需要包含长度和参数数量字段
                throw new ArgumentException("数据长度不足，无法解析消息包头部", nameof(data));
                
            _ms = new MemoryStream(data);
            
            try
            {
                _len = _ms.Read<int>();
                _parameterCount = _ms.Read<int>();
                TotalParameterCount = _parameterCount;
                CurrentParameterIndex = 0;
            }
            catch (EndOfStreamException)
            {
                throw new ArgumentException("数据格式错误，无法读取消息包头部", nameof(data));
            }
        }

        /// <summary>
        /// 尝试读取下一个结构体类型的参数
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="value">输出参数，成功时包含读取的值</param>
        /// <returns>是否成功读取参数</returns>
        /// <exception cref="ParameterLimitExceededException">没有更多参数可读时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public bool TryReadNextParameter<T>(out T value) where T : struct
        {
            ThrowIfDisposed();
            
            if (_parameterCount <= 0)
                throw new ParameterLimitExceededException($"已超过参数上限，总参数数量：{TotalParameterCount}");
                
            try
            {
                // 先读取长度，然后根据长度读取数据
                var size = _ms.Read<int>();
                value = _ms.Read<T>(size);
                _parameterCount--;
                CurrentParameterIndex++;
                return true;
            }
            catch (EndOfStreamException)
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// 尝试读取下一个字符串类型的参数
        /// </summary>
        /// <param name="str">输出参数，成功时包含读取的字符串</param>
        /// <returns>是否成功读取参数</returns>
        /// <exception cref="ParameterLimitExceededException">没有更多参数可读时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public bool TryReadNextParameter(out string str)
        {
            ThrowIfDisposed();
            
            if (_parameterCount <= 0)
                throw new ParameterLimitExceededException($"已超过参数上限，总参数数量：{TotalParameterCount}");
                
            try
            {
                // 先读取长度，然后根据长度读取字符串
                var len = _ms.Read<int>();
                str = _ms.ReadString(len);
                _parameterCount--;
                CurrentParameterIndex++;
                return true;
            }
            catch (EndOfStreamException)
            {
                str = null;
                return false;
            }
        }
        
        /// <summary>
        /// 获取剩余未读取的参数数量
        /// </summary>
        /// <returns>剩余参数数量</returns>
        public int GetRemainingParameterCount()
        {
            return _parameterCount;
        }
        
        /// <summary>
        /// 检查是否还有更多参数可读
        /// </summary>
        /// <returns>是否还有更多参数</returns>
        public bool HasMoreParameters()
        {
            return _parameterCount > 0;
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MessagePackParser), "消息包解析器已释放");
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
            }
        }

        /// <summary>
        /// 异步释放资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                if (_ms != null)
                {
                    await _ms.DisposeAsync();
                    _ms = null;
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 参数上限异常，当尝试读取超过消息包中定义的参数数量时抛出
        /// </summary>
        public class ParameterLimitExceededException : Exception
        {
            /// <summary>
            /// 初始化参数上限异常
            /// </summary>
            public ParameterLimitExceededException() : base("已超过参数组上限") { }
            
            /// <summary>
            /// 使用指定错误消息初始化参数上限异常
            /// </summary>
            /// <param name="message">错误消息</param>
            public ParameterLimitExceededException(string message) : base(message) { }
            
            /// <summary>
            /// 使用指定错误消息和内部异常初始化参数上限异常
            /// </summary>
            /// <param name="message">错误消息</param>
            /// <param name="innerException">内部异常</param>
            public ParameterLimitExceededException(string message, Exception innerException) 
                : base(message, innerException) { }
        }
    }
}