using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TaiChi.MonoGame.UI.Basic;
using TaiChi.MonoGame.UI.Controls;

namespace TaiChi.MonoGame.UI.Examples;

/// <summary>
/// 可滚动组件示例
/// </summary>
public class ScrollableExample : Game
{
    private readonly GraphicsDeviceManager _graphics;

    // 自定义可滚动面板
    private CustomScrollablePanel _customPanel;
    private ScrollBar _customScrollBar;
    private SpriteFont _font;
    private ScrollBar _listScrollBar;

    // UI 元素
    private ListView _listView;
    private SpriteBatch _spriteBatch;

    public ScrollableExample()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 600;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Font"); // 需要添加一个字体资源

        // 创建列表视图
        _listView = new ListView
        {
            Position = new Vector2(50, 50),
            Size = new Vector2(300, 400),
            ItemHeight = 40,
            ItemSpacing = 5,
            Font = _font,
            BorderWidth = 2,
            CornerRadius = 5
        };

        // 添加一些测试数据
        for (var i = 1; i <= 30; i++) _listView.AddItem($"列表项 {i}");

        // 创建列表滚动条
        _listScrollBar = new ScrollBar
        {
            Position = new Vector2(360, 50),
            Size = new Vector2(20, 400),
            CornerRadius = 5,
            BorderWidth = 1,
            TargetScrollable = _listView // 绑定到列表视图
        };

        // 创建自定义可滚动面板
        _customPanel = new CustomScrollablePanel
        {
            Position = new Vector2(430, 50),
            Size = new Vector2(300, 400),
            Font = _font,
            BorderWidth = 2,
            CornerRadius = 5
        };

        // 添加一些测试内容
        _customPanel.SetContent("这是一个自定义的可滚动面板，它实现了IScrollable接口。\n\n" +
                                "任何实现IScrollable接口的组件都可以连接到ScrollBar控件，这样就可以使用统一的滚动条组件，而不需要为每种可滚动控件单独实现滚动条。\n\n" +
                                "这种设计方式的优点是：\n\n" +
                                "1. 提高了代码的复用性\n" +
                                "2. 降低了组件之间的耦合度\n" +
                                "3. 使界面更加一致\n" +
                                "4. 方便扩展新的可滚动组件\n\n" +
                                "接口定义了基本的滚动属性和方法，例如：\n\n" +
                                "- ScrollOffset：当前滚动偏移量\n" +
                                "- MaxScrollOffset：最大滚动偏移量\n" +
                                "- ContentHeight：内容总高度\n" +
                                "- ViewportHeight：可视区域高度\n\n" +
                                "接口还定义了一组方法，例如ScrollTo、ScrollBy、ScrollToTop、ScrollToBottom等，以及获取和设置滚动百分比的方法。\n\n" +
                                "通过这种方式，滚动条可以与任何实现了此接口的组件进行交互，无论该组件是列表视图、文本框、图像查看器还是其他自定义组件。");

        // 创建自定义面板的滚动条
        _customScrollBar = new ScrollBar
        {
            Position = new Vector2(740, 50),
            Size = new Vector2(20, 400),
            CornerRadius = 5,
            BorderWidth = 1,
            TargetScrollable = _customPanel // 绑定到自定义面板
        };
    }

    protected override void Update(GameTime gameTime)
    {
        // 更新UI元素
        _listView.Update(gameTime);
        _listScrollBar.Update(gameTime);
        _customPanel.Update(gameTime);
        _customScrollBar.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _spriteBatch.Begin();

        // 绘制标题
        _spriteBatch.DrawString(_font, "可滚动组件示例", new Vector2(50, 20), Color.White);
        _spriteBatch.DrawString(_font, "ListView + ScrollBar", new Vector2(50, 30), Color.White);
        _spriteBatch.DrawString(_font, "自定义面板 + ScrollBar", new Vector2(430, 30), Color.White);

        // 绘制UI元素
        _listView.Draw(gameTime);
        _listScrollBar.Draw(gameTime);
        _customPanel.Draw(gameTime);
        _customScrollBar.Draw(gameTime);

        _spriteBatch.End();

        base.Draw(gameTime);
    }
}

/// <summary>
/// 自定义可滚动面板，实现IScrollable接口
/// </summary>
public class CustomScrollablePanel : UIElement, IScrollable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public CustomScrollablePanel()
    {
        BackgroundColor = Color.White;
    }

    /// <summary>
    /// 设置文本内容
    /// </summary>
    /// <param name="text">文本内容</param>
    public void SetContent(string text)
    {
        Text = text ?? string.Empty;

        // 分割文本为行
        _textLines.Clear();
        var lines = Text.Split('\n');

        foreach (var line in lines) _textLines.Add(line);

        // 计算内容高度
        if (Font != null)
        {
            _lineHeight = Font.MeasureString("M").Y;
            ContentHeight = _textLines.Count * _lineHeight + 2 * Padding;

            // 更新最大滚动偏移
            UpdateMaxScrollOffset();
        }
    }

    /// <summary>
    /// 更新最大滚动偏移
    /// </summary>
    private void UpdateMaxScrollOffset()
    {
        var oldMaxScrollOffset = MaxScrollOffset;
        MaxScrollOffset = Math.Max(0, ContentHeight - Size.Y);

        // 确保滚动偏移不超过最大值
        ScrollOffset = Math.Min(_scrollOffset, MaxScrollOffset);

        // 通知内容高度变化
        OnContentHeightChanged?.Invoke(this, new ContentHeightEventArgs(ContentHeight));
    }

    /// <summary>
    /// 更新面板
    /// </summary>
    /// <param name="gameTime">游戏时间</param>
    public override void Update(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        base.Update(gameTime);
    }

    /// <summary>
    /// 绘制面板
    /// </summary>
    /// <param name="gameTime"></param>
    public override void Draw(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        // 确保有像素纹理
        if (_pixelTexture == null)
        {
            _pixelTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        // 绘制背景
        base.Draw(gameTime);

        // 绘制边框
        if (BorderWidth > 0)
        {
            if (CornerRadius > 0)
            {
                // 绘制圆角边框
                DrawRoundedRectangle(SpriteBatch, Position, Size, BorderColor, BorderWidth, CornerRadius);
            }
            else
            {
                // 绘制矩形边框
                DrawRectangle(SpriteBatch, new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)BorderWidth), BorderColor);
                DrawRectangle(SpriteBatch, new Rectangle((int)Position.X, (int)(Position.Y + Size.Y - BorderWidth), (int)Size.X, (int)BorderWidth), BorderColor);
                DrawRectangle(SpriteBatch, new Rectangle((int)Position.X, (int)Position.Y, (int)BorderWidth, (int)Size.Y), BorderColor);
                DrawRectangle(SpriteBatch, new Rectangle((int)(Position.X + Size.X - BorderWidth), (int)Position.Y, (int)BorderWidth, (int)Size.Y), BorderColor);
            }
        }

        // 创建裁剪区域，确保内容不会溢出面板边界
        var scissorRect = new Rectangle(
            (int)Position.X,
            (int)Position.Y,
            (int)Size.X,
            (int)Size.Y
        );

        var originalScissorRect = SpriteBatch.GraphicsDevice.ScissorRectangle;
        SpriteBatch.End();

        // 设置裁剪区域
        SpriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;
        SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, new RasterizerState { ScissorTestEnable = true });

        // 绘制文本内容
        if (Font != null && _textLines.Count > 0)
        {
            var y = Position.Y + Padding - _scrollOffset;

            for (var i = 0; i < _textLines.Count; i++)
            {
                // 只绘制可见行
                if (y + _lineHeight >= Position.Y && y <= Position.Y + Size.Y) SpriteBatch.DrawString(Font, _textLines[i], new Vector2(Position.X + Padding, y), TextColor);

                y += _lineHeight;
            }
        }

        // 恢复原始裁剪区域
        SpriteBatch.End();
        SpriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
        SpriteBatch.Begin();
    }

    /// <summary>
    /// 绘制圆角矩形
    /// </summary>
    private void DrawRoundedRectangle(SpriteBatch spriteBatch, Vector2 position, Vector2 size, Color color, float borderWidth, float cornerRadius)
    {
        // 简化版圆角矩形绘制，实际项目可能需要更复杂的实现
        // 绘制上边框
        DrawRectangle(spriteBatch, new Rectangle((int)(position.X + cornerRadius), (int)position.Y, (int)(size.X - 2 * cornerRadius), (int)borderWidth), color);

        // 绘制下边框
        DrawRectangle(spriteBatch, new Rectangle((int)(position.X + cornerRadius), (int)(position.Y + size.Y - borderWidth), (int)(size.X - 2 * cornerRadius), (int)borderWidth), color);

        // 绘制左边框
        DrawRectangle(spriteBatch, new Rectangle((int)position.X, (int)(position.Y + cornerRadius), (int)borderWidth, (int)(size.Y - 2 * cornerRadius)), color);

        // 绘制右边框
        DrawRectangle(spriteBatch, new Rectangle((int)(position.X + size.X - borderWidth), (int)(position.Y + cornerRadius), (int)borderWidth, (int)(size.Y - 2 * cornerRadius)), color);
    }

    /// <summary>
    /// 绘制矩形
    /// </summary>
    private new void DrawRectangle(SpriteBatch spriteBatch, Rectangle rectangle, Color color)
    {
        if (_pixelTexture == null)
        {
            _pixelTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        spriteBatch.Draw(_pixelTexture, rectangle, color);
    }

    #region IScrollable接口实现

    /// <summary>
    /// 当前滚动偏移量
    /// </summary>
    public float ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            var newValue = MathHelper.Clamp(value, 0, MaxScrollOffset);
            if (_scrollOffset != newValue)
            {
                _scrollOffset = newValue;
                OnScrollOffsetChanged?.Invoke(this, new ScrollEventArgs(_scrollOffset, MaxScrollOffset));
            }
        }
    }

    /// <summary>
    /// 最大滚动偏移量
    /// </summary>
    public float MaxScrollOffset { get; private set; }

    /// <summary>
    /// 内容总高度
    /// </summary>
    public float ContentHeight { get; private set; }

    /// <summary>
    /// 可视区域高度
    /// </summary>
    public float ViewportHeight => Size.Y;

    /// <summary>
    /// 滚动偏移变化事件
    /// </summary>
    public event EventHandler<ScrollEventArgs> OnScrollOffsetChanged;

    /// <summary>
    /// 内容高度变化事件
    /// </summary>
    public event EventHandler<ContentHeightEventArgs> OnContentHeightChanged;

    /// <summary>
    /// 滚动到指定偏移位置
    /// </summary>
    /// <param name="offset">目标偏移位置</param>
    public void ScrollTo(float offset)
    {
        ScrollOffset = offset;
    }

    /// <summary>
    /// 滚动指定的距离
    /// </summary>
    /// <param name="delta">滚动距离，正值向下滚动，负值向上滚动</param>
    public void ScrollBy(float delta)
    {
        ScrollOffset = _scrollOffset + delta;
    }

    /// <summary>
    /// 滚动到顶部
    /// </summary>
    public void ScrollToTop()
    {
        ScrollOffset = 0;
    }

    /// <summary>
    /// 滚动到底部
    /// </summary>
    public void ScrollToBottom()
    {
        ScrollOffset = MaxScrollOffset;
    }

    /// <summary>
    /// 获取滚动百分比（0.0 - 1.0）
    /// </summary>
    /// <returns>滚动百分比</returns>
    public float GetScrollPercentage()
    {
        return MaxScrollOffset > 0 ? ScrollOffset / MaxScrollOffset : 0;
    }

    /// <summary>
    /// 设置滚动百分比（0.0 - 1.0）
    /// </summary>
    /// <param name="percentage">滚动百分比</param>
    public void SetScrollPercentage(float percentage)
    {
        ScrollOffset = MaxScrollOffset * MathHelper.Clamp(percentage, 0, 1);
    }

    #endregion

    #region 属性

    /// <summary>
    /// 字体
    /// </summary>
    public SpriteFont Font { get; set; }

    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// 文本颜色
    /// </summary>
    public Color TextColor { get; set; } = Color.Black;

    /// <summary>
    /// 内边距
    /// </summary>
    public float Padding { get; set; } = 10f;

    /// <summary>
    /// 边框颜色
    /// </summary>
    public Color BorderColor { get; set; } = Color.Gray;

    /// <summary>
    /// 边框宽度
    /// </summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>
    /// 按钮圆角半径
    /// </summary>
    public float CornerRadius { get; set; }

    #endregion

    #region 私有字段

    // 滚动偏移
    private float _scrollOffset;

    // 最大滚动偏移

    // 内容高度

    // 文本行列表
    private readonly List<string> _textLines = new();

    // 行高
    private float _lineHeight;

    // 1x1像素纹理，用于绘制矩形
    private Texture2D _pixelTexture;

    #endregion
}