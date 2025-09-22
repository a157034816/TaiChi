using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TaiChi.MonoGame.UI.Controls;

/// <summary>
/// 数字输入框控件，只允许输入数字值
/// </summary>
public class NumberBox : TextBox
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public NumberBox()
    {
        // 设置默认值
        Value = 0f;

        // 监听文本变化事件
        OnTextChanged += NumberBox_OnTextChanged;
    }

    /// <summary>
    /// 处理文本变化
    /// </summary>
    private void NumberBox_OnTextChanged(object sender, EventArgs e)
    {
        // 验证文本是否符合数字格式
        var text = Text;

        // 只允许数字和小数点
        var regex = DecimalPlaces > 0
            ? new Regex(@"^-?\d*\.?\d*$")
            : new Regex(@"^-?\d*$");

        if (!regex.IsMatch(text))
        {
            // 移除不符合格式的字符
            var filtered = "";
            foreach (var c in text)
                if ((c >= '0' && c <= '9') || (DecimalPlaces > 0 && c == '.') || (c == '-' && filtered.Length == 0))
                    filtered += c;

            Text = filtered;
            return;
        }

        // 检查是否为空或只有负号或小数点
        if (string.IsNullOrEmpty(text) || text == "-" || text == ".") return;

        // 尝试解析为数字
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            // 检查是否超出范围
            if (parsedValue < MinValue)
            {
                Value = MinValue;
                return;
            }

            if (parsedValue > MaxValue)
            {
                Value = MaxValue;
                return;
            }

            // 触发值变化事件
            OnValueChanged?.Invoke(this, parsedValue);
        }
    }

    /// <summary>
    /// 更新输入框状态
    /// </summary>
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        // 获取键盘状态
        var keyboardState = Keyboard.GetState();

        // 检查是否获得焦点，以便处理上下键调整数值
        if (IsFocused && !IsReadOnly)
        {
            // 上键增加值
            if (keyboardState.IsKeyDown(Keys.Up) && _previousKeyboardState.IsKeyUp(Keys.Up)) Value += StepValue;

            // 下键减少值
            if (keyboardState.IsKeyDown(Keys.Down) && _previousKeyboardState.IsKeyUp(Keys.Down)) Value -= StepValue;
        }
    }

    #region 属性

    /// <summary>
    /// 当前数值
    /// </summary>
    public float Value
    {
        get
        {
            if (float.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)) return result;
            return 0f;
        }
        set
        {
            // 检查是否在最小/最大值范围内
            var clampedValue = Math.Clamp(value, MinValue, MaxValue);

            // 格式化为文本，考虑小数位数
            var formattedValue = clampedValue.ToString(
                DecimalPlaces > 0 ? $"F{DecimalPlaces}" : "F0",
                CultureInfo.InvariantCulture);

            Text = formattedValue;
        }
    }

    /// <summary>
    /// 最小值
    /// </summary>
    public float MinValue { get; set; } = float.MinValue;

    /// <summary>
    /// 最大值
    /// </summary>
    public float MaxValue { get; set; } = float.MaxValue;

    /// <summary>
    /// 步长值（上下键调整时使用）
    /// </summary>
    public float StepValue { get; set; } = 1f;

    /// <summary>
    /// 小数位数（0表示整数）
    /// </summary>
    public int DecimalPlaces { get; set; } = 0;

    /// <summary>
    /// 数值变化事件
    /// </summary>
    public event EventHandler<float> OnValueChanged;

    #endregion
}