using System.Collections.Generic;

namespace TaiChi.MonoGame.UI.Basic;

/// <summary>
/// 容器元素接口，定义了拥有多个子元素的控件所需的基本功能
/// </summary>
public interface IContainerElement : IUIElement
{
    /// <summary>
    /// 获取子元素集合
    /// </summary>
    IReadOnlyList<IUIElement> Children { get; }

    /// <summary>
    /// 获取子元素数量
    /// </summary>
    int ChildCount { get; }

    /// <summary>
    /// 添加子元素
    /// </summary>
    /// <param name="element">要添加的子元素</param>
    void AddChild(IUIElement element);

    /// <summary>
    /// 移除子元素
    /// </summary>
    /// <param name="element">要移除的子元素</param>
    /// <returns>如果成功移除返回true，否则返回false</returns>
    bool RemoveChild(IUIElement element);

    /// <summary>
    /// 清空所有子元素
    /// </summary>
    void ClearChildren();

    /// <summary>
    /// 检查是否包含指定子元素
    /// </summary>
    /// <param name="element">要检查的子元素</param>
    /// <returns>如果包含返回true，否则返回false</returns>
    bool ContainsChild(IUIElement element);
}