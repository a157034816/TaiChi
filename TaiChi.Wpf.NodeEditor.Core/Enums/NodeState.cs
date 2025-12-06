namespace TaiChi.Wpf.NodeEditor.Core.Enums;

/// <summary>
/// 节点的当前状态枚举
/// </summary>
public enum NodeState
{
    /// <summary>
    /// 正常空闲状态
    /// </summary>
    Normal,
    
    /// <summary>
    /// 正在执行
    /// </summary>
    Executing,
    
    /// <summary>
    /// 执行成功（可选，用于可视化反馈）
    /// </summary>
    Success,
    
    /// <summary>
    /// 执行出错
    /// </summary>
    Error
}
