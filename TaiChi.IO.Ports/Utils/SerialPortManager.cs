using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;

namespace TaiChi.IO.Ports.Utils
{
    /// <summary>
    /// 串口管理器，用于全局管理串口实例
    /// </summary>
    public static class SerialPortManager
    {
        private static readonly Dictionary<string, SerialPortWrapper> PortInstances = new Dictionary<string, SerialPortWrapper>(StringComparer.OrdinalIgnoreCase);
        private static readonly object SyncLock = new object();
        
        /// <summary>
        /// 获取当前所有打开的串口实例
        /// </summary>
        public static IReadOnlyCollection<SerialPortWrapper> ActivePorts
        {
            get
            {
                lock (SyncLock)
                {
                    return PortInstances.Values.Where(p => p.IsOpen).ToList().AsReadOnly();
                }
            }
        }
        
        /// <summary>
        /// 根据端口名称获取串口实例
        /// </summary>
        /// <param name="portName">端口名称</param>
        /// <returns>串口实例，若不存在则返回null</returns>
        public static SerialPortWrapper? GetPort(string portName)
        {
            if (string.IsNullOrEmpty(portName))
                return null;
                
            lock (SyncLock)
            {
                return PortInstances.TryGetValue(portName, out var port) ? port : null;
            }
        }
        
        /// <summary>
        /// 注册串口实例
        /// </summary>
        /// <param name="wrapper">串口实例</param>
        internal static void RegisterPort(SerialPortWrapper wrapper)
        {
            if (wrapper == null || string.IsNullOrEmpty(wrapper.PortName))
                return;
                
            lock (SyncLock)
            {
                string portName = wrapper.PortName;
                
                // 如果已存在相同名称的端口实例，先关闭并移除它
                if (PortInstances.TryGetValue(portName, out var existingPort))
                {
                    existingPort.Close();
                    PortInstances.Remove(portName);
                }
                
                PortInstances[portName] = wrapper;
            }
        }
        
        /// <summary>
        /// 注销串口实例
        /// </summary>
        /// <param name="portName">端口名称</param>
        internal static void UnregisterPort(string portName)
        {
            if (string.IsNullOrEmpty(portName))
                return;
                
            lock (SyncLock)
            {
                if (PortInstances.TryGetValue(portName, out _))
                {
                    PortInstances.Remove(portName);
                }
            }
        }
        
        /// <summary>
        /// 关闭所有串口
        /// </summary>
        public static void CloseAllPorts()
        {
            lock (SyncLock)
            {
                foreach (var port in PortInstances.Values)
                {
                    try
                    {
                        port.Close();
                    }
                    catch
                    {
                        // 忽略关闭过程中的异常
                    }
                }
                PortInstances.Clear();
            }
        }
        
        /// <summary>
        /// 获取当前已注册的串口名称列表
        /// </summary>
        /// <returns>已注册的串口名称数组</returns>
        public static string[] GetRegisteredPortNames()
        {
            lock (SyncLock)
            {
                return PortInstances.Keys.ToArray();
            }
        }
        
        /// <summary>
        /// 检查新插入的串口设备
        /// </summary>
        /// <returns>新插入的串口列表</returns>
        public static string[] CheckNewPorts()
        {
            var currentPorts = SerialPort.GetPortNames();
            var registeredPorts = GetRegisteredPortNames();
            
            return currentPorts.Except(registeredPorts, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        
        /// <summary>
        /// 检查已移除的串口设备
        /// </summary>
        /// <returns>已移除的串口列表</returns>
        public static string[] CheckRemovedPorts()
        {
            var currentPorts = SerialPort.GetPortNames();
            var registeredPorts = GetRegisteredPortNames();
            
            return registeredPorts.Except(currentPorts, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}