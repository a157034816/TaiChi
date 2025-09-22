using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TaiChi.MonoGame.UI.Basic;

namespace TaiChi.MonoGame.UI.Controls;

/// <summary>
/// 列表视图控件，允许显示可滚动的项目列表，并支持自定义项目渲染
/// </summary>
public class ListView : UIElement, IScrollable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public ListView()
    {
        BackgroundColor = Color.White;
    }

    /// <summary>
    /// 添加项目
    /// </summary>
    /// <param name="item">要添加的项目</param>
    public void AddItem(object item)
    {
        Items.Add(item);
        UpdateMaxScrollOffset();
    }

    /// <summary>
    /// 插入项目
    /// </summary>
    /// <param name="index">插入位置</param>
    /// <param name="item">要插入的项目</param>
    public void InsertItem(int index, object item)
    {
        Items.Insert(index, item);
        UpdateMaxScrollOffset();
    }

    /// <summary>
    /// 移除项目
    /// </summary>
    /// <param name="item">要移除的项目</param>
    /// <returns>移除是否成功</returns>
    public bool RemoveItem(object item)
    {
        var result = Items.Remove(item);

        if (result)
        {
            // 如果删除的是选中项，重置选择
            if (SelectedItem == item)
                SelectedIndex = -1;
            else if (SelectedIndex >= Items.Count)
                // 如果选中项在被删除项之后，调整选择索引
                SelectedIndex = Items.Count - 1;

            UpdateMaxScrollOffset();
        }

        return result;
    }

    /// <summary>
    /// 移除指定索引的项目
    /// </summary>
    /// <param name="index">要移除的项目索引</param>
    public void RemoveItemAt(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            Items.RemoveAt(index);

            // 调整选择索引
            if (SelectedIndex == index)
                SelectedIndex = -1;
            else if (SelectedIndex > index) SelectedIndex--;

            UpdateMaxScrollOffset();
        }
    }

    /// <summary>
    /// 清空项目列表
    /// </summary>
    public void ClearItems()
    {
        Items.Clear();
        SelectedIndex = -1;
        _scrollOffset = 0f;
        MaxScrollOffset = 0f;
        OnContentHeightChanged?.Invoke(this, new ContentHeightEventArgs(0));
    }

    /// <summary>
    /// 更新最大滚动偏移
    /// </summary>
    private void UpdateMaxScrollOffset()
    {
        var totalContentHeight = ContentHeight;
        var oldMaxScrollOffset = MaxScrollOffset;
        MaxScrollOffset = Math.Max(0, totalContentHeight - Size.Y);

        // 确保滚动偏移不超过最大值
        ScrollOffset = Math.Min(_scrollOffset, MaxScrollOffset);

        // 通知内容高度变化
        OnContentHeightChanged?.Invoke(this, new ContentHeightEventArgs(totalContentHeight));
    }

    /// <summary>
    /// 滚动到指定项目
    /// </summary>
    /// <param name="index">项目索引</param>
    public void ScrollToItem(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            var itemTop = index * (ItemHeight + ItemSpacing);
            var itemBottom = itemTop + ItemHeight;

            // 如果项目在可视区域外，则滚动到可见位置
            if (itemTop < _scrollOffset)
                // 滚动使项目置顶
                ScrollOffset = itemTop;
            else if (itemBottom > _scrollOffset + Size.Y)
                // 滚动使项目置底
                ScrollOffset = itemBottom - Size.Y;
        }
    }

    /// <summary>
    /// 更新列表视图
    /// </summary>
    /// <param name="gameTime">游戏时间</param>
    public override void Update(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        // 获取鼠标状态
        var mouseState = Mouse.GetState();
        var mousePosition = ScreenToVirtualCoordinates(mouseState.Position);

        // 检查鼠标是否在列表视图范围内
        var listViewRect = new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);
        var isMouseOver = listViewRect.Contains(mousePosition);

        // 处理滚轮滚动
        if (isMouseOver)
        {
            var scrollWheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollWheelDelta != 0)
            {
                // 计算滚动量
                var scrollAmount = scrollWheelDelta / 120f * 20f; // 每次滚动20像素

                // 应用滚动
                ScrollBy(-scrollAmount);
            }
        }

        // 处理项目悬停和点击
        _hoverIndex = -1;

        if (isMouseOver && Items.Count > 0)
        {
            // 计算可见项范围
            var firstVisibleIndex = (int)(_scrollOffset / (ItemHeight + ItemSpacing));
            var lastVisibleIndex = (int)((_scrollOffset + Size.Y) / (ItemHeight + ItemSpacing));

            // 限制范围
            firstVisibleIndex = Math.Max(0, firstVisibleIndex);
            lastVisibleIndex = Math.Min(Items.Count - 1, lastVisibleIndex);

            // 检查鼠标悬停在哪个项目上
            for (var i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                var itemRect = GetItemRect(i);

                if (itemRect.Contains(mousePosition))
                {
                    _hoverIndex = i;

                    // 处理点击
                    if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
                    {
                        // 选择项目
                        SelectedIndex = i;

                        // 触发点击事件
                        OnItemClick?.Invoke(this, new ItemClickEventArgs(i, Items[i]));

                        // 检查是否为双击
                        if (_doubleClickTimer > 0 && _doubleClickIndex == i) OnItemDoubleClick?.Invoke(this, new ItemClickEventArgs(i, Items[i]));

                        // 重置双击计时器
                        _doubleClickTimer = DoubleClickTime;
                        _doubleClickIndex = i;
                    }

                    break;
                }
            }
        }

        // 更新双击计时器
        if (_doubleClickTimer > 0) _doubleClickTimer -= (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        _previousMouseState = mouseState;
    }

    /// <summary>
    /// 绘制列表视图
    /// </summary>
    /// <param name="gameTime"></param>
    public override void Draw(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        // 确保有像素纹理
        if (_pixelTexture == null)
        {
            _pixelTexture = new Texture2D(SpriteBatch.GraphicsDevice, 1, 1);
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
                DrawRoundedRectangle(Position, Size, BorderColor, BorderWidth, CornerRadius);
            }
            else
            {
                // 绘制矩形边框
                DrawRectangle(new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)BorderWidth), BorderColor);
                DrawRectangle(new Rectangle((int)Position.X, (int)(Position.Y + Size.Y - BorderWidth), (int)Size.X, (int)BorderWidth), BorderColor);
                DrawRectangle(new Rectangle((int)Position.X, (int)Position.Y, (int)BorderWidth, (int)Size.Y), BorderColor);
                DrawRectangle(new Rectangle((int)(Position.X + Size.X - BorderWidth), (int)Position.Y, (int)BorderWidth, (int)Size.Y), BorderColor);
            }
        }

        // 创建裁剪区域，确保项目不会溢出列表视图边界
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

        // 绘制项目
        if (Items.Count > 0)
        {
            // 计算可见项范围
            var firstVisibleIndex = (int)(_scrollOffset / (ItemHeight + ItemSpacing));
            var lastVisibleIndex = (int)((_scrollOffset + Size.Y) / (ItemHeight + ItemSpacing));

            // 限制范围
            firstVisibleIndex = Math.Max(0, firstVisibleIndex);
            lastVisibleIndex = Math.Min(Items.Count - 1, lastVisibleIndex);

            // 绘制可见项
            for (var i = firstVisibleIndex; i <= lastVisibleIndex; i++) DrawItem(i);
        }

        // 恢复原始裁剪区域
        SpriteBatch.End();
        SpriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
        SpriteBatch.Begin();
    }

    /// <summary>
    /// 获取项目矩形区域
    /// </summary>
    /// <param name="index">项目索引</param>
    /// <returns>项目的矩形区域</returns>
    protected Rectangle GetItemRect(int index)
    {
        var y = Position.Y + index * (ItemHeight + ItemSpacing) - _scrollOffset;

        return new Rectangle(
            (int)Position.X,
            (int)y,
            (int)Size.X,
            (int)ItemHeight
        );
    }

    /// <summary>
    /// 绘制项目
    /// </summary>
    /// <param name="spriteBatch">精灵批处理</param>
    /// <param name="index">项目索引</param>
    protected virtual void DrawItem(int index)
    {
        if (index < 0 || index >= Items.Count)
            return;

        var itemRect = GetItemRect(index);
        var isSelected = index == SelectedIndex;
        var isHovered = index == _hoverIndex;
        var item = Items[index];

        // 绘制项目背景
        var bgColor = isSelected ? SelectedItemBackgroundColor : isHovered ? HoverItemBackgroundColor : ItemBackgroundColor;
        DrawRectangle(itemRect, bgColor);

        // 绘制项目内容
        DrawItemContent(itemRect, item, index, isSelected);
    }

    /// <summary>
    /// 绘制项目内容，可由派生类重写以自定义项目外观
    /// </summary>
    /// <param name="spriteBatch">精灵批处理</param>
    /// <param name="itemRect">项目矩形区域</param>
    /// <param name="item">项目对象</param>
    /// <param name="index">项目索引</param>
    /// <param name="isSelected">是否被选中</param>
    protected virtual void DrawItemContent(Rectangle itemRect, object item, int index, bool isSelected)
    {
        if (Font != null)
        {
            // 默认实现：显示项目的ToString()
            var text = item?.ToString() ?? string.Empty;

            // 计算文本位置（垂直居中）
            var textSize = Font.MeasureString(text);
            var textPosition = new Vector2(
                itemRect.X + ItemPadding,
                itemRect.Y + (itemRect.Height - textSize.Y) / 2
            );

            // 绘制文本
            var textColor = isSelected ? SelectedItemTextColor : ItemTextColor;
            SpriteBatch.DrawString(Font, text, textPosition, textColor);
        }
    }

    /// <summary>
    /// 绘制圆角矩形
    /// </summary>
    private void DrawRoundedRectangle(Vector2 position, Vector2 size, Color color, float borderWidth, float cornerRadius)
    {
        // 简化版圆角矩形绘制，实际项目可能需要更复杂的实现
        // 绘制上边框
        DrawRectangle(new Rectangle((int)(position.X + cornerRadius), (int)position.Y, (int)(size.X - 2 * cornerRadius), (int)borderWidth), color);

        // 绘制下边框
        DrawRectangle(new Rectangle((int)(position.X + cornerRadius), (int)(position.Y + size.Y - borderWidth), (int)(size.X - 2 * cornerRadius), (int)borderWidth), color);

        // 绘制左边框
        DrawRectangle(new Rectangle((int)position.X, (int)(position.Y + cornerRadius), (int)borderWidth, (int)(size.Y - 2 * cornerRadius)), color);

        // 绘制右边框
        DrawRectangle(new Rectangle((int)(position.X + size.X - borderWidth), (int)(position.Y + cornerRadius), (int)borderWidth, (int)(size.Y - 2 * cornerRadius)), color);
    }

    /// <summary>
    /// 绘制矩形
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
    /// 项目列表
    /// </summary>
    public List<object> Items { get; } = new();

    /// <summary>
    /// 选中项索引，-1表示未选中
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value)
            {
                _selectedIndex = value;
                OnSelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private int _selectedIndex = -1;

    /// <summary>
    /// 选中的项目
    /// </summary>
    public object SelectedItem
    {
        get => SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;
        set
        {
            var index = Items.IndexOf(value);
            SelectedIndex = index;
        }
    }

    /// <summary>
    /// 项目高度
    /// </summary>
    public float ItemHeight { get; set; } = 32f;

    /// <summary>
    /// 项目间距
    /// </summary>
    public float ItemSpacing { get; set; } = 2f;

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
    public float CornerRadius { get; set; } = 0f;

    /// <summary>
    /// 选中项背景颜色
    /// </summary>
    public Color SelectedItemBackgroundColor { get; set; } = new(120, 170, 220);

    /// <summary>
    /// 悬停项背景颜色
    /// </summary>
    public Color HoverItemBackgroundColor { get; set; } = new(180, 200, 230);

    /// <summary>
    /// 项目文本颜色
    /// </summary>
    public Color ItemTextColor { get; set; } = Color.Black;

    /// <summary>
    /// 选中项文本颜色
    /// </summary>
    public Color SelectedItemTextColor { get; set; } = Color.White;

    /// <summary>
    /// 项目背景颜色
    /// </summary>
    public Color ItemBackgroundColor { get; set; } = Color.WhiteSmoke;

    /// <summary>
    /// 项目字体
    /// </summary>
    public SpriteFont Font { get; set; }

    /// <summary>
    /// 项目内边距
    /// </summary>
    public float ItemPadding { get; set; } = 5f;

    /// <summary>
    /// 选中项变更事件
    /// </summary>
    public event EventHandler OnSelectedIndexChanged;

    /// <summary>
    /// 项目点击事件
    /// </summary>
    public event EventHandler<ItemClickEventArgs> OnItemClick;

    /// <summary>
    /// 项目双击事件
    /// </summary>
    public event EventHandler<ItemClickEventArgs> OnItemDoubleClick;

    #endregion

    #region IScrollable 接口实现

    /// <summary>
    /// 滚动偏移量
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
    public float ContentHeight => Items.Count > 0 ? Items.Count * (ItemHeight + ItemSpacing) - ItemSpacing : 0;

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

    #region 私有字段

    // 滚动偏移
    private float _scrollOffset;

    // 最大滚动偏移

    // 悬停项索引
    private int _hoverIndex = -1;

    // 上一帧鼠标状态
    private MouseState _previousMouseState;

    // 双击计时器
    private float _doubleClickTimer;

    // 双击索引
    private int _doubleClickIndex = -1;

    // 双击间隔（毫秒）
    private const float DoubleClickTime = 500f;

    // 1x1像素纹理，用于绘制矩形
    private Texture2D _pixelTexture;

    #endregion
}

/// <summary>
/// 项目点击事件参数
/// </summary>
public class ItemClickEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="index">项目索引</param>
    /// <param name="item">项目对象</param>
    public ItemClickEventArgs(int index, object item)
    {
        Index = index;
        Item = item;
    }

    /// <summary>
    /// 项目索引
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 项目对象
    /// </summary>
    public object Item { get; }
}