using System;
using System.Collections.Generic;

namespace TaiChi.Core.Performance
{
    /// <summary>
    /// 性能监控接口
    /// </summary>
    public interface IPerformanceMonitor
    {
        /// <summary>
        /// 报告性能数据
        /// </summary>
        /// <param name="context">性能上下文</param>
        void GetPerformanceData(PerformanceContext context);
    }

    /// <summary>
    /// 性能上下文 - 用于收集各层性能数据的大型类
    /// </summary>
    public class PerformanceContext
    {
        /// <summary>
        /// 性能数据字典
        /// </summary>
        public Dictionary<string, object> Data { get; } = new Dictionary<string, object>();

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>
        /// 添加性能数据
        /// </summary>
        /// <param name="category">分类</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void AddData(string category, string key, object value)
        {
            var fullKey = $"{category}.{key}";
            Data[fullKey] = value;
        }

        /// <summary>
        /// 获取性能数据
        /// </summary>
        /// <param name="category">分类</param>
        /// <param name="key">键</param>
        /// <returns>值</returns>
        public T GetData<T>(string category, string key, T defaultValue = default)
        {
            var fullKey = $"{category}.{key}";
            if (Data.TryGetValue(fullKey, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 添加内存数据
        /// </summary>
        /// <param name="component">组件名</param>
        /// <param name="memoryUsage">内存使用量（字节）</param>
        /// <param name="objectCount">对象数量</param>
        public void AddMemoryData(string component, long memoryUsage, int objectCount = 0)
        {
            AddData("Memory", $"{component}.Usage", memoryUsage);
            AddData("Memory", $"{component}.UsageMB", memoryUsage / (1024.0 * 1024.0));
            if (objectCount > 0)
            {
                AddData("Memory", $"{component}.ObjectCount", objectCount);
            }
        }

        /// <summary>
        /// 添加性能计时数据
        /// </summary>
        /// <param name="component">组件名</param>
        /// <param name="operation">操作名</param>
        /// <param name="elapsedMs">耗时（毫秒）</param>
        public void AddTimingData(string component, string operation, double elapsedMs)
        {
            AddData("Timing", $"{component}.{operation}", elapsedMs);
        }

        /// <summary>
        /// 添加计数数据
        /// </summary>
        /// <param name="component">组件名</param>
        /// <param name="counter">计数器名</param>
        /// <param name="count">计数</param>
        public void AddCountData(string component, string counter, int count)
        {
            AddData("Count", $"{component}.{counter}", count);
        }

        /// <summary>
        /// 获取所有指定分类的数据
        /// </summary>
        /// <param name="category">分类</param>
        /// <returns>数据字典</returns>
        public Dictionary<string, object> GetCategoryData(string category)
        {
            var result = new Dictionary<string, object>();
            var prefix = $"{category}.";
        
            foreach (var kvp in Data)
            {
                if (kvp.Key.StartsWith(prefix))
                {
                    var key = kvp.Key.Substring(prefix.Length);
                    result[key] = kvp.Value;
                }
            }
        
            return result;
        }

        /// <summary>
        /// 生成性能报告
        /// </summary>
        /// <returns>性能报告字符串</returns>
        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"=== 性能监控报告 ({Timestamp:HH:mm:ss}) ===");

            // 内存数据
            var memoryData = GetCategoryData("Memory");
            if (memoryData.Count > 0)
            {
                report.AppendLine("【内存使用】");
                foreach (var kvp in memoryData)
                {
                    if (kvp.Key.EndsWith(".UsageMB"))
                    {
                        var component = kvp.Key.Replace(".UsageMB", "");
                        var usageMB = (double)kvp.Value;
                        var objectCount = GetData<int>("Memory", $"{component}.ObjectCount");
                    
                        if (objectCount > 0)
                            report.AppendLine($"  {component}: {usageMB:F2} MB ({objectCount} 个对象)");
                        else
                            report.AppendLine($"  {component}: {usageMB:F2} MB");
                    }
                }
            }

            // 计时数据
            var timingData = GetCategoryData("Timing");
            if (timingData.Count > 0)
            {
                report.AppendLine("【性能计时】");
                foreach (var kvp in timingData)
                {
                    report.AppendLine($"  {kvp.Key}: {kvp.Value:F2} ms");
                }
            }

            // 计数数据
            var countData = GetCategoryData("Count");
            if (countData.Count > 0)
            {
                report.AppendLine("【计数统计】");
                foreach (var kvp in countData)
                {
                    report.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// 性能监控管理器
    /// </summary>
    public class PerformanceManager
    {
        private readonly List<IPerformanceMonitor> _monitors = new List<IPerformanceMonitor>();
        private DateTime _lastReportTime = DateTime.MinValue;
        private TimeSpan _reportInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 注册性能监控器
        /// </summary>
        /// <param name="monitor">监控器</param>
        public void RegisterMonitor(IPerformanceMonitor monitor)
        {
            if (!_monitors.Contains(monitor))
            {
                _monitors.Add(monitor);
            }
        }

        /// <summary>
        /// 移除性能监控器
        /// </summary>
        /// <param name="monitor">监控器</param>
        public void UnregisterMonitor(IPerformanceMonitor monitor)
        {
            _monitors.Remove(monitor);
        }

        /// <summary>
        /// 更新性能监控
        /// </summary>
        public void Update()
        {
            if (DateTime.Now - _lastReportTime >= _reportInterval)
            {
                CollectAndReport();
                _lastReportTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 收集并报告性能数据
        /// </summary>
        public void CollectAndReport()
        {
            var context = new PerformanceContext();

            // 收集系统内存信息
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        
            var totalMemory = GC.GetTotalMemory(false);
            context.AddMemoryData("System", totalMemory);

            // 让所有监控器报告数据
            foreach (var monitor in _monitors)
            {
                try
                {
                    monitor.GetPerformanceData(context);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"性能监控器报告失败: {ex.Message}");
                }
            }

            // 输出报告
            var report = context.GenerateReport();
            System.Diagnostics.Debug.WriteLine(report);
        }

        /// <summary>
        /// 设置报告间隔
        /// </summary>
        /// <param name="interval">间隔时间</param>
        public void SetReportInterval(TimeSpan interval)
        {
            _reportInterval = interval;
        }
    }
}
