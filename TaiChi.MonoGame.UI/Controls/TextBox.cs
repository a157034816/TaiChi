using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TaiChi.MonoGame.UI.Basic;

namespace TaiChi.MonoGame.UI.Controls;

/// <summary>
/// 文本输入框控件，允许用户输入和编辑文本
/// </summary>
public class TextBox : UIElement
{
    /// <summary>
    /// 获取1x1像素纹理
    /// </summary>
    private Texture2D _pixelTexture;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TextBox()
    {
        BackgroundColor = Color.White;
    }

    /// <summary>
    /// 更新输入框状态
    /// </summary>
    /// <param name="gameTime">游戏时间</param>
    public override void Update(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        // 获取鼠标和键盘状态
        var mouseState = Mouse.GetState();
        var keyboardState = Keyboard.GetState();
        var mousePosition = ScreenToVirtualCoordinates(mouseState.Position);

        // 检查鼠标是否在输入框范围内
        var textBoxRect = new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);
        var isMouseOver = textBoxRect.Contains(mousePosition);

        // 处理鼠标点击
        if (mouseState.LeftButton == ButtonState.Pressed && isMouseOver)
        {
            IsFocused = true;

            if (Font != null && !string.IsNullOrEmpty(_text))
            {
                // 计算光标位置
                var textPosX = Position.X + Padding;
                for (var i = 0; i <= _text.Length; i++)
                {
                    var textPart = _text.Substring(0, i);
                    var size = Font.MeasureString(IsPasswordBox ? new string(PasswordChar, i) : textPart);

                    if (mousePosition.X < textPosX + size.X)
                    {
                        _cursorPosition = i;
                        break;
                    }

                    // 如果鼠标在最后一个字符之后
                    if (i == _text.Length) _cursorPosition = _text.Length;
                }
            }
            else
            {
                _cursorPosition = 0;
            }

            // 重置光标闪烁
            _isCursorVisible = true;
            _cursorBlinkTimer = 0;
        }
        else if (mouseState.LeftButton == ButtonState.Pressed && !isMouseOver)
        {
            IsFocused = false;
        }

        // 处理键盘输入
        if (IsFocused && !IsReadOnly) HandleKeyboardInput(keyboardState, gameTime);

        // 更新光标闪烁
        if (IsFocused)
        {
            _cursorBlinkTimer += (float)gameTime.ElapsedGameTime.TotalMilliseconds;

            if (_cursorBlinkTimer >= CursorBlinkInterval)
            {
                _isCursorVisible = !_isCursorVisible;
                _cursorBlinkTimer = 0;
            }
        }

        _previousKeyboardState = keyboardState;
    }

    /// <summary>
    /// 处理键盘输入
    /// </summary>
    private void HandleKeyboardInput(KeyboardState keyboardState, GameTime gameTime)
    {
        // 首先检查特殊键
        if (IsKeyPressed(keyboardState, Keys.Enter))
        {
            if (IsMultiLine)
            {
                // 在多行模式下，插入换行符
                if (MaxLength <= 0 || _text.Length < MaxLength)
                {
                    if (_selectionStart.HasValue) DeleteSelectedText();

                    Text = _text.Insert(_cursorPosition, Environment.NewLine);
                    _cursorPosition += Environment.NewLine.Length;

                    // 重置光标闪烁
                    _isCursorVisible = true;
                    _cursorBlinkTimer = 0;
                }
            }
            else
            {
                // 单行模式下触发提交事件
                OnSubmit?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        // 处理退格键
        if (IsKeyPressedOrRepeating(keyboardState, Keys.Back, gameTime))
        {
            if (_cursorPosition > 0)
            {
                if (_selectionStart.HasValue)
                {
                    DeleteSelectedText();
                }
                else
                {
                    Text = _text.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
                }
            }

            return;
        }

        // 处理删除键
        if (IsKeyPressedOrRepeating(keyboardState, Keys.Delete, gameTime))
        {
            if (_selectionStart.HasValue)
                DeleteSelectedText();
            else if (_cursorPosition < _text.Length) Text = _text.Remove(_cursorPosition, 1);
            return;
        }

        // 处理左键
        if (IsKeyPressedOrRepeating(keyboardState, Keys.Left, gameTime))
        {
            if (_cursorPosition > 0)
            {
                _cursorPosition--;

                // 按住Shift键时选择文本
                if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
                {
                    if (!_selectionStart.HasValue) _selectionStart = _cursorPosition + 1;
                }
                else
                {
                    _selectionStart = null;
                }
            }

            return;
        }

        // 处理右键
        if (IsKeyPressedOrRepeating(keyboardState, Keys.Right, gameTime))
        {
            if (_cursorPosition < _text.Length)
            {
                _cursorPosition++;

                // 按住Shift键时选择文本
                if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
                {
                    if (!_selectionStart.HasValue) _selectionStart = _cursorPosition - 1;
                }
                else
                {
                    _selectionStart = null;
                }
            }

            return;
        }

        // 处理上键（多行模式）
        if (IsMultiLine && IsKeyPressedOrRepeating(keyboardState, Keys.Up, gameTime))
        {
            MoveCursorVertically(-1, keyboardState);
            return;
        }

        // 处理下键（多行模式）
        if (IsMultiLine && IsKeyPressedOrRepeating(keyboardState, Keys.Down, gameTime))
        {
            MoveCursorVertically(1, keyboardState);
            return;
        }

        // 处理Home键
        if (IsKeyPressed(keyboardState, Keys.Home))
        {
            if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
            {
                if (!_selectionStart.HasValue) _selectionStart = _cursorPosition;
            }
            else
            {
                _selectionStart = null;
            }

            _cursorPosition = 0;
            return;
        }

        // 处理End键
        if (IsKeyPressed(keyboardState, Keys.End))
        {
            if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
            {
                if (!_selectionStart.HasValue) _selectionStart = _cursorPosition;
            }
            else
            {
                _selectionStart = null;
            }

            _cursorPosition = _text.Length;
            return;
        }

        // 处理普通字符输入
        foreach (var key in keyboardState.GetPressedKeys())
        {
            if (_previousKeyboardState.IsKeyDown(key))
                continue;

            var ch = GetCharFromKey(key, keyboardState);
            if (ch.HasValue)
            {
                // 检查最大长度限制
                if (MaxLength > 0 && _text.Length >= MaxLength)
                    continue;

                if (_selectionStart.HasValue) DeleteSelectedText();

                Text = _text.Insert(_cursorPosition, ch.Value.ToString());
                _cursorPosition++;

                // 重置光标闪烁
                _isCursorVisible = true;
                _cursorBlinkTimer = 0;
            }
        }
    }

    /// <summary>
    /// 删除选中的文本
    /// </summary>
    private void DeleteSelectedText()
    {
        if (!_selectionStart.HasValue)
            return;

        var start = Math.Min(_selectionStart.Value, _cursorPosition);
        var length = Math.Abs(_selectionStart.Value - _cursorPosition);

        if (length > 0)
        {
            Text = _text.Remove(start, length);
            _cursorPosition = start;
            _selectionStart = null;
        }
    }

    /// <summary>
    /// 检查键是否被按下（单次触发）
    /// </summary>
    private bool IsKeyPressed(KeyboardState keyboardState, Keys key)
    {
        return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    /// <summary>
    /// 检查键是否被按下或正在重复（带延迟和重复率）
    /// </summary>
    private bool IsKeyPressedOrRepeating(KeyboardState keyboardState, Keys key, GameTime gameTime)
    {
        if (keyboardState.IsKeyDown(key))
        {
            if (!_previousKeyboardState.IsKeyDown(key))
            {
                // 键刚刚被按下
                _currentRepeatingKey = key;
                _keyRepeatTimer = 0;
                return true;
            }

            if (_currentRepeatingKey == key)
            {
                // 键正在被持续按下
                _keyRepeatTimer += (float)gameTime.ElapsedGameTime.TotalMilliseconds;

                if (_keyRepeatTimer > KeyRepeatDelay)
                {
                    var timeAfterDelay = _keyRepeatTimer - KeyRepeatDelay;
                    if (timeAfterDelay % KeyRepeatInterval < (float)gameTime.ElapsedGameTime.TotalMilliseconds)
                        // 达到重复间隔
                        return true;
                }
            }
        }
        else if (_currentRepeatingKey == key)
        {
            // 按键被释放
            _currentRepeatingKey = null;
        }

        return false;
    }

    /// <summary>
    /// 从按键获取对应的字符
    /// </summary>
    private char? GetCharFromKey(Keys key, KeyboardState keyboardState)
    {
        var shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

        // 字母键
        if (key >= Keys.A && key <= Keys.Z)
        {
            var ch = (char)('a' + (key - Keys.A));
            return shift ? char.ToUpper(ch) : ch;
        }

        // 数字键
        if (key >= Keys.D0 && key <= Keys.D9 && !shift) return (char)('0' + (key - Keys.D0));

        // 数字键 + Shift
        if (key >= Keys.D0 && key <= Keys.D9 && shift)
        {
            char[] shiftNumbers = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
            return shiftNumbers[key - Keys.D0];
        }

        // 小键盘数字
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));

        // 空格
        if (key == Keys.Space) return ' ';

        // 特殊符号
        switch (key)
        {
            case Keys.OemPeriod:
                return shift ? '>' : '.';
            case Keys.OemComma:
                return shift ? '<' : ',';
            case Keys.OemSemicolon:
                return shift ? ':' : ';';
            case Keys.OemQuotes:
                return shift ? '"' : '\'';
            case Keys.OemQuestion:
                return shift ? '?' : '/';
            case Keys.OemPlus:
                return shift ? '+' : '=';
            case Keys.OemMinus:
                return shift ? '_' : '-';
            case Keys.OemOpenBrackets:
                return shift ? '{' : '[';
            case Keys.OemCloseBrackets:
                return shift ? '}' : ']';
            case Keys.OemPipe:
                return shift ? '|' : '\\';
            case Keys.OemTilde:
                return shift ? '~' : '`';
        }

        return null;
    }

    /// <summary>
    /// 绘制输入框
    /// </summary>
    /// <param name="spriteBatch">精灵批处理</param>
    public override void Draw(GameTime gameTime)
    {
        if (!IsVisible)
            return;

        // 绘制背景
        var bgColor = BackgroundColor ?? Color.White;
        DrawRectangle(new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y), bgColor);

        // 绘制边框
        var borderCol = IsFocused ? FocusedBorderColor : BorderColor;
        if (BorderWidth > 0)
        {
            if (CornerRadius > 0)
            {
                // 绘制圆角边框
                DrawRoundedRectangle(Position, Size, borderCol, BorderWidth, CornerRadius);
            }
            else
            {
                // 绘制矩形边框
                DrawRectangle(new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)BorderWidth), borderCol);
                DrawRectangle(new Rectangle((int)Position.X, (int)(Position.Y + Size.Y - BorderWidth), (int)Size.X, (int)BorderWidth), borderCol);
                DrawRectangle(new Rectangle((int)Position.X, (int)Position.Y, (int)BorderWidth, (int)Size.Y), borderCol);
                DrawRectangle(new Rectangle((int)(Position.X + Size.X - BorderWidth), (int)Position.Y, (int)BorderWidth, (int)Size.Y), borderCol);
            }
        }

        // 计算文本绘制位置
        var textPosition = new Vector2(Position.X + Padding, Position.Y + Padding);

        if (!IsMultiLine)
            // 单行模式下，垂直居中
            textPosition.Y = Position.Y + (Size.Y - (Font?.MeasureString("A").Y ?? 0)) / 2;

        // 绘制文本或占位符
        if (Font != null)
        {
            if (string.IsNullOrEmpty(_text) && !string.IsNullOrEmpty(PlaceholderText))
            {
                // 绘制占位符文本
                SpriteBatch.DrawString(Font, PlaceholderText, textPosition, PlaceholderColor);
            }
            else
            {
                // 获取显示文本（考虑密码框）
                var displayText = IsPasswordBox ? new string(PasswordChar, _text.Length) : _text;

                if (IsMultiLine)
                {
                    // 多行模式下，按行绘制
                    DrawMultiLineText(displayText, textPosition);
                }
                else
                {
                    // 如果有选中文本，绘制选择背景
                    if (_selectionStart.HasValue && IsFocused)
                    {
                        var start = Math.Min(_selectionStart.Value, _cursorPosition);
                        var end = Math.Max(_selectionStart.Value, _cursorPosition);

                        if (start < end)
                        {
                            var beforeSelection = displayText.Substring(0, start);
                            var selection = displayText.Substring(start, end - start);

                            var beforeSize = Font.MeasureString(beforeSelection);
                            var selectionSize = Font.MeasureString(selection);

                            // 绘制选择背景
                            var selectionRect = new Rectangle(
                                (int)(textPosition.X + beforeSize.X),
                                (int)textPosition.Y,
                                (int)selectionSize.X,
                                (int)Font.MeasureString("A").Y
                            );

                            DrawRectangle(selectionRect, new Color(120, 170, 220, 150));
                        }
                    }

                    // 绘制文本
                    SpriteBatch.DrawString(Font, displayText, textPosition, TextColor);
                }
            }
        }

        // 绘制光标
        if (IsFocused && _isCursorVisible)
            if (Font != null)
            {
                var cursorPos = GetCursorPosition(textPosition);

                // 绘制一个垂直线作为光标
                var cursorHeight = Font.MeasureString("A").Y;
                SpriteBatch.Draw(
                    GetPixelTexture(),
                    new Rectangle((int)cursorPos.X, (int)cursorPos.Y, 1, (int)cursorHeight),
                    TextColor
                );
            }
    }

    /// <summary>
    /// 绘制多行文本
    /// </summary>
    private void DrawMultiLineText(string text, Vector2 position)
    {
        if (string.IsNullOrEmpty(text) || Font == null)
            return;

        var availableWidth = Size.X - Padding * 2;
        var lineHeight = Font.MeasureString("A").Y;

        if (WordWrap)
        {
            // 使用自动换行处理
            var wrappedLines = new List<string>();
            var currentTextPosition = 0;

            // 首先按照手动换行符分割文本
            var manualLines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            foreach (var line in manualLines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    wrappedLines.Add(string.Empty);
                    currentTextPosition += Environment.NewLine.Length;
                    continue;
                }

                // 处理每一行的自动换行
                WrapTextLine(line, availableWidth, wrappedLines);
                currentTextPosition += line.Length + Environment.NewLine.Length;
            }

            // 绘制处理后的行
            DrawWrappedLines(wrappedLines, position, lineHeight);
        }
        else
        {
            // 原有的按行绘制逻辑
            var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            var currentPos = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var linePos = new Vector2(position.X, position.Y + i * lineHeight);

                // 如果有选中文本，绘制选择背景
                if (_selectionStart.HasValue && IsFocused)
                {
                    var selStart = Math.Min(_selectionStart.Value, _cursorPosition);
                    var selEnd = Math.Max(_selectionStart.Value, _cursorPosition);

                    // 计算当前行选择部分
                    var lineStart = currentPos;
                    var lineEnd = currentPos + lines[i].Length;

                    // 当前行包含选择的一部分
                    if (!(selEnd <= lineStart || selStart >= lineEnd))
                    {
                        var lineSelStart = Math.Max(0, selStart - lineStart);
                        var lineSelEnd = Math.Min(lines[i].Length, selEnd - lineStart);

                        if (lineSelStart < lineSelEnd)
                        {
                            var beforeSelection = lines[i].Substring(0, lineSelStart);
                            var selection = lines[i].Substring(lineSelStart, lineSelEnd - lineSelStart);

                            var beforeSize = Font.MeasureString(beforeSelection);
                            var selectionSize = Font.MeasureString(selection);

                            // 绘制选择背景
                            var selectionRect = new Rectangle(
                                (int)(linePos.X + beforeSize.X),
                                (int)linePos.Y,
                                (int)selectionSize.X,
                                (int)lineHeight
                            );

                            DrawRectangle(selectionRect, new Color(120, 170, 220, 150));
                        }
                    }
                }

                // 绘制当前行文本
                SpriteBatch.DrawString(Font, lines[i], linePos, TextColor);

                // 更新当前位置
                currentPos += lines[i].Length + Environment.NewLine.Length;
            }
        }
    }

    /// <summary>
    /// 对单行文本进行自动换行处理
    /// </summary>
    private void WrapTextLine(string line, float maxWidth, List<string> wrappedLines)
    {
        if (string.IsNullOrEmpty(line))
        {
            wrappedLines.Add(string.Empty);
            return;
        }

        var words = line.Split(' ');
        var currentLine = new StringBuilder();

        foreach (var word in words)
            // 处理特别长的单词
            if (Font.MeasureString(word).X > maxWidth)
            {
                // 如果当前行已有内容，先添加当前行
                if (currentLine.Length > 0)
                {
                    wrappedLines.Add(currentLine.ToString());
                    currentLine.Clear();
                }

                // 对长单词进行字符级别的换行
                SplitLongWord(word, maxWidth, wrappedLines);
            }
            else
            {
                // 检查添加这个词是否会超过可用宽度
                var testLine = currentLine.Length > 0
                    ? currentLine + " " + word
                    : word;

                if (Font.MeasureString(testLine).X <= maxWidth)
                {
                    // 可以添加到当前行
                    if (currentLine.Length > 0) currentLine.Append(" ");
                    currentLine.Append(word);
                }
                else
                {
                    // 需要换行
                    if (currentLine.Length > 0)
                    {
                        wrappedLines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }

                    currentLine.Append(word);
                }
            }

        // 添加最后一行
        if (currentLine.Length > 0) wrappedLines.Add(currentLine.ToString());
    }

    /// <summary>
    /// 将长单词分割成多行
    /// </summary>
    private void SplitLongWord(string word, float maxWidth, List<string> wrappedLines)
    {
        var currentPart = new StringBuilder();

        for (var i = 0; i < word.Length; i++)
        {
            currentPart.Append(word[i]);

            // 检查是否需要换行
            if (Font.MeasureString(currentPart.ToString()).X > maxWidth)
            {
                // 移除最后一个字符（放到下一行）
                if (currentPart.Length > 1)
                {
                    currentPart.Remove(currentPart.Length - 1, 1);
                    wrappedLines.Add(currentPart.ToString());
                    currentPart.Clear();
                    currentPart.Append(word[i]);
                }
                else
                {
                    // 即使单个字符也超宽，只能保留
                    wrappedLines.Add(currentPart.ToString());
                    currentPart.Clear();
                }
            }
        }

        // 添加最后一部分
        if (currentPart.Length > 0) wrappedLines.Add(currentPart.ToString());
    }

    /// <summary>
    /// 绘制已经处理好自动换行的文本
    /// </summary>
    private void DrawWrappedLines(List<string> wrappedLines, Vector2 position, float lineHeight)
    {
        // 转换手动换行和自动换行后的文本位置映射
        var positionMapping = BuildPositionMapping(wrappedLines);

        for (var i = 0; i < wrappedLines.Count; i++)
        {
            var linePos = new Vector2(position.X, position.Y + i * lineHeight);

            // 绘制当前行
            SpriteBatch.DrawString(Font, wrappedLines[i], linePos, TextColor);

            // 如果有选中文本，绘制选择背景
            if (_selectionStart.HasValue && IsFocused)
            {
                var selStart = Math.Min(_selectionStart.Value, _cursorPosition);
                var selEnd = Math.Max(_selectionStart.Value, _cursorPosition);

                // 确定当前行覆盖的原始文本范围
                var lineRange = positionMapping.ContainsKey(i) ? positionMapping[i] : null;

                if (lineRange != null)
                {
                    var lineStart = lineRange.Item1;
                    var lineEnd = lineRange.Item2;

                    // 当前行包含选择的一部分
                    if (!(selEnd <= lineStart || selStart >= lineEnd))
                    {
                        var lineSelStart = Math.Max(0, selStart - lineStart);
                        var lineSelEnd = Math.Min(wrappedLines[i].Length, selEnd - lineStart);

                        if (lineSelStart < lineSelEnd && lineSelStart < wrappedLines[i].Length)
                        {
                            lineSelStart = Math.Min(lineSelStart, wrappedLines[i].Length);
                            lineSelEnd = Math.Min(lineSelEnd, wrappedLines[i].Length);

                            if (lineSelStart < lineSelEnd)
                            {
                                var beforeSelection = wrappedLines[i].Substring(0, lineSelStart);
                                var selection = wrappedLines[i].Substring(lineSelStart, lineSelEnd - lineSelStart);

                                var beforeSize = Font.MeasureString(beforeSelection);
                                var selectionSize = Font.MeasureString(selection);

                                // 绘制选择背景
                                var selectionRect = new Rectangle(
                                    (int)(linePos.X + beforeSize.X),
                                    (int)linePos.Y,
                                    (int)selectionSize.X,
                                    (int)lineHeight
                                );

                                DrawRectangle(selectionRect, new Color(120, 170, 220, 150));
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 构建自动换行后的行与原始文本位置之间的映射
    /// </summary>
    private Dictionary<int, Tuple<int, int>> BuildPositionMapping(List<string> wrappedLines)
    {
        // 暂时使用简化的映射，假定文本布局是固定的
        // 实际项目中可能需要更复杂的映射逻辑
        var mapping = new Dictionary<int, Tuple<int, int>>();

        var currentOriginalPos = 0;

        for (var i = 0; i < wrappedLines.Count; i++)
        {
            var startPos = currentOriginalPos;
            currentOriginalPos += wrappedLines[i].Length;

            mapping[i] = new Tuple<int, int>(startPos, currentOriginalPos);

            // 如果不是最后一行，还要考虑空格
            if (i < wrappedLines.Count - 1 && wrappedLines[i].Length > 0) currentOriginalPos++; // 为单词间的空格留位置
        }

        return mapping;
    }

    /// <summary>
    /// 获取光标位置
    /// </summary>
    private Vector2 GetCursorPosition(Vector2 textPosition)
    {
        var cursorPos = textPosition;

        if (IsMultiLine)
        {
            // 多行模式下，计算光标在哪一行
            if (!string.IsNullOrEmpty(_text))
            {
                if (WordWrap)
                {
                    // 获取自动换行后的行
                    var wrappedLines = new List<string>();
                    var manualLines = _text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    var availableWidth = Size.X - Padding * 2;

                    foreach (var line in manualLines)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            wrappedLines.Add(string.Empty);
                            continue;
                        }

                        WrapTextLine(line, availableWidth, wrappedLines);
                    }

                    // 构建映射
                    var mapping = BuildPositionMapping(wrappedLines);

                    // 查找光标所在的行
                    for (var i = 0; i < wrappedLines.Count; i++)
                        if (mapping.ContainsKey(i))
                        {
                            var range = mapping[i];

                            if (_cursorPosition >= range.Item1 && _cursorPosition <= range.Item2)
                            {
                                // 光标在当前行
                                var relativePos = _cursorPosition - range.Item1;

                                // 确保不超出当前行长度
                                relativePos = Math.Min(relativePos, wrappedLines[i].Length);

                                var textBeforeCursor = IsPasswordBox ? new string(PasswordChar, relativePos) : wrappedLines[i].Substring(0, relativePos);

                                cursorPos.X = textPosition.X + Font.MeasureString(textBeforeCursor).X;
                                cursorPos.Y = textPosition.Y + i * Font.MeasureString("A").Y;
                                break;
                            }
                        }
                }
                else
                {
                    // 原有的计算逻辑
                    var lines = _text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    var currentPos = 0;

                    for (var i = 0; i < lines.Length; i++)
                    {
                        var lineLength = lines[i].Length;

                        if (_cursorPosition <= currentPos + lineLength)
                        {
                            // 光标在当前行
                            var textBeforeCursor = IsPasswordBox ? new string(PasswordChar, _cursorPosition - currentPos) : lines[i].Substring(0, _cursorPosition - currentPos);

                            cursorPos.X = textPosition.X + Font.MeasureString(textBeforeCursor).X;
                            cursorPos.Y = textPosition.Y + i * Font.MeasureString("A").Y;
                            break;
                        }

                        currentPos += lineLength + Environment.NewLine.Length;
                    }
                }
            }
        }
        else
        {
            // 单行模式下的光标位置
            if (!string.IsNullOrEmpty(_text))
            {
                var textBeforeCursor = IsPasswordBox ? new string(PasswordChar, _cursorPosition) : _text.Substring(0, _cursorPosition);

                cursorPos.X += Font.MeasureString(textBeforeCursor).X;
            }
        }

        return cursorPos;
    }

    /// <summary>
    /// 垂直移动光标（上下键）
    /// </summary>
    private void MoveCursorVertically(int direction, KeyboardState keyboardState)
    {
        if (string.IsNullOrEmpty(_text) || !IsMultiLine || Font == null)
            return;

        var lines = _text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        // 查找当前光标所在行
        var currentPos = 0;
        var currentLine = 0;
        var positionInCurrentLine = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineLength = lines[i].Length;

            if (_cursorPosition <= currentPos + lineLength)
            {
                // 光标在当前行
                currentLine = i;
                positionInCurrentLine = _cursorPosition - currentPos;
                break;
            }

            currentPos += lineLength + Environment.NewLine.Length;
        }

        // 计算目标行
        var targetLine = currentLine + direction;

        if (targetLine >= 0 && targetLine < lines.Length)
        {
            // 计算目标行中光标的水平位置
            var targetPosition = positionInCurrentLine;

            // 确保不超过目标行长度
            if (targetPosition > lines[targetLine].Length) targetPosition = lines[targetLine].Length;

            // 计算新的光标位置
            var newCursorPosition = 0;

            for (var i = 0; i < targetLine; i++) newCursorPosition += lines[i].Length + Environment.NewLine.Length;

            newCursorPosition += targetPosition;

            // 更新光标位置
            if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
            {
                if (!_selectionStart.HasValue) _selectionStart = _cursorPosition;
            }
            else
            {
                _selectionStart = null;
            }

            _cursorPosition = newCursorPosition;
        }
    }

    /// <summary>
    /// 绘制圆角矩形
    /// </summary>
    private void DrawRoundedRectangle(Vector2 position, Vector2 size, Color color, float borderWidth, float cornerRadius)
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
    private void DrawCircle(Vector2 center, float radius, Color color, float borderWidth)
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
    private void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        var edge = end - start;
        var angle = (float)Math.Atan2(edge.Y, edge.X);
        var length = edge.Length();

        SpriteBatch.Draw(
            GetPixelTexture(),
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, width),
            SpriteEffects.None,
            0
        );
    }

    private Texture2D GetPixelTexture()
    {
        if (_pixelTexture == null)
        {
            _pixelTexture = new Texture2D(SpriteBatch.GraphicsDevice, 1, 1);
            var colorData = new Color[1];
            colorData[0] = Color.White;
            _pixelTexture.SetData(colorData);
        }

        return _pixelTexture;
    }

    #region 属性

    /// <summary>
    /// 文本内容
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                _cursorPosition = Math.Min(_cursorPosition, _text.Length);
                OnTextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private string _text = string.Empty;

    /// <summary>
    /// 占位符文本，在文本框为空时显示
    /// </summary>
    public string PlaceholderText { get; set; } = string.Empty;

    /// <summary>
    /// 文本颜色
    /// </summary>
    public Color TextColor { get; set; } = Color.Black;

    /// <summary>
    /// 占位符文本颜色
    /// </summary>
    public Color PlaceholderColor { get; set; } = new(180, 180, 180);

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
    /// 获得焦点时的边框颜色
    /// </summary>
    public Color FocusedBorderColor { get; set; } = Color.Blue;

    /// <summary>
    /// 文本字体
    /// </summary>
    public SpriteFont Font { get; set; }

    /// <summary>
    /// 内边距
    /// </summary>
    public float Padding { get; set; } = 5f;

    /// <summary>
    /// 最大文本长度，0表示不限制
    /// </summary>
    public int MaxLength { get; set; } = 0;

    /// <summary>
    /// 是否为密码输入框
    /// </summary>
    public bool IsPasswordBox { get; set; } = false;

    /// <summary>
    /// 密码字符
    /// </summary>
    public char PasswordChar { get; set; } = '*';

    /// <summary>
    /// 是否获得焦点
    /// </summary>
    public bool IsFocused { get; set; }

    /// <summary>
    /// 是否只读
    /// </summary>
    public bool IsReadOnly { get; set; } = false;

    /// <summary>
    /// 是否允许多行文本
    /// </summary>
    public bool IsMultiLine { get; set; } = false;

    /// <summary>
    /// 是否自动换行（仅在多行模式下有效）
    /// </summary>
    public bool WordWrap { get; set; } = true;

    /// <summary>
    /// 光标闪烁间隔（毫秒）
    /// </summary>
    public float CursorBlinkInterval { get; set; } = 500f;

    /// <summary>
    /// 文本改变事件
    /// </summary>
    public event EventHandler OnTextChanged;

    /// <summary>
    /// 提交事件（按下回车）
    /// </summary>
    public event EventHandler OnSubmit;

    #endregion

    #region 私有字段

    // 光标位置（字符索引）
    private int _cursorPosition;

    // 光标是否可见（闪烁效果）
    private bool _isCursorVisible = true;

    // 光标闪烁计时器
    private float _cursorBlinkTimer;

    // 按键重复延迟计时器
    private float _keyRepeatTimer;

    // 按键重复开始延迟（毫秒）
    private const float KeyRepeatDelay = 500f;

    // 按键重复间隔（毫秒）
    private const float KeyRepeatInterval = 50f;

    // 当前重复按键
    private Keys? _currentRepeatingKey;

    // 上一帧键盘状态
    protected KeyboardState _previousKeyboardState;

    // 选择起始位置（用于文本选择）
    private int? _selectionStart;

    #endregion
}