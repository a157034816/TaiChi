using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TaiChi.MonoGame.UI.Basic;

/// <summary>
/// UI元素基类，提供基本的UI功能和父子关系
/// </summary>
public class UIElement : IUIElement
{
    /// <summary>
    /// 获取1x1像素纹理（共享实例）
    /// </summary>
    private static Texture2D _sharedPixelTexture;


    /// <summary>
    /// 构造函数
    /// </summary>
    public UIElement()
    {
    }

    /// <summary>
    /// 将屏幕坐标转换为虚拟坐标的函数
    /// 可以被游戏自定义以适配不同的坐标系统
    /// </summary>
    public static Func<Point, Point> ScreenToVirtualCoordinates { get; set; } = position => position;

    /// <summary>
    /// 将虚拟坐标转换为屏幕坐标的函数
    /// 可以被游戏自定义以适配不同的坐标系统
    /// </summary>
    public static Func<Point, Point> VirtualToScreenCoordinates { get; set; } = position => position;

    /// <summary>
    /// 是否可见
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 背景颜色
    /// </summary>
    public Color? BackgroundColor { get; set; }

    /// <summary>
    /// 背景纹理
    /// </summary>
    public Texture2D BackgroundTexture { get; set; }

    /// <summary>
    /// 纹理绘制模式
    /// </summary>
    public TextureDrawMode TextureDrawMode { get; set; } = TextureDrawMode.Stretch;

    /// <summary>
    /// 纹理绘制颜色（用于调整纹理的颜色或透明度）
    /// </summary>
    public Color TextureColor { get; set; } = Color.White;

    /// <summary>
    /// 纹理源矩形（用于绘制纹理的一部分）
    /// </summary>
    public Rectangle? TextureSourceRect { get; set; }

    /// <summary>
    /// 纹理绘制缩放比例
    /// </summary>
    public float TextureScale { get; set; } = 1.0f;

    /// <summary>
    /// 元素位置
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// 元素尺寸
    /// </summary>
    public Vector2 Size { get; set; }

    /// <summary>
    /// 父控件
    /// </summary>
    public IUIElement Parent { get; set; }

    public bool Enabled { get; set; } = true;

    public int UpdateOrder { get; set; } = 0;

    public int DrawOrder { get; set; } = 0;

    public bool Visible { get; set; } = true;
    public Game Game { get; set; }
    public SpriteBatch SpriteBatch { get; set; }

    /// <summary>
    /// 更新元素
    /// </summary>
    /// <param name="gameTime">游戏时间</param>
    public virtual void Update(GameTime gameTime)
    {
        if (!IsVisible)
            return;
    }

    /// <summary>
    /// 绘制元素
    /// </summary>
    /// <param name="gameTime"></param>
    public virtual void Draw(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        // 绘制背景
        if (BackgroundTexture != null || BackgroundColor.HasValue)
        {
            if (BackgroundTexture != null)
                // 根据不同的绘制模式绘制纹理
                switch (TextureDrawMode)
                {
                    case TextureDrawMode.Stretch:
                        // 拉伸模式 - 将纹理拉伸填充整个控件
                        SpriteBatch.Draw(
                            BackgroundTexture,
                            new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y),
                            TextureSourceRect,
                            TextureColor);
                        break;

                    case TextureDrawMode.Center:
                        // 居中模式 - 在控件中央绘制纹理
                        var texWidth = TextureSourceRect?.Width ?? BackgroundTexture.Width;
                        var texHeight = TextureSourceRect?.Height ?? BackgroundTexture.Height;

                        var scaledWidth = texWidth * TextureScale;
                        var scaledHeight = texHeight * TextureScale;

                        var x = Position.X + (Size.X - scaledWidth) / 2;
                        var y = Position.Y + (Size.Y - scaledHeight) / 2;

                        SpriteBatch.Draw(
                            BackgroundTexture,
                            new Rectangle((int)x, (int)y, (int)scaledWidth, (int)scaledHeight),
                            TextureSourceRect,
                            TextureColor);
                        break;

                    case TextureDrawMode.Tile:
                        // 平铺模式 - 将纹理在控件内平铺
                        var tileWidth = TextureSourceRect?.Width ?? BackgroundTexture.Width;
                        var tileHeight = TextureSourceRect?.Height ?? BackgroundTexture.Height;

                        // 计算需要绘制的行数和列数
                        var columns = (int)Math.Ceiling(Size.X / (tileWidth * TextureScale));
                        var rows = (int)Math.Ceiling(Size.Y / (tileHeight * TextureScale));

                        for (var row = 0; row < rows; row++)
                        for (var col = 0; col < columns; col++)
                        {
                            var tileX = Position.X + col * tileWidth * TextureScale;
                            var tileY = Position.Y + row * tileHeight * TextureScale;

                            SpriteBatch.Draw(
                                BackgroundTexture,
                                new Rectangle(
                                    (int)tileX,
                                    (int)tileY,
                                    (int)(tileWidth * TextureScale),
                                    (int)(tileHeight * TextureScale)),
                                TextureSourceRect,
                                TextureColor);
                        }

                        break;

                    case TextureDrawMode.Fit:
                        // 适应模式 - 保持纹理比例，适应控件尺寸
                        float textureWidth = TextureSourceRect?.Width ?? BackgroundTexture.Width;
                        float textureHeight = TextureSourceRect?.Height ?? BackgroundTexture.Height;

                        var widthRatio = Size.X / textureWidth;
                        var heightRatio = Size.Y / textureHeight;
                        var ratio = Math.Min(widthRatio, heightRatio);

                        var fitWidth = textureWidth * ratio;
                        var fitHeight = textureHeight * ratio;

                        var fitX = Position.X + (Size.X - fitWidth) / 2;
                        var fitY = Position.Y + (Size.Y - fitHeight) / 2;

                        SpriteBatch.Draw(
                            BackgroundTexture,
                            new Rectangle((int)fitX, (int)fitY, (int)fitWidth, (int)fitHeight),
                            TextureSourceRect,
                            TextureColor);
                        break;
                }
            else if (BackgroundColor.HasValue)
                DrawRectangle(new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y),
                    BackgroundColor.Value);
        }
    }

    public event EventHandler<EventArgs> EnabledChanged;
    public event EventHandler<EventArgs> UpdateOrderChanged;
    public event EventHandler<EventArgs> DrawOrderChanged;
    public event EventHandler<EventArgs> VisibleChanged;

    /// <summary>
    /// 测量元素所需的尺寸
    /// </summary>
    /// <param name="availableSize">可用尺寸</param>
    /// <returns>计算出的尺寸</returns>
    public virtual Vector2 Measure(Vector2 availableSize)
    {
        return Size;
    }

    /// <summary>
    /// 安排元素布局
    /// </summary>
    /// <param name="finalSize">最终尺寸</param>
    public virtual void Arrange(Vector2 finalSize)
    {
        Size = finalSize;
    }

    public virtual void SetGame(Game game)
    {
        Game = game;
    }

    public virtual void SetSpriteBatch(SpriteBatch spriteBatch)
    {
        SpriteBatch = spriteBatch;
    }

    /// <summary>
    /// 绘制填充矩形
    /// </summary>
    protected void DrawRectangle(Rectangle rectangle, Color color)
    {
        // 使用共享的白色纹理，避免重复创建
        var pixel = GetPixelTexture();
        SpriteBatch.Draw(pixel, rectangle, color);
    }


    protected Texture2D GetPixelTexture()
    {
        if (_sharedPixelTexture == null || _sharedPixelTexture.IsDisposed)
        {
            _sharedPixelTexture = new Texture2D(SpriteBatch.GraphicsDevice, 1, 1);
            _sharedPixelTexture.SetData(new[] { Color.White });
        }

        return _sharedPixelTexture;
    }
}

/// <summary>
/// 纹理绘制模式枚举
/// </summary>
public enum TextureDrawMode
{
    /// <summary>
    /// 拉伸 - 将纹理拉伸填充整个控件
    /// </summary>
    Stretch,

    /// <summary>
    /// 居中 - 在控件中央绘制纹理
    /// </summary>
    Center,

    /// <summary>
    /// 平铺 - 将纹理在控件内平铺
    /// </summary>
    Tile,

    /// <summary>
    /// 适应 - 保持纹理比例，适应控件尺寸
    /// </summary>
    Fit
}