using System;
using Microsoft.Xna.Framework;
using TaiChi.MonoGame.UI.Basic;

namespace TaiChi.MonoGame.UI.Layout;

/// <summary>
/// 面板控件，提供一个可以包含多个子元素的容器
/// </summary>
public class Panel : ContainerElement
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public Panel()
    {
        BackgroundColor = new Color(240, 240, 240);
    }

    /// <summary>
    /// 内边距
    /// </summary>
    public Thickness Padding { get; set; } = new(5);

    /// <summary>
    /// 边框颜色
    /// </summary>
    public Color BorderColor { get; set; } = Color.Gray;

    /// <summary>
    /// 边框宽度
    /// </summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>
    /// 圆角半径
    /// </summary>
    public float CornerRadius { get; set; } = 0f;

    /// <summary>
    /// 绘制面板
    /// </summary>
    /// <param name="gameTime"></param>
    public override void Draw(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        // 绘制面板背景
        base.Draw(gameTime);

        // 绘制边框
        if (BorderWidth > 0)
        {
            // 简单实现，绘制一个矩形边框
            // 实际应用中可能需要考虑圆角等情况
            var rect = new Rectangle(
                (int)Position.X,
                (int)Position.Y,
                (int)Size.X,
                (int)Size.Y
            );

            DrawBorder(rect, BorderColor, BorderWidth);
        }

        // 绘制子元素在基类中已经实现
    }

    /// <summary>
    /// 绘制边框
    /// </summary>
    protected void DrawBorder(Rectangle rectangle, Color color, float thickness)
    {
        // 绘制四条边
        // 上边
        DrawRectangle(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, (int)thickness), color);
        // 下边
        DrawRectangle(new Rectangle(rectangle.X, rectangle.Y + rectangle.Height - (int)thickness, rectangle.Width, (int)thickness), color);
        // 左边
        DrawRectangle(new Rectangle(rectangle.X, rectangle.Y, (int)thickness, rectangle.Height), color);
        // 右边
        DrawRectangle(new Rectangle(rectangle.X + rectangle.Width - (int)thickness, rectangle.Y, (int)thickness, rectangle.Height), color);
    }

    /// <summary>
    /// 安排子元素布局
    /// </summary>
    /// <param name="finalSize">最终尺寸</param>
    public override void Arrange(Vector2 finalSize)
    {
        base.Arrange(finalSize);

        // 计算子元素的可用区域（考虑内边距）
        var availableRect = new Rectangle(
            (int)(Position.X + Padding.Left),
            (int)(Position.Y + Padding.Top),
            (int)(Size.X - Padding.Left - Padding.Right),
            (int)(Size.Y - Padding.Top - Padding.Bottom)
        );

        // 在这个简单实现中，我们不改变子元素的位置
        // 子元素的位置应该是相对于面板的
        // 更复杂的布局逻辑可以在子类中实现
    }

    /// <summary>
    /// 测量面板所需的尺寸
    /// </summary>
    /// <param name="availableSize">可用尺寸</param>
    /// <returns>计算出的尺寸</returns>
    public override Vector2 Measure(Vector2 availableSize)
    {
        // 先减去内边距得到子元素可用空间
        var childAvailableSize = new Vector2(
            Math.Max(0, availableSize.X - Padding.Left - Padding.Right),
            Math.Max(0, availableSize.Y - Padding.Top - Padding.Bottom)
        );

        // 测量所有子元素
        var maxChildSize = Vector2.Zero;
        foreach (var child in Children)
        {
            var childSize = child.Measure(childAvailableSize);
            maxChildSize.X = Math.Max(maxChildSize.X, childSize.X);
            maxChildSize.Y = Math.Max(maxChildSize.Y, childSize.Y);
        }

        // 加上内边距得到最终尺寸
        return new Vector2(
            maxChildSize.X + Padding.Left + Padding.Right,
            maxChildSize.Y + Padding.Top + Padding.Bottom
        );
    }
}

/// <summary>
/// 表示四边厚度的结构
/// </summary>
public struct Thickness
{
    /// <summary>
    /// 左边距
    /// </summary>
    public float Left { get; set; }

    /// <summary>
    /// 顶边距
    /// </summary>
    public float Top { get; set; }

    /// <summary>
    /// 右边距
    /// </summary>
    public float Right { get; set; }

    /// <summary>
    /// 底边距
    /// </summary>
    public float Bottom { get; set; }

    /// <summary>
    /// 构造函数 - 四个不同的值
    /// </summary>
    public Thickness(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>
    /// 构造函数 - 四边使用相同的值
    /// </summary>
    public Thickness(float uniformSize)
    {
        Left = Top = Right = Bottom = uniformSize;
    }

    /// <summary>
    /// 构造函数 - 水平边和垂直边使用不同的值
    /// </summary>
    public Thickness(float horizontal, float vertical)
    {
        Left = Right = horizontal;
        Top = Bottom = vertical;
    }
}