using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TaiChi.MonoGame.UI.Basic;

/// <summary>
/// UI元素接口
/// </summary>
public interface IUIElement : IUpdateable, IDrawable
{
    /// <summary>
    /// 元素位置
    /// </summary>
    Vector2 Position { get; set; }

    /// <summary>
    /// 元素尺寸
    /// </summary>
    Vector2 Size { get; set; }

    /// <summary>
    /// 父控件
    /// </summary>
    IUIElement Parent { get; set; }

    /// <summary>
    /// 游戏
    /// </summary>
    Game Game { get; set; }

    /// <summary>
    /// 精灵批处理
    /// </summary>
    SpriteBatch SpriteBatch { get; set; }

    /// <summary>
    /// 测量元素所需的尺寸
    /// </summary>
    /// <param name="availableSize">可用尺寸</param>
    /// <returns>计算出的尺寸</returns>
    Vector2 Measure(Vector2 availableSize);

    /// <summary>
    /// 安排元素布局
    /// </summary>
    /// <param name="finalSize">最终尺寸</param>
    void Arrange(Vector2 finalSize);

    /// <summary>
    /// 设置游戏
    /// </summary>
    /// <param name="game">游戏</param>
    void SetGame(Game game);

    /// <summary>
    /// 设置精灵批处理
    /// </summary>
    /// <param name="spriteBatch">精灵批处理</param>
    void SetSpriteBatch(SpriteBatch spriteBatch);
}