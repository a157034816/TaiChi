using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TaiChi.MonoGame.UI.Basic;

namespace TaiChi.MonoGame.UI.Controls;

/// <summary>
/// 按钮基类，提供基本的按钮功能
/// </summary>
public abstract class BaseButton : UIElement
{
    /// <summary>
    /// 鼠标是否悬停在按钮上
    /// </summary>
    protected bool _isHovered;

    /// <summary>
    /// 按钮是否被按下
    /// </summary>
    protected bool _isPressed;

    /// <summary>
    /// 获取1x1像素纹理
    /// </summary>
    private Texture2D _pixelTexture;

    /// <summary>
    /// 构造函数
    /// </summary>
    protected BaseButton()
    {
    }

    /// <summary>
    /// 按钮圆角半径
    /// </summary>
    public float CornerRadius { get; set; } = 0f;

    /// <summary>
    /// 按钮边框颜色
    /// </summary>
    public Color BorderColor { get; set; } = Color.White;

    /// <summary>
    /// 按钮边框宽度
    /// </summary>
    public float BorderWidth { get; set; } = 2f;

    /// <summary>
    /// 按钮悬停时的背景颜色
    /// </summary>
    public Color HoverBackgroundColor { get; set; } = new(200, 200, 200);

    /// <summary>
    /// 按钮按下时的背景颜色
    /// </summary>
    public Color PressedBackgroundColor { get; set; } = new(150, 150, 150);

    /// <summary>
    /// 按钮禁用时的背景颜色
    /// </summary>
    public Color DisabledBackgroundColor { get; set; } = new(100, 100, 100);

    /// <summary>
    /// 按钮是否禁用
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// 点击事件
    /// </summary>
    public event EventHandler OnClick;

    /// <summary>
    /// 更新按钮状态
    /// </summary>
    /// <param name="gameTime">游戏时间</param>
    public override void Update(GameTime gameTime)
    {
        if (!IsVisible || IsDisabled)
            return;

        var mouseState = Mouse.GetState();
        var mousePosition = ScreenToVirtualCoordinates(mouseState.Position);

        // 检查鼠标是否在按钮范围内
        var buttonRect = new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);
        _isHovered = buttonRect.Contains(mousePosition);

        // 处理鼠标按下和释放
        if (_isHovered)
        {
            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                _isPressed = true;
            }
            else if (mouseState.LeftButton == ButtonState.Released && _isPressed)
            {
                _isPressed = false;
                OnClick?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            _isPressed = false;
        }
    }

    /// <summary>
    /// 绘制按钮边框
    /// </summary>
    /// <param name="spriteBatch">精灵批处理</param>
    protected void DrawBorder()
    {
        if (BorderWidth > 0)
        {
            if (CornerRadius > 0)
            {
                // 绘制圆角边框
                DrawRoundedRectangle(Position, Size, BorderColor, BorderWidth, CornerRadius);
            }
            else
            {
                // 绘制普通边框
                DrawRectangle(new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)BorderWidth), BorderColor);
                DrawRectangle(new Rectangle((int)Position.X, (int)(Position.Y + Size.Y - BorderWidth), (int)Size.X, (int)BorderWidth), BorderColor);
                DrawRectangle(new Rectangle((int)Position.X, (int)Position.Y, (int)BorderWidth, (int)Size.Y), BorderColor);
                DrawRectangle(new Rectangle((int)(Position.X + Size.X - BorderWidth), (int)Position.Y, (int)BorderWidth, (int)Size.Y), BorderColor);
            }
        }
    }

    /// <summary>
    /// 绘制圆角矩形
    /// </summary>
    protected void DrawRoundedRectangle(Vector2 position, Vector2 size, Color color, float borderWidth, float cornerRadius)
    {
        // 确保圆角半径不超过尺寸的一半
        cornerRadius = Math.Min(cornerRadius, Math.Min(size.X, size.Y) / 2);

        // 绘制四个角
        DrawCircle(new Vector2(position.X + cornerRadius, position.Y + cornerRadius), cornerRadius, color, borderWidth);
        DrawCircle(new Vector2(position.X + size.X - cornerRadius, position.Y + cornerRadius), cornerRadius, color, borderWidth);
        DrawCircle(new Vector2(position.X + cornerRadius, position.Y + size.Y - cornerRadius), cornerRadius, color, borderWidth);
        DrawCircle(new Vector2(position.X + size.X - cornerRadius, position.Y + size.Y - cornerRadius), cornerRadius, color, borderWidth);

        // 绘制边框
        DrawRectangle(new Rectangle((int)(position.X + cornerRadius), (int)position.Y, (int)(size.X - 2 * cornerRadius), (int)borderWidth), color);
        DrawRectangle(new Rectangle((int)(position.X + cornerRadius), (int)(position.Y + size.Y - borderWidth), (int)(size.X - 2 * cornerRadius), (int)borderWidth), color);
        DrawRectangle(new Rectangle((int)position.X, (int)(position.Y + cornerRadius), (int)borderWidth, (int)(size.Y - 2 * cornerRadius)), color);
        DrawRectangle(new Rectangle((int)(position.X + size.X - borderWidth), (int)(position.Y + cornerRadius), (int)borderWidth, (int)(size.Y - 2 * cornerRadius)), color);
    }

    /// <summary>
    /// 绘制圆形
    /// </summary>
    protected void DrawCircle(Vector2 center, float radius, Color color, float borderWidth)
    {
        const int segments = 32;
        var angleStep = MathHelper.TwoPi / segments;

        for (var i = 0; i < segments; i++)
        {
            var angle1 = i * angleStep;
            var angle2 = (i + 1) * angleStep;

            var point1 = center + new Vector2(
                (float)Math.Cos(angle1) * radius,
                (float)Math.Sin(angle1) * radius
            );
            var point2 = center + new Vector2(
                (float)Math.Cos(angle2) * radius,
                (float)Math.Sin(angle2) * radius
            );

            DrawLine(point1, point2, color, borderWidth);
        }
    }

    /// <summary>
    /// 绘制线条
    /// </summary>
    protected void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        var edge = end - start;
        var angle = (float)Math.Atan2(edge.Y, edge.X);
        var length = edge.Length();

        var scale = new Vector2(length, width);
        var origin = new Vector2(0, width / 2);

        SpriteBatch.Draw(
            GetPixelTexture(),
            start,
            null,
            color,
            angle,
            origin,
            scale,
            SpriteEffects.None,
            0
        );
    }

    protected Texture2D GetPixelTexture()
    {
        if (_pixelTexture == null)
        {
            _pixelTexture = new Texture2D(SpriteBatch.GraphicsDevice, 1, 1);
            var colorData = new Color[1 * 1];
            for (var i = 0; i < colorData.Length; i++) colorData[i] = Color.White;
            _pixelTexture.SetData(colorData);
        }

        return _pixelTexture;
    }

    /// <summary>
    /// 获取当前背景颜色
    /// </summary>
    protected Color GetCurrentBackgroundColor()
    {
        if (IsDisabled)
            return DisabledBackgroundColor;

        return _isPressed ? PressedBackgroundColor :
            _isHovered ? HoverBackgroundColor :
            BackgroundColor ?? Color.Transparent;
    }
}