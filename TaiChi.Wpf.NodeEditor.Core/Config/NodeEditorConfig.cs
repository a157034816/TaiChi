namespace TaiChi.Wpf.NodeEditor.Core.Config;

/// <summary>
/// 节点编辑器的全局配置
/// </summary>
public static class NodeEditorConfig
{
    /// <summary>
    /// 当对一个已连接的输入引脚再次建立连接时：
    /// - false（默认）：保持严格模式，禁止新的连接
    /// - true：先断开输入引脚当前连接，再建立新的连接
    /// </summary>
    public static bool ReplaceInputConnectionOnNew { get; set; } = true;

    /// <summary>
    /// 是否启用性能监控功能
    /// - true（默认）：启用性能监控，适用于开发和测试环境
    /// - false：禁用性能监控，适用于生产环境以提高性能
    /// </summary>
    public static bool EnablePerformanceMonitoring { get; set; } = true;
}

