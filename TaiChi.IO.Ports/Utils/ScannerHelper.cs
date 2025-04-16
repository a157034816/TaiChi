using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TaiChi.IO.Ports.Utils
{
    /// <summary>
    /// 扫描枪辅助类，提供识别、配置和测试扫描枪的功能
    /// </summary>
    public static class ScannerHelper
    {
        /// <summary>
        /// 判断设备是否可能为扫描枪
        /// </summary>
        /// <param name="portInfo">串口设备信息</param>
        /// <returns>是否可能为扫描枪</returns>
        public static bool IsPossibleScannerDevice(SerialPortInfo portInfo)
        {
            if (portInfo == null)
            {
                return false;
            }
            
            // 从设备描述中判断
            string description = portInfo.Description?.ToLowerInvariant() ?? "";
            string pnpId = portInfo.PnpDeviceID?.ToLowerInvariant() ?? "";
            
            // 常见扫描枪关键字
            string[] scannerKeywords = new string[] 
            { 
                "scanner", "scan", "barcode", "bar code", "qr", "reader",
                "扫描", "条码", "条形码", "扫码", "扫描枪", "二维码"
            };
            
            // 判断描述中是否包含扫描枪关键字
            foreach (string keyword in scannerKeywords)
            {
                if (description.Contains(keyword))
                {
                    return true;
                }
            }
            
            // 常见扫描枪厂商的VID
            string[] commonScannerVids = new string[]
            {
                // 霍尼韦尔 Honeywell
                "0c2e", "0801", "05f9",
                // 讯宝/斑马 Symbol/Zebra/Motorola
                "05e0", "0536", "18ee",
                // 德利捷 Datalogic
                "05f9", "05f2",
                // 新大陆 Newland
                "1eab",
                // 民德 Mindeo
                "23d0", 
                // 原道 Yanzeo
                "0493"
            };
            
            // 检查VID是否匹配常见扫描枪厂商
            string vid = GetVidFromPnpId(pnpId);
            if (!string.IsNullOrEmpty(vid) && commonScannerVids.Contains(vid.ToLowerInvariant()))
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 检测串口设备是否是扫描枪（通过通信测试）(可能是无效方法)
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>检测结果及信息</returns>
        [Obsolete]
        public static async Task<(bool IsScanner, string Message)> DetectScannerDeviceAsync(
            string portName, 
            int baudRate = 9600, 
            int dataBits = 8, 
            StopBits stopBits = StopBits.One, 
            Parity parity = Parity.None,
            int timeoutMs = 2000)
        {
            // 首先检查端口是否存在
            if (!SerialPortHelper.IsPortExists(portName))
            {
                return (false, $"串口 {portName} 不存在");
            }
            
            SerialPortWrapper portWrapper = null;
            try
            {
                // 创建并打开串口
                portWrapper = new SerialPortWrapper();
                if (!portWrapper.Open(portName, baudRate, dataBits, stopBits, parity))
                {
                    return (false, $"无法打开串口 {portName}: {portWrapper.LastError}");
                }
                
                // 设置扫描枪数据接收标志
                bool dataReceived = false;
                string receivedData = string.Empty;
                
                // 注册数据接收事件
                portWrapper.DataReceived += (sender, e) => 
                {
                    dataReceived = true;
                    receivedData = e.GetString();
                };
                
                // 等待一段时间，看是否有数据返回
                // 大多数扫描枪在连接后不会主动发送数据，但某些扫描枪可能会发送初始化信息
                await Task.Delay(timeoutMs / 2);
                
                // 尝试向扫描枪发送触发命令
                // 不同品牌的扫描枪可能有不同的命令，这里使用一些通用命令
                byte[][] triggerCommands = new byte[][]
                {
                    // 通用触发命令
                    new byte[] { 0x16, 0x54, 0x0D }, // 某些霍尼韦尔/讯宝扫描枪
                    Encoding.ASCII.GetBytes("SCAN\r\n"), // 某些扫描枪
                    new byte[] { 0x1B, 0x31 }, // ESC+1 某些扫描枪
                };
                
                // 依次尝试不同的触发命令
                foreach (byte[] cmd in triggerCommands)
                {
                    if (dataReceived)
                    {
                        break; // 已经收到数据，不再继续尝试
                    }
                    
                    portWrapper.Send(cmd);
                    await Task.Delay(500); // 等待扫描枪响应
                }
                
                // 再等待一段时间
                await Task.Delay(timeoutMs / 2);
                
                // 如果收到数据，分析是否符合条码格式
                if (dataReceived)
                {
                    // 检查是否是条形码数据（通常是ASCII字符）
                    bool isBarcode = IsLikelyBarcodeData(receivedData);
                    if (isBarcode)
                    {
                        return (true, $"设备返回疑似条码数据: {receivedData}");
                    }
                    
                    // 返回了数据但不像条码
                    return (false, $"设备返回数据但不符合条码格式: {receivedData}");
                }
                
                // 根据设备信息进行静态判断
                var portInfo = SerialPortHelper.GetSerialPortInfos().FirstOrDefault(p => p.PortName == portName);
                if (portInfo != null && IsPossibleScannerDevice(portInfo))
                {
                    return (true, "根据设备信息判断可能是扫描枪，但未接收到响应数据");
                }
                
                return (false, "设备未返回数据，且设备信息不符合扫描枪特征");
            }
            catch (Exception ex)
            {
                return (false, $"检测过程出错: {ex.Message}");
            }
            finally
            {
                // 关闭并释放串口
                portWrapper?.Dispose();
            }
        }
        
        /// <summary>
        /// 从PnP设备ID中提取VID
        /// </summary>
        private static string GetVidFromPnpId(string pnpId)
        {
            if (string.IsNullOrEmpty(pnpId))
            {
                return string.Empty;
            }
            
            try
            {
                var match = Regex.Match(pnpId, @"vid_([a-fA-F0-9]{4})", RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            catch
            {
                // 忽略异常
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// 判断字符串是否可能是条码数据
        /// </summary>
        private static bool IsLikelyBarcodeData(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return false;
            }
            
            // 移除开头和结尾的不可打印字符
            string cleaned = data.Trim('\r', '\n', '\0', (char)3, (char)2);
            
            // 条码通常有最小长度
            if (cleaned.Length < 4)
            {
                return false;
            }
            
            // 条码通常是可打印字符
            if (!cleaned.All(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)))
            {
                // 如果包含较多不可打印字符，可能不是条码
                int nonPrintableCount = cleaned.Count(c => c < 32 || c > 126);
                if (nonPrintableCount > cleaned.Length / 4)
                {
                    return false;
                }
            }
            
            // 检查是否符合常见条码格式
            
            // EAN-13: 13位数字
            if (cleaned.Length == 13 && cleaned.All(char.IsDigit))
            {
                return true;
            }
            
            // UPC-A: 12位数字
            if (cleaned.Length == 12 && cleaned.All(char.IsDigit))
            {
                return true;
            }
            
            // EAN-8: 8位数字
            if (cleaned.Length == 8 && cleaned.All(char.IsDigit))
            {
                return true;
            }
            
            // CODE-39: 通常以*开头和结尾
            if ((cleaned.StartsWith("*") && cleaned.EndsWith("*")) || 
                Regex.IsMatch(cleaned, @"^[A-Z0-9\-\.$/\+%\s]+$"))
            {
                return true;
            }
            
            // QR Code/其他二维码：可能包含URL或较长的数据
            if (cleaned.StartsWith("http") || cleaned.Length > 20)
            {
                return true;
            }
            
            // 没有明确匹配条码格式，但也不排除可能性
            return false;
        }
        
        /// <summary>
        /// 获取所有可能的扫描枪设备
        /// </summary>
        /// <returns>可能为扫描枪的串口设备列表</returns>
        public static List<SerialPortInfo> GetPossibleScannerDevices()
        {
            var allPorts = SerialPortHelper.GetSerialPortInfos();
            return allPorts.Where(p => IsPossibleScannerDevice(p)).ToList();
        }

        /// <summary>
        /// 扫描枪配置命令
        /// </summary>
        public static class ScannerCommands
        {
            /// <summary>
            /// 常用扫描枪触发命令集合
            /// </summary>
            public static Dictionary<string, byte[]> TriggerCommands = new Dictionary<string, byte[]>
            {
                { "通用触发命令", new byte[] { 0x16, 0x54, 0x0D } },
                { "扫描命令", Encoding.ASCII.GetBytes("SCAN\r\n") },
                { "ESC+1", new byte[] { 0x1B, 0x31 } },
                { "触发按键", new byte[] { 0x1B, 0x01, 0x01, 0x01 } },
                { "立即扫描", new byte[] { 0x1B, 0x1D, 0x61, 0x01, 0x01 } }
            };

            /// <summary>
            /// 启用手动触发模式命令(霍尼韦尔)
            /// </summary>
            public static byte[] EnableManualTrigger_Honeywell = new byte[] { 0x16, 0x4D, 0x0D };

            /// <summary>
            /// 启用自动触发模式命令(霍尼韦尔)
            /// </summary>
            public static byte[] EnableAutoTrigger_Honeywell = new byte[] { 0x16, 0x41, 0x0D };

            /// <summary>
            /// 启用连续扫描模式命令(霍尼韦尔)
            /// </summary>
            public static byte[] EnableContinuousScan_Honeywell = new byte[] { 0x16, 0x53, 0x0D };

            /// <summary>
            /// 设置斑马Zebra/讯宝Symbol扫描枪为手动触发模式
            /// </summary>
            public static byte[] SetManualTrigger_Zebra = Encoding.ASCII.GetBytes("SYN105,21,0\r");

            /// <summary>
            /// 发送恢复出厂设置命令
            /// </summary>
            /// <param name="brand">扫描枪品牌</param>
            /// <returns>命令字节数组</returns>
            public static byte[] GetResetCommand(string brand)
            {
                switch (brand?.ToLowerInvariant())
                {
                    case "honeywell":
                    case "霍尼韦尔":
                        return new byte[] { 0x16, 0x01, 0x02, 0x03, 0x0D }; // 霍尼韦尔重置命令
                    
                    case "zebra":
                    case "symbol":
                    case "motorola":
                    case "斑马":
                    case "讯宝":
                        return Encoding.ASCII.GetBytes("SYN999\r"); // 讯宝/斑马设备重置命令
                    
                    case "datalogic":
                    case "德利捷":
                        return new byte[] { 0x1B, 0x01, 0xFF, 0x01, 0x02 }; // 德利捷重置命令
                    
                    default:
                        // 通用重置命令 - 不一定适用于所有设备
                        return new byte[] { 0x1D, 0x2A, 0x1D, 0x45 };
                }
            }
        }

        /// <summary>
        /// 测试扫描枪并监听数据(可能是无效方法)
        /// </summary>
        /// <param name="portName">串口名</param>
        /// <param name="timeout">超时时间(毫秒)</param>
        /// <param name="triggerScanner">是否发送触发指令</param>
        /// <returns>测试结果</returns>
        [Obsolete]
        public static async Task<(bool Success, string Result)> TestScannerAsync(
            string portName, int timeout = 10000, bool triggerScanner = true)
        {
            SerialPortWrapper wrapper = null;
            
            try
            {
                // 创建串口包装器
                wrapper = new SerialPortWrapper();
                if (!wrapper.Open(portName))
                {
                    return (false, $"无法打开串口 {portName}: {wrapper.LastError}");
                }
                
                // 使用TaskCompletionSource等待扫描结果
                var taskCompletionSource = new TaskCompletionSource<string>();
                
                // 设置超时取消令牌
                using (var cancellationTokenSource = new CancellationTokenSource(timeout))
                {
                    // 当收到取消信号时完成任务
                    cancellationTokenSource.Token.Register(() => 
                    {
                        taskCompletionSource.TrySetResult("超时，未收到扫描数据");
                    }, false);
                    
                    // 注册数据接收事件
                    wrapper.DataReceived += (sender, e) => 
                    {
                        string data = e.GetString();
                        string hexData = e.GetHexString();
                        
                        // 设置结果并完成任务
                        taskCompletionSource.TrySetResult(
                            $"收到数据: {data}\r\n十六进制: {hexData}");
                    };
                    
                    // 如果需要触发扫描枪
                    if (triggerScanner)
                    {
                        // 测试每一种触发命令
                        foreach (var cmd in ScannerCommands.TriggerCommands)
                        {
                            wrapper.Send(cmd.Value);
                            // 等待一小段时间看是否有响应
                            await Task.Delay(500, cancellationTokenSource.Token);
                            
                            // 如果任务已完成，说明收到了数据
                            if (taskCompletionSource.Task.IsCompleted)
                            {
                                break;
                            }
                        }
                    }
                    
                    // 等待结果
                    string result = await taskCompletionSource.Task;
                    
                    // 如果超时
                    if (result.Contains("超时"))
                    {
                        return (false, "请手动按下扫描枪按钮进行测试，或重新连接扫描枪后再试");
                    }
                    
                    return (true, result);
                }
            }
            catch (Exception ex)
            {
                return (false, $"测试过程发生错误: {ex.Message}");
            }
            finally
            {
                // 释放资源
                wrapper?.Dispose();
            }
        }

        /// <summary>
        /// 配置扫描枪为常用模式
        /// </summary>
        /// <param name="portName">串口名</param>
        /// <param name="brand">扫描枪品牌（可选）</param>
        /// <param name="mode">工作模式: Manual=手动触发, Auto=自动触发, Continuous=连续扫描</param>
        /// <returns>配置结果</returns>
        public static async Task<(bool Success, string Message)> ConfigureScannerAsync(
            string portName, string brand = "", string mode = "Manual")
        {
            SerialPortWrapper wrapper = null;
            
            try
            {
                wrapper = new SerialPortWrapper();
                if (!wrapper.Open(portName))
                {
                    return (false, $"无法打开串口 {portName}: {wrapper.LastError}");
                }
                
                byte[] configCommand = null;
                
                // 根据品牌和模式选择配置命令
                switch (mode.ToLowerInvariant())
                {
                    case "manual":
                        if (brand.ToLowerInvariant().Contains("honeywell") || 
                            brand.ToLowerInvariant().Contains("霍尼韦尔"))
                        {
                            configCommand = ScannerCommands.EnableManualTrigger_Honeywell;
                        }
                        else if (brand.ToLowerInvariant().Contains("zebra") || 
                                 brand.ToLowerInvariant().Contains("symbol") ||
                                 brand.ToLowerInvariant().Contains("斑马") || 
                                 brand.ToLowerInvariant().Contains("讯宝"))
                        {
                            configCommand = ScannerCommands.SetManualTrigger_Zebra;
                        }
                        break;
                        
                    case "auto":
                        if (brand.ToLowerInvariant().Contains("honeywell") || 
                            brand.ToLowerInvariant().Contains("霍尼韦尔"))
                        {
                            configCommand = ScannerCommands.EnableAutoTrigger_Honeywell;
                        }
                        break;
                        
                    case "continuous":
                        if (brand.ToLowerInvariant().Contains("honeywell") || 
                            brand.ToLowerInvariant().Contains("霍尼韦尔"))
                        {
                            configCommand = ScannerCommands.EnableContinuousScan_Honeywell;
                        }
                        break;
                }
                
                if (configCommand != null)
                {
                    // 发送配置命令
                    if (!wrapper.Send(configCommand))
                    {
                        return (false, "发送配置命令失败");
                    }
                    
                    // 等待一段时间让设备处理命令
                    await Task.Delay(500);
                    
                    return (true, $"已发送{mode}模式配置命令");
                }
                else
                {
                    // 尝试发送通用配置命令或提示手动配置
                    return (false, "未找到适用于该品牌扫描枪的配置命令，请参考扫描枪说明书进行手动配置");
                }
            }
            catch (Exception ex)
            {
                return (false, $"配置过程发生错误: {ex.Message}");
            }
            finally
            {
                wrapper?.Dispose();
            }
        }
    }
} 