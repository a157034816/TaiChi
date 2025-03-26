using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace TaiChi.IO.Ports.Utils
{
    /// <summary>
    /// 串口辅助类，提供获取串口信息、遍历串口设备等功能
    /// </summary>
    public static class SerialPortHelper
    {
        /// <summary>
        /// 获取所有可用串口名称
        /// </summary>
        /// <returns>可用串口名称数组</returns>
        public static string[] GetPortNames()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// 获取详细的串口设备信息列表
        /// </summary>
        /// <returns>串口设备信息列表</returns>
        public static List<SerialPortInfo> GetSerialPortInfos()
        {
            List<SerialPortInfo> portInfos = new List<SerialPortInfo>();
            
            try
            {
                // 获取所有串口名称
                string[] portNames = SerialPort.GetPortNames();
                
                // 使用WMI查询获取详细的串口设备信息
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE ClassGuid = '{4d36e978-e325-11ce-bfc1-08002be10318}'"))
                {
                    foreach (ManagementObject queryObj in searcher.Get().Cast<ManagementObject>())
                    {
                        if (queryObj["Caption"] != null)
                        {
                            string caption = queryObj["Caption"].ToString();
                            
                            // 提取串口名称和描述信息
                            string portName = ExtractPortName(caption);
                            
                            if (!string.IsNullOrEmpty(portName) && portNames.Contains(portName))
                            {
                                string description = caption.Replace($"({portName})", "").Trim();
                                string deviceId = queryObj["DeviceID"]?.ToString() ?? "";
                                string pnpDeviceId = queryObj["PNPDeviceID"]?.ToString() ?? "";
                                
                                portInfos.Add(new SerialPortInfo
                                {
                                    PortName = portName,
                                    Description = description,
                                    DeviceID = deviceId,
                                    PnpDeviceID = pnpDeviceId,
                                    IsAvailable = true
                                });
                            }
                        }
                    }
                }
                
                // 添加未找到详细信息的端口
                foreach (string portName in portNames)
                {
                    if (!portInfos.Any(p => p.PortName == portName))
                    {
                        portInfos.Add(new SerialPortInfo
                        {
                            PortName = portName,
                            Description = "未知设备",
                            IsAvailable = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // 发生异常时返回基本的端口列表
                foreach (string portName in SerialPort.GetPortNames())
                {
                    portInfos.Add(new SerialPortInfo
                    {
                        PortName = portName,
                        Description = "获取详细信息失败: " + ex.Message,
                        IsAvailable = true
                    });
                }
            }
            
            return portInfos;
        }

        /// <summary>
        /// 异步获取详细的串口设备信息列表
        /// </summary>
        /// <returns>串口设备信息列表</returns>
        public static Task<List<SerialPortInfo>> GetSerialPortInfosAsync()
        {
            return Task.Run(() => GetSerialPortInfos());
        }

        /// <summary>
        /// 从设备描述中提取串口名称
        /// </summary>
        /// <param name="caption">设备描述</param>
        /// <returns>串口名称</returns>
        private static string ExtractPortName(string caption)
        {
            // 从描述中提取串口名称，格式通常为 "XXX (COM1)"
            int startIndex = caption.LastIndexOf('(');
            int endIndex = caption.LastIndexOf(')');
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                string portName = caption.Substring(startIndex + 1, endIndex - startIndex - 1);
                return portName;
            }
            
            return string.Empty;
        }

        /// <summary>
        /// 检查串口是否存在
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <returns>是否存在</returns>
        public static bool IsPortExists(string portName)
        {
            if (string.IsNullOrEmpty(portName))
            {
                return false;
            }
            
            string[] availablePorts = SerialPort.GetPortNames();
            return Array.Exists(availablePorts, p => string.Equals(p, portName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 尝试打开串口
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <param name="serialPort">如果成功，返回已打开的串口对象</param>
        /// <param name="errorMessage">如果失败，返回错误信息</param>
        /// <returns>是否成功</returns>
        public static bool TryOpenPort(string portName, int baudRate, int dataBits, StopBits stopBits,
            Parity parity, out SerialPort serialPort, out string errorMessage)
        {
            serialPort = null;
            errorMessage = string.Empty;
            
            if (string.IsNullOrEmpty(portName))
            {
                errorMessage = "串口名称不能为空";
                return false;
            }
            
            if (!IsPortExists(portName))
            {
                errorMessage = $"串口 {portName} 不存在";
                return false;
            }
            
            try
            {
                serialPort = new SerialPort
                {
                    PortName = portName,
                    BaudRate = baudRate,
                    DataBits = dataBits,
                    StopBits = stopBits,
                    Parity = parity,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    DtrEnable = true,
                    RtsEnable = true
                };
                
                serialPort.Open();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage = $"串口 {portName} 访问被拒绝，可能被其他程序占用";
                return false;
            }
            catch (ArgumentException ex)
            {
                errorMessage = $"串口参数无效: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"打开串口失败: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 安全关闭串口
        /// </summary>
        /// <param name="serialPort">串口对象</param>
        /// <returns>是否成功关闭</returns>
        public static bool SafeClosePort(SerialPort serialPort)
        {
            if (serialPort == null)
            {
                return true;
            }
            
            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();
                    serialPort.Close();
                }
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// 创建并打开一个串口包装器
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <returns>串口包装器，如果失败则返回null</returns>
        public static SerialPortWrapper CreatePortWrapper(string portName, int baudRate = 9600, 
            int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None)
        {
            SerialPortWrapper wrapper = new SerialPortWrapper();
            if (wrapper.Open(portName, baudRate, dataBits, stopBits, parity))
            {
                return wrapper;
            }
            
            wrapper.Dispose();
            return null;
        }
        
        /// <summary>
        /// 获取推荐的波特率列表
        /// </summary>
        /// <returns>常用波特率列表</returns>
        public static int[] GetCommonBaudRates()
        {
            return new int[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
        }
        
        /// <summary>
        /// 获取推荐的数据位列表
        /// </summary>
        /// <returns>数据位列表</returns>
        public static int[] GetCommonDataBits()
        {
            return new int[] { 5, 6, 7, 8 };
        }
        
        /// <summary>
        /// 获取串口参数转换为可读字符串
        /// </summary>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <returns>参数描述字符串</returns>
        public static string GetPortParametersString(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            string stopBitsStr;
            switch (stopBits)
            {
                case StopBits.None:
                    stopBitsStr = "0";
                    break;
                case StopBits.OnePointFive:
                    stopBitsStr = "1.5";
                    break;
                case StopBits.Two:
                    stopBitsStr = "2";
                    break;
                default:
                    stopBitsStr = "1";
                    break;
            }
            
            string parityStr;
            switch (parity)
            {
                case Parity.Odd:
                    parityStr = "奇校验";
                    break;
                case Parity.Even:
                    parityStr = "偶校验";
                    break;
                case Parity.Mark:
                    parityStr = "标记校验";
                    break;
                case Parity.Space:
                    parityStr = "空校验";
                    break;
                default:
                    parityStr = "无校验";
                    break;
            }
            
            return $"{baudRate},{dataBits},{stopBitsStr},{parityStr}";
        }
    }

    /// <summary>
    /// 串口设备信息类
    /// </summary>
    public class SerialPortInfo
    {
        /// <summary>
        /// 串口名称（如COM1）
        /// </summary>
        public string PortName { get; set; }
        
        /// <summary>
        /// 设备描述（如USB串口设备）
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// 设备ID
        /// </summary>
        public string DeviceID { get; set; }
        
        /// <summary>
        /// 即插即用设备ID
        /// </summary>
        public string PnpDeviceID { get; set; }
        
        /// <summary>
        /// 端口是否可用
        /// </summary>
        public bool IsAvailable { get; set; }
        
        /// <summary>
        /// 获取串口完整描述
        /// </summary>
        public string FullDescription => $"{Description} ({PortName})";
        
        /// <summary>
        /// 获取USB设备VID和PID信息（如可用）
        /// </summary>
        public string GetVidPid()
        {
            if (string.IsNullOrEmpty(PnpDeviceID))
            {
                return string.Empty;
            }
            
            try
            {
                // 示例PnpDeviceID: USB\VID_0483&PID_5740\5&31AC2C5B&0&2
                if (PnpDeviceID.StartsWith("USB\\VID_", StringComparison.OrdinalIgnoreCase))
                {
                    int vidIndex = PnpDeviceID.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
                    int pidIndex = PnpDeviceID.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
                    
                    if (vidIndex >= 0 && pidIndex >= 0)
                    {
                        string vid = PnpDeviceID.Substring(vidIndex + 4, 4);
                        string pid = PnpDeviceID.Substring(pidIndex + 4, 4);
                        return $"VID:{vid}, PID:{pid}";
                    }
                }
            }
            catch
            {
                // 忽略异常
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        public override string ToString()
        {
            return FullDescription;
        }
    }
}
