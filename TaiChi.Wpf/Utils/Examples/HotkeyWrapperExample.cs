using System;
using System.Windows;
using System.Windows.Input;

namespace TaiChi.Wpf.Utils.Examples
{
    /// <summary>
    /// HotkeyWrapper 使用示例
    /// </summary>
    public class HotkeyWrapperExample
    {
        private readonly HotkeyWrapper _hotkeyWrapper;
        private Window _window;

        public HotkeyWrapperExample(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            
            // 创建并初始化热键包装器
            _hotkeyWrapper = new HotkeyWrapper(window);
            _hotkeyWrapper.Initialize();

            // 在窗口关闭时释放资源
            window.Closed += (s, e) => _hotkeyWrapper.Cleanup();
        }

        /// <summary>
        /// 注册示例热键
        /// </summary>
        public void RegisterExampleHotkeys()
        {
            try
            {
                // 注册 Ctrl+Shift+A 热键
                _hotkeyWrapper.RegisterHotkey(
                    "ShowMessage",
                    ModifierKeys.Control | ModifierKeys.Shift,
                    Key.A,
                    () => MessageBox.Show("热键 Ctrl+Shift+A 被触发!")
                );

                // 注册 Alt+F4 热键（覆盖默认行为）
                _hotkeyWrapper.RegisterHotkey(
                    "CustomExit",
                    ModifierKeys.Alt,
                    Key.F4,
                    () => {
                        var result = MessageBox.Show(
                            "确定要退出应用程序吗?",
                            "确认退出",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question
                        );
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            _window.Close();
                        }
                    }
                );

                // 注册 Win+S 热键
                _hotkeyWrapper.RegisterHotkey(
                    "SearchFunction",
                    ModifierKeys.Windows,
                    Key.S,
                    () => MessageBox.Show("搜索功能被触发!")
                );

                MessageBox.Show("热键注册成功！\n" +
                                "Ctrl+Shift+A: 显示消息\n" +
                                "Alt+F4: 自定义退出\n" +
                                "Win+S: 搜索功能");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"注册热键失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 切换热键示例
        /// </summary>
        public void SwitchHotkeyExample()
        {
            try
            {
                // 将 "ShowMessage" 热键从 Ctrl+Shift+A 切换为 Ctrl+Alt+M
                if (_hotkeyWrapper.IsHotkeyRegistered("ShowMessage"))
                {
                    bool success = _hotkeyWrapper.SwitchHotkey(
                        "ShowMessage",
                        ModifierKeys.Control | ModifierKeys.Alt,
                        Key.M
                    );

                    if (success)
                    {
                        MessageBox.Show("热键已切换: ShowMessage 现在是 Ctrl+Alt+M");
                    }
                    else
                    {
                        MessageBox.Show("热键切换失败!");
                    }
                }
                else
                {
                    MessageBox.Show("热键 'ShowMessage' 未注册");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换热键异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 注销热键示例
        /// </summary>
        public void UnregisterHotkeyExample()
        {
            // 注销指定名称的热键
            bool result = _hotkeyWrapper.UnregisterHotkey("CustomExit");
            
            if (result)
            {
                MessageBox.Show("成功注销 'CustomExit' 热键 (Alt+F4)");
            }
            else
            {
                MessageBox.Show("注销 'CustomExit' 热键失败，或热键不存在");
            }
        }

        /// <summary>
        /// 列出当前注册的所有热键
        /// </summary>
        public void ListRegisteredHotkeys()
        {
            var hotkeyNames = _hotkeyWrapper.GetRegisteredHotkeyNames();
            
            if (hotkeyNames.Count > 0)
            {
                MessageBox.Show($"已注册的热键: {string.Join(", ", hotkeyNames)}");
            }
            else
            {
                MessageBox.Show("当前没有注册的热键");
            }
        }

        /// <summary>
        /// 示例：在窗口按钮点击时注册/切换/注销热键
        /// </summary>
        public void ConfigureUIButtons()
        {
            // 注意：这个方法仅为示例，实际使用时需要根据UI结构调整
            
            /* 
            // 在实际代码中，您可以这样使用：
            
            registerButton.Click += (s, e) => RegisterExampleHotkeys();
            switchButton.Click += (s, e) => SwitchHotkeyExample();
            unregisterButton.Click += (s, e) => UnregisterHotkeyExample();
            listButton.Click += (s, e) => ListRegisteredHotkeys();
            
            // 或者直接在XAML中绑定：
            // <Button Content="注册热键" Click="RegisterButton_Click" />
            
            // 然后在后台代码中：
            private void RegisterButton_Click(object sender, RoutedEventArgs e)
            {
                _hotkeyExample.RegisterExampleHotkeys();
            }
            */
        }
    }
} 