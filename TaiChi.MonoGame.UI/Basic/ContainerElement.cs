using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TaiChi.MonoGame.UI.Basic;

/// <summary>
/// 容器元素基类，实现了IContainerElement接口，提供基本的子元素管理功能
/// </summary>
public class ContainerElement : UIElement, IContainerElement
{
    private readonly List<IUIElement> _children = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public ContainerElement()
    {
    }

    /// <summary>
    /// 获取子元素集合
    /// </summary>
    public IReadOnlyList<IUIElement> Children => _children.AsReadOnly();

    /// <summary>
    /// 获取子元素数量
    /// </summary>
    public int ChildCount => _children.Count;

    /// <summary>
    /// 添加子元素
    /// </summary>
    /// <param name="element">要添加的子元素</param>
    public virtual void AddChild(IUIElement element)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        if (_children.Contains(element))
            return;

        _children.Add(element);
        element.Parent = this;
        element.SetGame(Game);
        element.SetSpriteBatch(SpriteBatch);
    }

    /// <summary>
    /// 移除子元素
    /// </summary>
    /// <param name="element">要移除的子元素</param>
    /// <returns>如果成功移除返回true，否则返回false</returns>
    public virtual bool RemoveChild(IUIElement element)
    {
        if (element == null)
            return false;

        if (_children.Remove(element))
        {
            element.Parent = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 清空所有子元素
    /// </summary>
    public virtual void ClearChildren()
    {
        foreach (var child in _children) child.Parent = null;

        _children.Clear();
    }

    /// <summary>
    /// 检查是否包含指定子元素
    /// </summary>
    /// <param name="element">要检查的子元素</param>
    /// <returns>如果包含返回true，否则返回false</returns>
    public virtual bool ContainsChild(IUIElement element)
    {
        return _children.Contains(element);
    }

    /// <summary>
    /// 更新容器及其所有子元素
    /// </summary>
    /// <param name="gameTime">游戏时间</param>
    public override void Update(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        base.Update(gameTime);

        // 更新所有子元素
        foreach (var child in _children.ToArray()) // 使用ToArray避免在迭代过程中集合被修改
            child.Update(gameTime);
    }

    /// <summary>
    /// 绘制容器及其所有子元素
    /// </summary>
    /// <param name="spriteBatch">精灵批处理</param>
    public override void Draw(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        base.Draw(gameTime);

        // 绘制所有子元素
        foreach (var child in _children) child.Draw(gameTime);
    }

    /// <summary>
    /// 测量容器和子元素所需尺寸
    /// </summary>
    /// <param name="availableSize">可用尺寸</param>
    /// <returns>计算出的尺寸</returns>
    public override Vector2 Measure(Vector2 availableSize)
    {
        // 默认实现：取所有子元素所需空间的最大范围
        // 具体实现可能需要根据布局逻辑重写

        var size = base.Measure(availableSize);

        foreach (var child in _children)
        {
            var childSize = child.Measure(availableSize);
            size.X = Math.Max(size.X, childSize.X);
            size.Y = Math.Max(size.Y, childSize.Y);
        }

        return size;
    }

    /// <summary>
    /// 安排容器和子元素布局
    /// </summary>
    /// <param name="finalSize">最终尺寸</param>
    public override void Arrange(Vector2 finalSize)
    {
        base.Arrange(finalSize);

        // 默认实现：不改变子元素位置
        // 具体容器类型应当重写这个方法，根据自己的布局逻辑安排子元素位置
    }

    public override void SetGame(Game game)
    {
        base.SetGame(game);
        foreach (var child in _children) child.SetGame(game);
    }

    public override void SetSpriteBatch(SpriteBatch spriteBatch)
    {
        base.SetSpriteBatch(spriteBatch);
        foreach (var child in _children) child.SetSpriteBatch(spriteBatch);
    }
}