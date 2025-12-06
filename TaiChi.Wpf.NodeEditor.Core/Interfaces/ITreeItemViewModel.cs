using System.Collections.ObjectModel;

namespace TaiChi.Wpf.NodeEditor.Core.Interfaces;

/// <summary>
/// 树形视图项的基础接口定义
/// </summary>
public interface ITreeItemViewModel
{
    /// <summary>
    /// 显示名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 完整路径
    /// </summary>
    string FullPath { get; }

    /// <summary>
    /// 层级深度
    /// </summary>
    int Level { get; }

    /// <summary>
    /// 是否展开
    /// </summary>
    bool IsExpanded { get; set; }

    /// <summary>
    /// 是否选中
    /// </summary>
    bool IsSelected { get; set; }

    /// <summary>
    /// 是否可见（用于搜索过滤）
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// 父节点
    /// </summary>
    ITreeItemViewModel? Parent { get; }

    /// <summary>
    /// 子节点集合
    /// </summary>
    ObservableCollection<ITreeItemViewModel> Children { get; }

    /// <summary>
    /// 该项是否包含子节点
    /// </summary>
    bool HasChildren { get; }

    /// <summary>
    /// 是否为叶子节点（节点项）
    /// </summary>
    bool IsLeaf { get; }

    /// <summary>
    /// 应用搜索过滤
    /// </summary>
    /// <param name="searchText">搜索文本</param>
    /// <returns>是否匹配搜索条件</returns>
    bool ApplySearchFilter(string searchText);

    /// <summary>
    /// 设置可见性
    /// </summary>
    /// <param name="isVisible">是否可见</param>
    void SetVisibility(bool isVisible);

    /// <summary>
    /// 工具提示文本
    /// </summary>
    string ToolTipText { get; }
}