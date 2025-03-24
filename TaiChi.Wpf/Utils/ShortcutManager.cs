using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

// WPF工具包中的快捷键管理器
namespace TaiChi.Wpf.Utils
{
    /// <summary>
    /// 提供快捷键注册和管理功能的静态类
    /// </summary>
    public static class ShortcutManager
    {
        // 使用弱引用跟踪窗口及其快捷键配置
        private static readonly ConditionalWeakTable<DependencyObject, ShortcutRegistry> Registries =
            new ConditionalWeakTable<DependencyObject, ShortcutRegistry>();

        /// <summary>
        /// 注册快捷键
        /// </summary>
        /// <param name="target">要注册快捷键的目标对象</param>
        /// <param name="gesture">快捷键的手势</param>
        /// <param name="execute">执行快捷键操作的委托</param>
        /// <param name="canExecute">确定快捷键是否可以执行的委托</param>
        public static void RegisterShortcut(DependencyObject target, KeyGesture gesture, Action execute,
            Func<bool> canExecute = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var registry = Registries.GetValue(target, _ => new ShortcutRegistry(target));
            registry.AddShortcut(gesture, execute, canExecute);
        }

        /// <summary>
        /// 移除指定快捷键
        /// </summary>
        /// <param name="target">要移除快捷键的目标对象</param>
        /// <param name="gesture">要移除的快捷键手势</param>
        public static void UnregisterShortcut(DependencyObject target, KeyGesture gesture)
        {
            if (Registries.TryGetValue(target, out var registry))
            {
                registry.RemoveShortcut(gesture);
            }
        }

        /// <summary>
        /// 获取窗口所有快捷键
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <returns>目标对象的所有快捷键手势</returns>
        public static IEnumerable<KeyGesture> GetShortcuts(DependencyObject target)
        {
            return Registries.TryGetValue(target, out var registry)
                ? registry.GetAllShortcuts()
                : Array.Empty<KeyGesture>();
        }

        // 窗口快捷键配置容器
        private class ShortcutRegistry
        {
            private readonly DependencyObject _target;

            // 存储快捷键与其绑定的命令
            private readonly Dictionary<KeyGesture, CommandBinding> _bindings =
                new Dictionary<KeyGesture, CommandBinding>();

            // 构造函数，根据目标对象初始化快捷键注册表
            public ShortcutRegistry(DependencyObject target)
            {
                _target = target;
                if (target is Window window)
                {
                    window.Closed += OnTargetClosed;
                }
                else if (target is Page page)
                {
                    page.Unloaded += OnTargetClosed;
                }
            }

            // 当窗口关闭时自动清理资源
            private void OnTargetClosed(object sender, EventArgs e)
            {
                if (_target is Window window)
                {
                    window.Closed -= OnTargetClosed;
                }
                else if (_target is Page page)
                {
                    page.Unloaded -= OnTargetClosed;
                }

                _bindings.Clear();
            }

            /// <summary>
            /// 添加快捷键
            /// </summary>
            /// <param name="gesture">快捷键手势</param>
            /// <param name="execute">执行快捷键操作的委托</param>
            /// <param name="canExecute">确定快捷键是否可以执行的委托</param>
            public void AddShortcut(KeyGesture gesture, Action execute, Func<bool> canExecute)
            {
                if (_bindings.ContainsKey(gesture)) return;

                var command = new RoutedCommand();
                command.InputGestures.Add(gesture);

                var binding = new CommandBinding(command,
                    (_, e) => execute(),
                    (_, e) => e.CanExecute = canExecute?.Invoke() ?? true);

                GetCommandBindings().Add(binding);
                _bindings.Add(gesture, binding);
            }

            /// <summary>
            /// 移除快捷键
            /// </summary>
            /// <param name="gesture">要移除的快捷键手势</param>
            public void RemoveShortcut(KeyGesture gesture)
            {
                if (!_bindings.TryGetValue(gesture, out var binding)) return;

                GetCommandBindings().Remove(binding);
                _bindings.Remove(gesture);
            }

            // 获取所有快捷键手势
            public IEnumerable<KeyGesture> GetAllShortcuts() => _bindings.Keys;

            // 获取目标对象的命令绑定集合
            private CommandBindingCollection GetCommandBindings()
            {
                return _target switch
                {
                    Window window => window.CommandBindings,
                    UserControl page => page.CommandBindings,
                    _ => throw new NotSupportedException("Unsupported target type")
                };
            }
        }
    }
}