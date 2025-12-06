using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace TaiChi.IO.Ports.Utils
{
    /// <summary>
    /// 串口通信包装类，用于处理串口的打开/关闭及数据的接收/发送
    /// </summary>
    public class SerialPortWrapper : IDisposable
    {
        private SerialPort? _serialPort;
        private bool _isDisposed;
        private readonly object _lockObject = new object();
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 3;
        private int _readTimeout = 1000;
        private int _writeTimeout = 1000;

        /// <summary>
        /// 串口接收数据事件
        /// </summary>
        public event EventHandler<PortDataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// 串口错误事件
        /// </summary>
        public event EventHandler<PortErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// 获取串口是否已打开
        /// </summary>
        public bool IsOpen => _serialPort?.IsOpen ?? false;

        /// <summary>
        /// 获取当前串口名称
        /// </summary>
        public string? PortName => _serialPort?.PortName;

        /// <summary>
        /// 获取当前波特率
        /// </summary>
        public int BaudRate => _serialPort?.BaudRate ?? 9600;

        /// <summary>
        /// 获取或设置接收缓冲区大小
        /// </summary>
        public int ReadBufferSize
        {
            get => _serialPort?.ReadBufferSize ?? 4096;
            set
            {
                if (_serialPort != null)
                {
                    _serialPort.ReadBufferSize = value;
                }
            }
        }

        /// <summary>
        /// 获取或设置发送缓冲区大小
        /// </summary>
        public int WriteBufferSize
        {
            get => _serialPort?.WriteBufferSize ?? 2048;
            set
            {
                if (_serialPort != null)
                {
                    _serialPort.WriteBufferSize = value;
                }
            }
        }

        /// <summary>
        /// 获取或设置读取超时时间（毫秒）
        /// </summary>
        public int ReadTimeout
        {
            get => _readTimeout;
            set
            {
                _readTimeout = value;
                if (_serialPort != null)
                {
                    _serialPort.ReadTimeout = value;
                }
            }
        }

        /// <summary>
        /// 获取或设置写入超时时间（毫秒）
        /// </summary>
        public int WriteTimeout
        {
            get => _writeTimeout;
            set
            {
                _writeTimeout = value;
                if (_serialPort != null)
                {
                    _serialPort.WriteTimeout = value;
                }
            }
        }

        /// <summary>
        /// 获取最近一次发生的错误信息
        /// </summary>
        public string? LastError { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SerialPortWrapper()
        {
        }

        /// <summary>
        /// 打开串口
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <returns>是否成功打开</returns>
        public bool Open(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None)
        {
            lock (_lockObject)
            {
                try
                {
                    // 参数验证
                    if (string.IsNullOrEmpty(portName))
                    {
                        LastError = "串口名称不能为空";
                        OnErrorOccurred(new PortErrorEventArgs(LastError));
                        return false;
                    }

                    // 检查端口是否存在
                    string[] availablePorts = SerialPort.GetPortNames();
                    bool portExists = Array.Exists(availablePorts, p => string.Equals(p, portName, StringComparison.OrdinalIgnoreCase));
                    if (!portExists)
                    {
                        LastError = $"串口 {portName} 不存在";
                        OnErrorOccurred(new PortErrorEventArgs(LastError));
                        return false;
                    }

                    // 如果已经打开，先关闭
                    Close();

                    _serialPort = new SerialPort
                    {
                        PortName = portName,
                        BaudRate = baudRate,
                        DataBits = dataBits,
                        StopBits = stopBits,
                        Parity = parity,
                        ReadTimeout = _readTimeout,
                        WriteTimeout = _writeTimeout,
                        DtrEnable = true,  // 启用DTR控制信号
                        RtsEnable = true   // 启用RTS控制信号
                    };

                    // 注册数据接收事件
                    _serialPort.DataReceived += SerialPort_DataReceived;
                    _serialPort.ErrorReceived += SerialPort_ErrorReceived;

                    // 打开串口
                    _serialPort.Open();
                    _reconnectAttempts = 0;
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    LastError = $"串口 {portName} 访问被拒绝，可能被其他程序占用";
                    OnErrorOccurred(new PortErrorEventArgs(LastError));
                    return false;
                }
                catch (ArgumentException ex)
                {
                    LastError = $"串口参数无效: {ex.Message}";
                    OnErrorOccurred(new PortErrorEventArgs(ex));
                    return false;
                }
                catch (Exception ex)
                {
                    LastError = $"打开串口失败: {ex.Message}";
                    OnErrorOccurred(new PortErrorEventArgs(ex));
                    return false;
                }
            }
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        public void Close()
        {
            lock (_lockObject)
            {
                if (_serialPort != null)
                {
                    try
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.DiscardInBuffer();
                            _serialPort.DiscardOutBuffer();
                            _serialPort.DataReceived -= SerialPort_DataReceived;
                            _serialPort.ErrorReceived -= SerialPort_ErrorReceived;
                            _serialPort.Close();
                        }
                        _serialPort.Dispose();
                        _serialPort = null;
                    }
                    catch (Exception ex)
                    {
                        LastError = $"关闭串口失败: {ex.Message}";
                        OnErrorOccurred(new PortErrorEventArgs(ex));
                    }
                }
            }
        }

        /// <summary>
        /// 尝试重新连接串口
        /// </summary>
        /// <returns>是否重连成功</returns>
        public bool TryReconnect()
        {
            lock (_lockObject)
            {
                if (_serialPort == null || _reconnectAttempts >= MaxReconnectAttempts)
                {
                    return false;
                }

                try
                {
                    _reconnectAttempts++;
                    string portName = _serialPort.PortName;
                    int baudRate = _serialPort.BaudRate;
                    int dataBits = _serialPort.DataBits;
                    StopBits stopBits = _serialPort.StopBits;
                    Parity parity = _serialPort.Parity;

                    // 关闭当前连接
                    Close();

                    // 等待一小段时间
                    Thread.Sleep(500);

                    // 重新打开
                    return Open(portName, baudRate, dataBits, stopBits, parity);
                }
                catch (Exception ex)
                {
                    LastError = $"重新连接失败: {ex.Message}";
                    OnErrorOccurred(new PortErrorEventArgs(ex));
                    return false;
                }
            }
        }

        /// <summary>
        /// 发送数据（字节数组）
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <returns>是否发送成功</returns>
        public bool Send(byte[] data)
        {
            if (_serialPort == null || !_serialPort.IsOpen || data == null || data.Length == 0)
            {
                LastError = "串口未打开或数据为空";
                return false;
            }

            lock (_lockObject)
            {
                try
                {
                    _serialPort.DiscardOutBuffer(); // 清空发送缓冲区
                    _serialPort.Write(data, 0, data.Length);
                    return true;
                }
                catch (TimeoutException)
                {
                    LastError = "发送数据超时";
                    OnErrorOccurred(new PortErrorEventArgs(LastError));
                    return false;
                }
                catch (InvalidOperationException)
                {
                    LastError = "串口已关闭或无效";
                    OnErrorOccurred(new PortErrorEventArgs(LastError));
                    
                    // 尝试重新连接
                    if (TryReconnect())
                    {
                        // 重新发送数据
                        return Send(data);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    LastError = $"发送数据失败: {ex.Message}";
                    OnErrorOccurred(new PortErrorEventArgs(ex));
                    return false;
                }
            }
        }

        /// <summary>
        /// 发送字符串数据
        /// </summary>
        /// <param name="data">要发送的字符串</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <returns>是否发送成功</returns>
        public bool Send(string data, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(data))
            {
                return false;
            }

            encoding ??= Encoding.UTF8;
            byte[] bytes = encoding.GetBytes(data);
            return Send(bytes);
        }

        /// <summary>
        /// 发送十六进制字符串
        /// </summary>
        /// <param name="hexString">十六进制字符串，如"01 02 03 0A"</param>
        /// <returns>是否发送成功</returns>
        public bool SendHex(string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
            {
                return false;
            }

            try
            {
                // 去除空格
                hexString = hexString.Replace(" ", "");

                // 如果字符串长度为奇数，补0
                if (hexString.Length % 2 != 0)
                {
                    hexString = "0" + hexString;
                }

                byte[] bytes = new byte[hexString.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
                }

                return Send(bytes);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new PortErrorEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task<bool> SendAsync(byte[] data)
        {
            return await Task.Run(() => Send(data));
        }

        /// <summary>
        /// 异步发送字符串
        /// </summary>
        /// <param name="data">要发送的字符串</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task<bool> SendAsync(string data, Encoding? encoding = null)
        {
            return await Task.Run(() => Send(data, encoding));
        }

        /// <summary>
        /// 异步发送十六进制字符串
        /// </summary>
        /// <param name="hexString">十六进制字符串</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task<bool> SendHexAsync(string hexString)
        {
            return await Task.Run(() => SendHex(hexString));
        }

        /// <summary>
        /// 获取可用串口列表
        /// </summary>
        /// <returns>可用串口名称数组</returns>
        public static string[] GetPortNames()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// 串口数据接收处理
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                return;
            }

            byte[] buffer;
            int bytesToRead;

            try
            {
                // 延时一小段时间，确保数据全部到达缓冲区
                Thread.Sleep(50);

                lock (_lockObject)
                {
                    // 获取接收到的字节数
                    bytesToRead = _serialPort.BytesToRead;
                    if (bytesToRead <= 0)
                    {
                        return;
                    }

                    // 读取数据
                    buffer = new byte[bytesToRead];
                    _serialPort.Read(buffer, 0, bytesToRead);
                }

                // 触发数据接收事件
                OnDataReceived(new PortDataReceivedEventArgs(buffer));
            }
            catch (TimeoutException)
            {
                LastError = "读取数据超时";
                OnErrorOccurred(new PortErrorEventArgs(LastError));
            }
            catch (InvalidOperationException)
            {
                LastError = "串口已关闭或无效";
                OnErrorOccurred(new PortErrorEventArgs(LastError));
                // 尝试重新连接
                TryReconnect();
            }
            catch (Exception ex)
            {
                LastError = $"接收数据失败: {ex.Message}";
                OnErrorOccurred(new PortErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// 串口错误处理
        /// </summary>
        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            OnErrorOccurred(new PortErrorEventArgs($"串口错误: {e.EventType}"));
        }

        /// <summary>
        /// 触发数据接收事件
        /// </summary>
        protected virtual void OnDataReceived(PortDataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        protected virtual void OnErrorOccurred(PortErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否为显式释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        Close();
                    }
                    catch (Exception ex)
                    {
                        // 在析构时不抛出异常
                        LastError = $"释放资源时发生错误: {ex.Message}";
                    }
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~SerialPortWrapper()
        {
            Dispose(false);
        }

        /// <summary>
        /// 清空接收缓冲区
        /// </summary>
        public void ClearReceiveBuffer()
        {
            lock (_lockObject)
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    try
                    {
                        _serialPort.DiscardInBuffer();
                    }
                    catch (Exception ex)
                    {
                        LastError = $"清空接收缓冲区失败: {ex.Message}";
                        OnErrorOccurred(new PortErrorEventArgs(ex));
                    }
                }
            }
        }

        /// <summary>
        /// 清空发送缓冲区
        /// </summary>
        public void ClearSendBuffer()
        {
            lock (_lockObject)
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    try
                    {
                        _serialPort.DiscardOutBuffer();
                    }
                    catch (Exception ex)
                    {
                        LastError = $"清空发送缓冲区失败: {ex.Message}";
                        OnErrorOccurred(new PortErrorEventArgs(ex));
                    }
                }
            }
        }
    }

    /// <summary>
    /// 串口数据接收事件参数
    /// </summary>
    public class PortDataReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// 接收到的数据
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// 数据长度
        /// </summary>
        public int Length => Data.Length;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="data">接收到的数据</param>
        public PortDataReceivedEventArgs(byte[] data)
        {
            Data = data;
        }

        /// <summary>
        /// 将接收到的数据转换为字符串
        /// </summary>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <returns>转换后的字符串</returns>
        public string GetString(Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            return encoding.GetString(Data);
        }

        /// <summary>
        /// 将接收到的数据转换为十六进制字符串
        /// </summary>
        /// <param name="separator">分隔符，默认为空格</param>
        /// <returns>十六进制字符串</returns>
        public string GetHexString(string separator = " ")
        {
            if (Data == null || Data.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Data.Length; i++)
            {
                sb.Append(Data[i].ToString("X2"));
                if (i < Data.Length - 1 && !string.IsNullOrEmpty(separator))
                {
                    sb.Append(separator);
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 串口错误事件参数
    /// </summary>
    public class PortErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 错误信息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 异常对象
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">错误信息</param>
        public PortErrorEventArgs(string message)
        {
            Message = message;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="exception">异常对象</param>
        public PortErrorEventArgs(Exception exception)
        {
            Exception = exception;
            Message = exception.Message;
        }
    }
}
