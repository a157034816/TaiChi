using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TaiChi.MonoGame.UI.Controls;

/// <summary>
/// 按钮控件，提供基本的点击和悬停功能
/// </summary>
public class Button : BaseButton
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public Button()
    {
    }

    /// <summary>
    /// 按钮文本
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// 文本颜色
    /// </summary>
    public Color TextColor { get; set; } = Color.White;

    /// <summary>
    /// 禁用状态下的文本颜色
    /// </summary>
    public Color DisabledTextColor { get; set; } = new(150, 150, 150);

    /// <summary>
    /// 文本字体
    /// </summary>
    public SpriteFont Font { get; set; }

    /// <summary>
    /// 更新按钮状态
    /// </summary>
    /// <param name="gameTime">游戏时间</param>
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
    }

    /// <summary>
    /// 绘制按钮
    /// </summary>
    /// <param name="spriteBatch">精灵批处理</param>
    public override void Draw(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        // 绘制按钮背景
        var backgroundColor = GetCurrentBackgroundColor();
        if (backgroundColor != Color.Transparent) DrawRectangle(new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y), backgroundColor);

        // 绘制按钮边框
        DrawBorder();

        // 绘制按钮文本
        if (!string.IsNullOrEmpty(Text) && Font != null)
        {
            var textSize = Font.MeasureString(Text);
            var textPosition = new Vector2(
                Position.X + (Size.X - textSize.X) / 2,
                Position.Y + (Size.Y - textSize.Y) / 2
            );

            SpriteBatch.DrawString(Font, Text, textPosition, IsDisabled ? DisabledTextColor : TextColor);
        }
    }

    /// <summary>
    /// 测量按钮所需尺寸
    /// </summary>
    /// <param name="availableSize">可用尺寸</param>
    /// <returns>计算出的尺寸</returns>
    public override Vector2 Measure(Vector2 availableSize)
    {
        if (string.IsNullOrEmpty(Text) || Font == null) return Size;

        var textSize = Font.MeasureString(Text);
        return new Vector2(
            Math.Max(Size.X, textSize.X + BorderWidth * 2),
            Math.Max(Size.Y, textSize.Y + BorderWidth * 2)
        );
    }
}