using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TaiChi.MonoGame.UI.Basic;

namespace TaiChi.MonoGame.UI.Controls;

/// <summary>
/// 滚动条方向
/// </summary>
public enum ScrollBarOrientation
{
    /// <summary>
    /// 垂直方向
    /// </summary>
    Vertical,

    /// <summary>
    /// 水平方向
    /// </summary>
    Horizontal
}

/// <summary>
/// 滚动条控件，可以绑定到任何实现IScrollable接口的组件
/// </summary>
public class ScrollBar : UIElement
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public ScrollBar()
    {
        BackgroundColor = Color.Transparent;
    }

    /// <summary>
    /// 目标滚动组件的滚动偏移变化事件处理
    /// </summary>
    private void TargetScrollable_OnScrollOffsetChanged(object sender, ScrollEventArgs e)
    {
        // 如果不是由滚动条引起的滚动，则更新滑块位置
        if (!IsDragging) UpdateThumbPosition();
    }

    /// <summary>
    /// 目标滚动组件的内容高度变化事件处理
    /// </summary>
    private void TargetScrollable_OnContentHeightChanged(object sender, ContentHeightEventArgs e)
    {
        // 更新滑块尺寸和位置
        UpdateThumbMetrics();
    }

    /// <summary>
    /// 更新滑块尺寸和位置
    /// </summary>
    private void UpdateThumbMetrics()
    {
        if (_targetScrollable == null)
            return;

        // 计算轨道区域
        _trackRect = new Rectangle(
            (int)Position.X,
            (int)Position.Y,
            (int)Size.X,
            (int)Size.Y
        );

        // 计算滑块尺寸
        var contentSize = _targetScrollable.ContentHeight;
        var viewportSize = _targetScrollable.ViewportHeight;

        if (contentSize <= viewportSize)
        {
            // 内容小于或等于视口，滑块占据整个轨道
            _thumbRect = _trackRect;
            return;
        }

        var trackSize = Orientation == ScrollBarOrientation.Vertical ? Size.Y : Size.X;
        var thumbSize = Math.Max(MinThumbSize, viewportSize / contentSize * trackSize);

        // 计算滑块位置
        float thumbPosition;
        if (_targetScrollable.MaxScrollOffset <= 0)
        {
            thumbPosition = 0;
        }
        else
        {
            var scrollPercentage = _targetScrollable.ScrollOffset / _targetScrollable.MaxScrollOffset;
            thumbPosition = scrollPercentage * (trackSize - thumbSize);
        }

        // 更新滑块矩形
        if (Orientation == ScrollBarOrientation.Vertical)
            _thumbRect = new Rectangle(
                _trackRect.X,
                _trackRect.Y + (int)thumbPosition,
                _trackRect.Width,
                (int)thumbSize
            );
        else
            _thumbRect = new Rectangle(
                _trackRect.X + (int)thumbPosition,
                _trackRect.Y,
                (int)thumbSize,
                _trackRect.Height
            );
    }

    /// <summary>
    /// 更新滑块位置
    /// </summary>
    private void UpdateThumbPosition()
    {
        if (_targetScrollable == null || _targetScrollable.MaxScrollOffset <= 0)
            return;

        var contentSize = _targetScrollable.ContentHeight;
        var viewportSize = _targetScrollable.ViewportHeight;
        var trackSize = Orientation == ScrollBarOrientation.Vertical ? Size.Y : Size.X;
        float thumbSize = Orientation == ScrollBarOrientation.Vertical ? _thumbRect.Height : _thumbRect.Width;

        var scrollPercentage = _targetScrollable.ScrollOffset / _targetScrollable.MaxScrollOffset;
        var thumbPosition = scrollPercentage * (trackSize - thumbSize);

        if (Orientation == ScrollBarOrientation.Vertical)
            _thumbRect.Y = _trackRect.Y + (int)thumbPosition;
        else
            _thumbRect.X = _trackRect.X + (int)thumbPosition;
    }

    /// <summary>
    /// 从滑块位置计算滚动偏移
    /// </summary>
    private float CalculateScrollOffsetFromThumbPosition()
    {
        if (_targetScrollable == null || _targetScrollable.MaxScrollOffset <= 0)
            return 0;

        var trackSize = Orientation == ScrollBarOrientation.Vertical ? Size.Y : Size.X;
        float thumbSize = Orientation == ScrollBarOrientation.Vertical ? _thumbRect.Height : _thumbRect.Width;
        float thumbPosition = Orientation == ScrollBarOrientation.Vertical ? _thumbRect.Y - _trackRect.Y : _thumbRect.X - _trackRect.X;

        var scrollPercentage = thumbPosition / (trackSize - thumbSize);
        return scrollPercentage * _targetScrollable.MaxScrollOffset;
    }

    /// <summary>
    ///     更新滚动条
    /// </summary>
    /// <param name="gameTime">游戏时间</param>
    public override void Update(GameTime gameTime)
    {
        if (!IsVisible || _targetScrollable == null)
            return;

        base.Update(gameTime);

        var mouseState = Mouse.GetState();
        // 使用ScreenToVirtualCoordinates转换鼠标坐标
        var mousePosition = ScreenToVirtualCoordinates(mouseState.Position);

        // 检查鼠标是否在滑块上
        var wasHoverThumb = _isHoverThumb;
        _isHoverThumb = _thumbRect.Contains(mousePosition);

        // 处理拖动
        if (IsDragging)
        {
            if (mouseState.LeftButton == ButtonState.Released)
            {
                IsDragging = false;
            }
            else
            {
                float mouseDelta;
                // 使用ScreenToVirtualCoordinates转换上一帧的鼠标坐标
                var previousMousePosition = ScreenToVirtualCoordinates(_previousMouseState.Position);

                if (Orientation == ScrollBarOrientation.Vertical)
                {
                    mouseDelta = mousePosition.Y - previousMousePosition.Y;
                    _thumbRect.Y = (int)MathHelper.Clamp(_thumbRect.Y + mouseDelta, _trackRect.Y, _trackRect.Y + _trackRect.Height - _thumbRect.Height);
                }
                else
                {
                    mouseDelta = mousePosition.X - previousMousePosition.X;
                    _thumbRect.X = (int)MathHelper.Clamp(_thumbRect.X + mouseDelta, _trackRect.X, _trackRect.X + _trackRect.Width - _thumbRect.Width);
                }

                // 根据滑块位置计算滚动偏移
                var newScrollOffset = CalculateScrollOffsetFromThumbPosition();

                // 应用滚动
                if (_targetScrollable.ScrollOffset != newScrollOffset)
                {
                    _targetScrollable.ScrollOffset = newScrollOffset;
                    OnScroll?.Invoke(this, new ScrollEventArgs(newScrollOffset, _targetScrollable.MaxScrollOffset));
                }
            }
        }
        else if (_isHoverThumb)
        {
            // 检查是否开始拖动
            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                IsDragging = true;

                if (Orientation == ScrollBarOrientation.Vertical)
                    _dragOffset = mousePosition.Y - _thumbRect.Y;
                else
                    _dragOffset = mousePosition.X - _thumbRect.X;
            }
        }
        else if (_trackRect.Contains(mousePosition))
        {
            // 点击轨道
            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                var clickAboveThumb = false;

                if (Orientation == ScrollBarOrientation.Vertical)
                    clickAboveThumb = mousePosition.Y < _thumbRect.Y;
                else
                    clickAboveThumb = mousePosition.X < _thumbRect.X;

                // 向上或向下翻页
                var pageSize = _targetScrollable.ViewportHeight;
                var scrollDelta = clickAboveThumb ? -pageSize : pageSize;

                _targetScrollable.ScrollBy(scrollDelta);
                OnScroll?.Invoke(this, new ScrollEventArgs(_targetScrollable.ScrollOffset, _targetScrollable.MaxScrollOffset));
            }
        }

        // 处理鼠标滚轮
        var scrollWheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        if (_trackRect.Contains(mousePosition) && scrollWheelDelta != 0)
        {
            // 计算滚动量
            var scrollAmount = scrollWheelDelta / 120f * 20f; // 每次滚动20像素

            // 应用滚动
            _targetScrollable.ScrollBy(-scrollAmount);
            OnScroll?.Invoke(this, new ScrollEventArgs(_targetScrollable.ScrollOffset, _targetScrollable.MaxScrollOffset));
        }

        _previousMouseState = mouseState;
    }

    /// <summary>
    ///     绘制滚动条
    /// </summary>
    /// <param name="spriteBatch">精灵批处理</param>
    public override void Draw(GameTime gameTime)
    {
        if (!IsVisible || _targetScrollable == null)
            return;

        // 确保有像素纹理
        if (_pixelTexture == null)
        {
            _pixelTexture = new Texture2D(SpriteBatch.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        // 绘制背景
        base.Draw(gameTime);

        // 绘制滚动条轨道
        DrawRectangle(_trackRect, BackgroundColor);

        // 绘制滑块
        var thumbColor = IsDragging ? ThumbActiveColor : _isHoverThumb ? ThumbHoverColor : ThumbColor;

        if (CornerRadius > 0)
        {
            // 绘制圆角滑块
            DrawRoundedRectangle(new Vector2(_thumbRect.X, _thumbRect.Y),
                new Vector2(_thumbRect.Width, _thumbRect.Height), thumbColor, BorderWidth, CornerRadius);
        }
        else
        {
            // 绘制矩形滑块
            DrawRectangle(_thumbRect, thumbColor);

            // 绘制边框
            if (BorderWidth > 0)
            {
                var borderColor = BorderColor;

                // 上边框
                DrawRectangle(new Rectangle(_thumbRect.X, _thumbRect.Y, _thumbRect.Width, (int)BorderWidth), borderColor);
                // 下边框
                DrawRectangle(new Rectangle(_thumbRect.X, _thumbRect.Y + _thumbRect.Height - (int)BorderWidth, _thumbRect.Width, (int)BorderWidth), borderColor);
                // 左边框
                DrawRectangle(new Rectangle(_thumbRect.X, _thumbRect.Y, (int)BorderWidth, _thumbRect.Height), borderColor);
                // 右边框
                DrawRectangle(new Rectangle(_thumbRect.X + _thumbRect.Width - (int)BorderWidth, _thumbRect.Y, (int)BorderWidth, _thumbRect.Height), borderColor);
            }
        }
    }

    /// <summary>
    ///     绘制圆角矩形
    /// </summary>
    private void DrawRoundedRectangle(Vector2 position, Vector2 size, Color color, float borderWidth, float cornerRadius)
    {
        // 简化版圆角矩形绘制，实际项目可能需要更复杂的实现
        // 绘制外部矩形
        DrawRectangle(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);

        // 绘制四个圆角
        var radius = (int)cornerRadius;

        // 左上角
        DrawRectangle(new Rectangle((int)position.X, (int)position.Y, radius, radius), color);

        // 右上角
        DrawRectangle(new Rectangle((int)(position.X + size.X - radius), (int)position.Y, radius, radius), color);

        // 左下角
        DrawRectangle(new Rectangle((int)position.X, (int)(position.Y + size.Y - radius), radius, radius), color);

        // 右下角
        DrawRectangle(new Rectangle((int)(position.X + size.X - radius), (int)(position.Y + size.Y - radius), radius, radius), color);
    }

    /// <summary>
    ///     绘制矩形
    /// </summary>
    private new void DrawRectangle(Rectangle rectangle, Color color)
    {
        if (_pixelTexture == null)
        {
            _pixelTexture = new Texture2D(SpriteBatch.GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        SpriteBatch.Draw(_pixelTexture, rectangle, color);
    }

    #region 属性

    /// <summary>
    ///     滚动条宽度
    /// </summary>
    public float ScrollBarWidth { get; set; } = 12f;

    /// <summary>
    ///     滑块颜色
    /// </summary>
    public Color ThumbColor { get; set; } = new(100, 100, 100);

    /// <summary>
    ///     滑块悬停颜色
    /// </summary>
    public Color ThumbHoverColor { get; set; } = new(130, 130, 130);

    /// <summary>
    ///     滑块激活颜色
    /// </summary>
    public Color ThumbActiveColor { get; set; } = new(150, 150, 150);

    /// <summary>
    ///     滚动条背景颜色
    /// </summary>
    public Color BackgroundColor { get; set; } = new(220, 220, 220);

    /// <summary>
    ///     滚动条方向
    /// </summary>
    public ScrollBarOrientation Orientation { get; set; } = ScrollBarOrientation.Vertical;

    /// <summary>
    ///     边框颜色
    /// </summary>
    public Color BorderColor { get; set; } = Color.Gray;

    /// <summary>
    ///     边框宽度
    /// </summary>
    public float BorderWidth { get; set; } = 1f;

    /// <summary>
    ///     按钮圆角半径
    /// </summary>
    public float CornerRadius { get; set; } = 2f;

    /// <summary>
    ///     滑块最小尺寸
    /// </summary>
    public float MinThumbSize { get; set; } = 30f;

    /// <summary>
    ///     滚动事件
    /// </summary>
    public event EventHandler<ScrollEventArgs> OnScroll;

    /// <summary>
    ///     绑定的可滚动组件
    /// </summary>
    public IScrollable TargetScrollable
    {
        get => _targetScrollable;
        set
        {
            if (_targetScrollable != value)
            {
                // 移除旧目标的事件处理
                if (_targetScrollable != null)
                {
                    _targetScrollable.OnScrollOffsetChanged -= TargetScrollable_OnScrollOffsetChanged;
                    _targetScrollable.OnContentHeightChanged -= TargetScrollable_OnContentHeightChanged;
                }

                _targetScrollable = value;

                // 添加新目标的事件处理
                if (_targetScrollable != null)
                {
                    _targetScrollable.OnScrollOffsetChanged += TargetScrollable_OnScrollOffsetChanged;
                    _targetScrollable.OnContentHeightChanged += TargetScrollable_OnContentHeightChanged;

                    // 更新滑块尺寸和位置
                    UpdateThumbMetrics();
                }
            }
        }
    }

    /// <summary>
    ///     获取滑块是否被拖动中
    /// </summary>
    public bool IsDragging { get; private set; }

    #endregion

    #region 私有字段

    // 绑定的可滚动组件
    private IScrollable _targetScrollable;

    // 1x1像素纹理，用于绘制矩形
    private Texture2D _pixelTexture;

    // 滑块区域
    private Rectangle _thumbRect;

    // 滚动区域
    private Rectangle _trackRect;

    // 是否正在拖动滑块

    // 拖动偏移
    private float _dragOffset;

    // 是否悬停在滑块上
    private bool _isHoverThumb;

    // 上一帧鼠标状态
    private MouseState _previousMouseState;

    #endregion
}