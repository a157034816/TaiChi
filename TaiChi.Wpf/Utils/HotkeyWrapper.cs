using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace TaiChi.Wpf.Utils
{
    /// <summary>
    /// SystemHotkeyManager的包装器，通过热键名称管理热键
    /// </summary>
    public class HotkeyWrapper
    {
        // 存储已注册热键的ID与名称的映射关系
        private readonly Dictionary<string, int> _hotkeyNameToId = new Dictionary<string, int>();
        
        // 存储热键名称与键定义的映射，用于热键切换
        private readonly Dictionary<string, HotkeyDefinition> _hotkeyDefinitions = new Dictionary<string, HotkeyDefinition>();
        
        // 线程同步锁对象
        private readonly object _lockObject = new object();
        
        // 窗口引用
        private readonly Window _window;
        
        // 是否已初始化
        private bool _initialized = false;

        /// <summary>
        /// 创建热键包装器的新实例
        /// </summary>
        /// <param name="window">要关联热键的窗口</param>
        public HotkeyWrapper(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window), "窗口实例不能为 null");
        }

        /// <summary>
        /// 初始化热键包装器
        /// </summary>
        public void Initialize()
        {
            lock (_lockObject)
            {
                if (_initialized)
                {
                    return;
                }

                SystemHotkeyManager.Initialize(_window);
                _initialized = true;
            }
        }

        /// <summary>
        /// 热键定义结构，存储热键的修饰键和键码信息
        /// </summary>
        public class HotkeyDefinition
        {
            public uint Modifiers { get; }
            public uint Key { get; }
            public Action Action { get; }

            public HotkeyDefinition(uint modifiers, uint key, Action action)
            {
                Modifiers = modifiers;
                Key = key;
                Action = action ?? throw new ArgumentNullException(nameof(action), "热键操作不能为 null");
            }
        }

        /// <summary>
        /// 注册热键
        /// </summary>
        /// <param name="name">热键名称</param>
        /// <param name="modifiers">修饰键</param>
        /// <param name="key">键码</param>
        /// <param name="action">热键触发时执行的操作</param>
        /// <returns>是否成功注册</returns>
        /// <exception cref="ArgumentNullException">name 或 action 为 null 时抛出</exception>
        /// <exception cref="ArgumentException">name 为空字符串或已存在时抛出</exception>
        /// <exception cref="InvalidOperationException">未初始化时抛出</exception>
        public bool RegisterHotkey(string name, uint modifiers, uint key, Action action)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("热键名称不能为空", nameof(name));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action), "热键操作不能为 null");
            }

            lock (_lockObject)
            {
                CheckInitialized();

                // 检查同名热键是否已注册
                if (_hotkeyNameToId.ContainsKey(name))
                {
                    throw new ArgumentException($"已存在名为 '{name}' 的热键", nameof(name));
                }

                try
                {
                    // 创建热键定义并保存
                    var hotkeyDefinition = new HotkeyDefinition(modifiers, key, action);
                    _hotkeyDefinitions[name] = hotkeyDefinition;

                    // 注册热键
                    int id = SystemHotkeyManager.RegisterHotKey(modifiers, key, action);
                    
                    // 保存ID与名称的映射
                    _hotkeyNameToId[name] = id;
                    
                    return true;
                }
                catch (Exception ex)
                {
                    // 注册失败，清理已保存的定义
                    if (_hotkeyDefinitions.ContainsKey(name))
                    {
                        _hotkeyDefinitions.Remove(name);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"注册热键 '{name}' 失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 注册热键（使用System.Windows.Input.Key）
        /// </summary>
        /// <param name="name">热键名称</param>
        /// <param name="modifiers">修饰键</param>
        /// <param name="key">按键</param>
        /// <param name="action">热键触发时执行的操作</param>
        /// <returns>是否成功注册</returns>
        public bool RegisterHotkey(string name, ModifierKeys modifiers, Key key, Action action)
        {
            uint modifiersValue = 0;

            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                modifiersValue |= SystemHotkeyManager.MOD_ALT;
            
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                modifiersValue |= SystemHotkeyManager.MOD_CONTROL;
            
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                modifiersValue |= SystemHotkeyManager.MOD_SHIFT;
            
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                modifiersValue |= SystemHotkeyManager.MOD_WIN;

            // 添加MOD_NOREPEAT标志防止重复触发
            modifiersValue |= SystemHotkeyManager.MOD_NOREPEAT;

            // 将WPF Key转换为虚拟键码
            uint vkCode = (uint)KeyInterop.VirtualKeyFromKey(key);

            return RegisterHotkey(name, modifiersValue, vkCode, action);
        }

        /// <summary>
        /// 注销指定名称的热键
        /// </summary>
        /// <param name="name">热键名称</param>
        /// <returns>是否成功注销</returns>
        public bool UnregisterHotkey(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            lock (_lockObject)
            {
                // 未初始化或名称不存在
                if (!_initialized || !_hotkeyNameToId.TryGetValue(name, out int id))
                {
                    return false;
                }

                bool result = SystemHotkeyManager.UnregisterHotKey(id);
                
                // 无论成功与否，都从集合中移除
                _hotkeyNameToId.Remove(name);
                _hotkeyDefinitions.Remove(name);
                
                return result;
            }
        }

        /// <summary>
        /// 切换热键（更改热键的键位）
        /// </summary>
        /// <param name="name">热键名称</param>
        /// <param name="newModifiers">新的修饰键</param>
        /// <param name="newKey">新的键码</param>
        /// <returns>是否成功切换</returns>
        public bool SwitchHotkey(string name, uint newModifiers, uint newKey)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            lock (_lockObject)
            {
                CheckInitialized();

                // 检查热键是否存在
                if (!_hotkeyNameToId.TryGetValue(name, out int id) || 
                    !_hotkeyDefinitions.TryGetValue(name, out HotkeyDefinition oldDefinition))
                {
                    return false;
                }

                try
                {
                    // 先注销旧热键
                    bool unregistered = SystemHotkeyManager.UnregisterHotKey(id);
                    if (!unregistered)
                    {
                        return false;
                    }

                    // 注册新热键
                    int newId = SystemHotkeyManager.RegisterHotKey(newModifiers, newKey, oldDefinition.Action);

                    // 更新映射
                    _hotkeyNameToId[name] = newId;
                    _hotkeyDefinitions[name] = new HotkeyDefinition(newModifiers, newKey, oldDefinition.Action);

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"切换热键 '{name}' 失败: {ex.Message}");

                    // 切换失败，尝试恢复旧热键
                    try
                    {
                        int recoveredId = SystemHotkeyManager.RegisterHotKey(
                            oldDefinition.Modifiers, 
                            oldDefinition.Key, 
                            oldDefinition.Action);
                        _hotkeyNameToId[name] = recoveredId;
                    }
                    catch
                    {
                        // 恢复失败，从集合中移除
                        _hotkeyNameToId.Remove(name);
                        _hotkeyDefinitions.Remove(name);
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// 切换热键（使用System.Windows.Input.Key）
        /// </summary>
        /// <param name="name">热键名称</param>
        /// <param name="newModifiers">新的修饰键</param>
        /// <param name="newKey">新的按键</param>
        /// <returns>是否成功切换</returns>
        public bool SwitchHotkey(string name, ModifierKeys newModifiers, Key newKey)
        {
            uint modifiersValue = 0;

            if ((newModifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                modifiersValue |= SystemHotkeyManager.MOD_ALT;
            
            if ((newModifiers & ModifierKeys.Control) == ModifierKeys.Control)
                modifiersValue |= SystemHotkeyManager.MOD_CONTROL;
            
            if ((newModifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                modifiersValue |= SystemHotkeyManager.MOD_SHIFT;
            
            if ((newModifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                modifiersValue |= SystemHotkeyManager.MOD_WIN;

            // 添加MOD_NOREPEAT标志防止重复触发
            modifiersValue |= SystemHotkeyManager.MOD_NOREPEAT;

            // 将WPF Key转换为虚拟键码
            uint vkCode = (uint)KeyInterop.VirtualKeyFromKey(newKey);

            return SwitchHotkey(name, modifiersValue, vkCode);
        }

        /// <summary>
        /// 注销所有热键
        /// </summary>
        public void UnregisterAllHotkeys()
        {
            lock (_lockObject)
            {
                if (!_initialized)
                {
                    return;
                }

                // 使用SystemHotkeyManager注销所有热键
                SystemHotkeyManager.UnregisterAllHotKeys();

                // 清空集合
                _hotkeyNameToId.Clear();
                _hotkeyDefinitions.Clear();
            }
        }

        /// <summary>
        /// 检查热键是否已注册
        /// </summary>
        /// <param name="name">热键名称</param>
        /// <returns>是否已注册</returns>
        public bool IsHotkeyRegistered(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            lock (_lockObject)
            {
                return _hotkeyNameToId.ContainsKey(name);
            }
        }

        /// <summary>
        /// 获取已注册的热键名称列表
        /// </summary>
        /// <returns>热键名称列表</returns>
        public IReadOnlyList<string> GetRegisteredHotkeyNames()
        {
            lock (_lockObject)
            {
                return new List<string>(_hotkeyNameToId.Keys);
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            lock (_lockObject)
            {
                if (!_initialized)
                {
                    return;
                }

                // 注销所有热键
                UnregisterAllHotkeys();

                // 重置状态
                _initialized = false;
            }
        }

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        /// <exception cref="InvalidOperationException">未初始化时抛出</exception>
        private void CheckInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("必须先调用 Initialize 方法");
            }
        }
    }
} 