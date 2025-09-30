using System;

namespace TaiChi.Wpf.NodeEditor.Core.Attributes;

/// <summary>
/// 用于标记节点类中的“可编辑常量”属性。
/// UI 可据此渲染对应的编辑控件并将值写回属性。
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class EditorForNodeAttribute : Attribute
{
    /// <summary>
    /// 可选的显示名称（用于 UI 标签）。
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// 可选的描述（用于提示）。
    /// </summary>
    public string? Description { get; }

    public EditorForNodeAttribute() { }

    public EditorForNodeAttribute(string displayName)
    {
        DisplayName = displayName;
    }

    public EditorForNodeAttribute(string displayName, string description)
    {
        DisplayName = displayName;
        Description = description;
    }
}

