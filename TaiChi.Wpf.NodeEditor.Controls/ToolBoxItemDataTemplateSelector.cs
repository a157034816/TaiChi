using System.Windows;
using System.Windows.Controls;
using TaiChi.Wpf.NodeEditor.Core.Interfaces;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace TaiChi.Wpf.NodeEditor.Controls;

/// <summary>
/// TreeView的数据模板选择器，根据不同情况选择合适的DataTemplate
/// </summary>
public class ToolBoxItemDataTemplateSelector : DataTemplateSelector
{
    #region 模板属性

    /// <summary>
    /// 分类节点的默认模板
    /// </summary>
    public DataTemplate? CategoryTemplate { get; set; }

    /// <summary>
    /// 节点项的默认模板
    /// </summary>
    public DataTemplate? NodeTemplate { get; set; }

    #endregion

    #region DataTemplateSelector 重写

    /// <summary>
    /// 根据数据项的类型和状态选择合适的DataTemplate
    /// </summary>
    /// <param name="item">数据项</param>
    /// <param name="container">容器元素</param>
    /// <returns>选择的DataTemplate</returns>
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item == null)
            return null;

        // 检查是否实现了Core接口
        if (item is ICategoryTreeItemViewModel category)
        {
            if (category.HasChildren)
            {
                return SelectCategoryTemplate(category);
            }
            else
            {
                return NodeTemplate;
            }
        }
        else if (item is INodeTreeItemViewModel node)
        {
            return SelectNodeTemplate(node);
        }

        return null;
    }

    #endregion

    #region 模板选择逻辑

    /// <summary>
    /// 为分类节点选择合适的模板
    /// </summary>
    /// <param name="category">分类节点</param>
    /// <returns>选择的DataTemplate</returns>
    private DataTemplate? SelectCategoryTemplate(ICategoryTreeItemViewModel category)
    {
        // 默认分类模板
        return CategoryTemplate;
    }

    /// <summary>
    /// 为节点项选择合适的模板
    /// </summary>
    /// <param name="node">节点项</param>
    /// <returns>选择的DataTemplate</returns>
    private DataTemplate? SelectNodeTemplate(INodeTreeItemViewModel node)
    {
        if (node.NodeMetadata == null)
            return NodeTemplate;

        // 默认节点模板
        return NodeTemplate;
    }

    #endregion
}