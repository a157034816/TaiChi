using System;

namespace TaiChi.Wpf.NodeEditor.Controls.Models;

/// <summary>
/// 描述带有 EditorForNode 特性的可编辑属性
/// </summary>
public class NodeEditablePropertyInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Type PropertyType { get; set; } = typeof(object);
}

