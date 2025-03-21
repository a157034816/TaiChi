using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Threading;

namespace TaiChi.Wpf.Utils
{
    /// <summary>
    /// 系统热键管理器
    /// </summary>
    public static class SystemHotkeyManager
    {
        // Win32 API 导入
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // 修饰键常量
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        // 热键消息常量
        private const int WM_HOTKEY = 0x0312;

        // 线程同步锁对象
        private static readonly object _lockObject = new object();

        // 存储已注册的热键
        private static readonly Dictionary<int, Action> _registeredHotkeys = new Dictionary<int, Action>();
        private static IntPtr _windowHandle;
        private static HwndSource _source;
        private static int _currentId = 1;
        private static bool _isInitialized = false;
        private static Window _registeredWindow = null;

        /// <summary>
        /// 初始化热键管理器
        /// </summary>
        /// <param name="window">要关联热键的窗口</param>
        /// <exception cref="ArgumentNullException">window 为 null 时抛出</exception>
        public static void Initialize(Window window)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window), "窗口实例不能为 null");
            }

            lock (_lockObject)
            {
                // 防止重复初始化
                if (_isInitialized)
                {
                    return;
                }

                try
                {
                    _registeredWindow = window;
                    _windowHandle = new WindowInteropHelper(window).Handle;

                    if (_windowHandle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("无法获取窗口句柄");
                    }

                    _source = HwndSource.FromHwnd(_windowHandle);

                    if (_source == null)
                    {
                        throw new InvalidOperationException("无法创建 HwndSource");
                    }

                    _source.AddHook(HwndHook);
                    _isInitialized = true;

                    // 在窗口关闭时自动清理资源
                    window.Closed += (s, e) => Cleanup();
                }
                catch (Exception ex)
                {
                    // 初始化失败时清理可能已分配的资源
                    _source = null;
                    _windowHandle = IntPtr.Zero;
                    _registeredWindow = null;
                    throw new InvalidOperationException("初始化热键管理器失败", ex);
                }
            }
        }

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        /// <exception cref="InvalidOperationException">未初始化时抛出</exception>
        private static void CheckInitialized()
        {
            if (!_isInitialized || _windowHandle == IntPtr.Zero || _source == null)
            {
                throw new InvalidOperationException("必须先调用 Initialize 方法");
            }
        }

        /// <summary>
        /// 注册热键
        /// </summary>
        /// <param name="modifiers">修饰键</param>
        /// <param name="key">键码</param>
        /// <param name="action">热键触发时执行的操作</param>
        /// <returns>成功返回热键ID</returns>
        /// <exception cref="ArgumentNullException">action 为 null 时抛出</exception>
        /// <exception cref="InvalidOperationException">未初始化时抛出；热键已被其他程序注册；或注册热键失败</exception>
        public static int RegisterHotKey(uint modifiers, uint key, Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action), "热键操作不能为 null");
            }

            CheckInitialized();

            lock (_lockObject)
            {
                int id = _currentId++;

                try
                {
                    if (RegisterHotKey(_windowHandle, id, modifiers, key))
                    {
                        _registeredHotkeys[id] = action;
                        return id;
                    }
                    else
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        // 可能是热键已被注册
                        if (errorCode == 1409) // ERROR_HOTKEY_ALREADY_REGISTERED
                        {
                            throw new InvalidOperationException("热键已被其他程序注册");
                        }
                        throw new InvalidOperationException($"注册热键失败，错误代码: {errorCode}");
                    }
                }
                catch (Exception)
                {
                    // 注册过程中出现异常，确保计数器一致性
                    _currentId--;
                    throw;
                }
            }
        }

        /// <summary>
        /// 注销热键
        /// </summary>
        /// <param name="id">要注销的热键ID</param>
        /// <returns>成功返回true，失败返回false</returns>
        public static bool UnregisterHotKey(int id)
        {
            if (id <= 0)
            {
                return false; // 无效的ID
            }

            // 即使未初始化也尝试注销，避免资源泄漏
            if (!_isInitialized || _windowHandle == IntPtr.Zero)
            {
                return false;
            }

            lock (_lockObject)
            {
                if (_registeredHotkeys.ContainsKey(id))
                {
                    try
                    {
                        bool result = UnregisterHotKey(_windowHandle, id);
                        if (result)
                        {
                            _registeredHotkeys.Remove(id);
                        }
                        return result;
                    }
                    catch (Exception)
                    {
                        // 即使发生异常，也尝试从字典中移除
                        _registeredHotkeys.Remove(id);
                        return false;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 注销所有热键
        /// </summary>
        public static void UnregisterAllHotKeys()
        {
            if (!_isInitialized || _windowHandle == IntPtr.Zero)
            {
                return; // 未初始化，无需操作
            }

            lock (_lockObject)
            {
                List<int> idsToRemove = new List<int>(_registeredHotkeys.Keys);

                foreach (int id in idsToRemove)
                {
                    try
                    {
                        UnregisterHotKey(_windowHandle, id);
                    }
                    catch (Exception)
                    {
                        // 忽略单个热键注销失败，继续注销其他热键
                    }
                }

                _registeredHotkeys.Clear();
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {
            lock (_lockObject)
            {
                if (!_isInitialized)
                {
                    return;
                }

                try
                {
                    UnregisterAllHotKeys();

                    if (_source != null)
                    {
                        _source.RemoveHook(HwndHook);
                        _source = null;
                    }

                    _windowHandle = IntPtr.Zero;
                    _registeredWindow = null;
                    _isInitialized = false;
                }
                catch (Exception)
                {
                    // 确保即使出现异常也标记为未初始化
                    _isInitialized = false;
                    _source = null;
                    _windowHandle = IntPtr.Zero;
                    _registeredWindow = null;
                }
            }
        }

        /// <summary>
        /// 检查热键管理器是否已初始化
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                lock (_lockObject)
                {
                    return _isInitialized && _windowHandle != IntPtr.Zero && _source != null;
                }
            }
        }

        /// <summary>
        /// 窗口消息处理
        /// </summary>
        private static IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();

                // 使用线程安全的方式获取委托
                Action actionToInvoke = null;

                lock (_lockObject)
                {
                    if (_registeredHotkeys.TryGetValue(id, out Action action))
                    {
                        actionToInvoke = action;
                    }
                }

                // 在锁外执行委托，避免死锁
                if (actionToInvoke != null)
                {
                    try
                    {
                        actionToInvoke.Invoke();
                        handled = true;
                    }
                    catch (Exception ex)
                    {
                        // 记录异常或通知应用程序，但不要让异常影响消息循环
                        System.Diagnostics.Debug.WriteLine($"热键执行异常: {ex.Message}");
                    }
                }
            }

            return IntPtr.Zero;
        }
    }
}