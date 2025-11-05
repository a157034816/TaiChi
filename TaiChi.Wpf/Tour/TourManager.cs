using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace TaiChi.Wpf.Tour
{
    /// <summary>
    /// 漫游式引导管理器：负责遮罩、气泡、定位与步骤切换。
    /// 使用示例：
    /// var mgr = TourManager.Start(window, new [] { new TourStep { Title = "欢迎", Description = "...", Target = btnStart } });
    /// </summary>
    public sealed class TourManager
    {
        private readonly Window _window;
        private readonly FrameworkElement _root;
        private readonly IList<TourStep> _steps;
        private readonly TourAdorner _adorner;
        private readonly AdornerLayer _adornerLayer;
        private readonly Popup _popup;
        private readonly TourBubble _bubble;
        private int _index;

        /// <summary>
        /// 当前步骤索引（从 0 开始）。
        /// </summary>
        public int Index => _index;

        /// <summary>
        /// 总步骤数。
        /// </summary>
        public int Count => _steps.Count;

        /// <summary>
        /// 步骤变更事件（index 从 0 开始）。
        /// </summary>
        public event EventHandler<int>? StepChanged;

        /// <summary>
        /// 引导完成或中止事件。
        /// </summary>
        public event EventHandler? Completed;

        /// <summary>
        /// 背景点击行为配置。默认 <see cref="TourBackgroundClickBehavior.None"/>（无动作）。
        /// 当用户点击遮罩背景（高亮区域之外）时，将根据此配置执行对应逻辑。
        /// </summary>
        public TourBackgroundClickBehavior BackgroundClickBehavior { get; set; } = TourBackgroundClickBehavior.None;

        private TourManager(Window window, IList<TourStep> steps)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
            if (_steps.Count == 0) throw new ArgumentException("steps 不能为空", nameof(steps));

            _root = (FrameworkElement)(window.Content as FrameworkElement
                ?? throw new InvalidOperationException("窗口的 Content 必须是 FrameworkElement"));

            // 遮罩层
            _adornerLayer = AdornerLayer.GetAdornerLayer(_root)
                            ?? throw new InvalidOperationException("未找到 AdornerLayer，请确保窗口根容器支持 AdornerLayer（例如 Grid）。");
            _adorner = new TourAdorner(_root);
            _adornerLayer.Add(_adorner);
            _adorner.BackgroundClicked += OnAdornerBackgroundClicked;

            // 气泡
            _bubble = new TourBubble();
            _bubble.NextCommand = new DelegateCommand(_ => Next());
            _bubble.PrevCommand = new DelegateCommand(_ => Prev());
            _bubble.CloseCommand = new DelegateCommand(_ => Stop());

            _popup = new Popup
            {
                Child = _bubble,
                AllowsTransparency = true,
                StaysOpen = true,
                Placement = PlacementMode.Relative,
                PlacementTarget = _root,
            };

            HookWindowEvents();
        }

        /// <summary>
        /// 启动引导并返回管理器实例。
        /// </summary>
        /// <param name="window">宿主窗口。</param>
        /// <param name="steps">步骤集合，不可为空。</param>
        /// <returns>管理器实例，可用于控制流程。</returns>
        public static TourManager Start(Window window, IList<TourStep> steps)
        {
            var mgr = new TourManager(window, steps);
            mgr._index = 0;
            mgr.ShowStep(mgr._index);
            return mgr;
        }

        /// <summary>
        /// 切换到下一步；若已是最后一步则结束引导。
        /// </summary>
        public void Next()
        {
            if (_index < _steps.Count - 1)
            {
                _index++;
                ShowStep(_index);
            }
            else
            {
                Stop();
            }
        }

        /// <summary>
        /// 切换到上一步；若已经是第一步则保持不变。
        /// </summary>
        public void Prev()
        {
            if (_index > 0)
            {
                _index--;
                ShowStep(_index);
            }
        }

        /// <summary>
        /// 停止并清理引导（移除遮罩与气泡）。
        /// </summary>
        public void Stop()
        {
            try
            {
                _popup.IsOpen = false;
                _popup.Child = null;
            }
            catch { /* ignore */ }

            try
            {
                _adornerLayer.Remove(_adorner);
            }
            catch { /* ignore */ }
            finally
            {
                // 解除遮罩事件订阅，避免潜在泄漏
                _adorner.BackgroundClicked -= OnAdornerBackgroundClicked;
            }

            UnhookWindowEvents();
            Completed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 处理遮罩背景点击，根据 <see cref="BackgroundClickBehavior"/> 执行动作。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="p">点击坐标（窗口根元素坐标系）。</param>
        private void OnAdornerBackgroundClicked(object? sender, Point p)
        {
            switch (BackgroundClickBehavior)
            {
                case TourBackgroundClickBehavior.None:
                    return;
                case TourBackgroundClickBehavior.Next:
                    Next();
                    return;
                case TourBackgroundClickBehavior.Prev:
                    Prev();
                    return;
                case TourBackgroundClickBehavior.Stop:
                    Stop();
                    return;
                default:
                    return;
            }
        }

        private void HookWindowEvents()
        {
            _window.PreviewKeyDown += OnWindowPreviewKeyDown;
            _root.LayoutUpdated += OnLayoutUpdated;
            _window.LocationChanged += OnWindowLocationChanged;
            _window.SizeChanged += OnWindowSizeChanged;
        }

        private void UnhookWindowEvents()
        {
            _window.PreviewKeyDown -= OnWindowPreviewKeyDown;
            _root.LayoutUpdated -= OnLayoutUpdated;
            _window.LocationChanged -= OnWindowLocationChanged;
            _window.SizeChanged -= OnWindowSizeChanged;
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 重新布局
            Reposition();
        }

        private void OnWindowLocationChanged(object? sender, EventArgs e)
        {
            // 重新布局
            Reposition();
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            // 控件移动/尺寸变化时刷新
            Reposition();
        }

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Stop();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                Next();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 显示指定步骤并布局。
        /// </summary>
        private void ShowStep(int index)
        {
            var step = _steps[index];
            _bubble.Title = step.Title ?? string.Empty;
            _bubble.Description = step.Description ?? string.Empty;
            _bubble.StepText = $"({index + 1}/{_steps.Count})";

            _popup.IsOpen = true;

            StepChanged?.Invoke(this, index);
            Reposition();
        }

        /// <summary>
        /// 重新计算高亮区域与气泡位置。
        /// </summary>
        private void Reposition()
        {
            if (_index < 0 || _index >= _steps.Count) return;
            var step = _steps[_index];

            Rect highlight = ComputeHighlightRect(step);
            _adorner.UpdateHighlight(highlight, step.CornerRadius);

            // 计算气泡大小
            _bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var bubbleSize = _bubble.DesiredSize;
            if (double.IsNaN(bubbleSize.Width) || double.IsInfinity(bubbleSize.Width) || bubbleSize.Width <= 0)
                bubbleSize = new Size(320, 140);

            var placement = step.Placement;
            var rootSize = new Size(_root.ActualWidth, _root.ActualHeight);
            const double gap = 8;

            // 候选位置按顺序尝试
            var candidates = GetCandidatePositions(placement, highlight, bubbleSize, gap);
            Point chosen = candidates.FirstOrDefault(p => FitsInWindow(p, bubbleSize, rootSize));
            if (chosen == default)
            {
                // 若无合适位置，退化为靠近目标并夹紧在窗口内
                chosen = new Point(
                    Math.Max(8, Math.Min(rootSize.Width - bubbleSize.Width - 8, highlight.X + (highlight.Width - bubbleSize.Width) / 2)),
                    Math.Max(8, Math.Min(rootSize.Height - bubbleSize.Height - 8, highlight.Bottom + gap))
                );
            }

            _popup.HorizontalOffset = chosen.X;
            _popup.VerticalOffset = chosen.Y;
        }

        /// <summary>
        /// 计算高亮矩形（窗口根元素坐标系）。
        /// </summary>
        private Rect ComputeHighlightRect(TourStep step)
        {
            Rect rect;
            if (step.Target != null)
            {
                if (!step.Target.IsLoaded)
                {
                    // 目标未加载时，尝试在 Loaded 后再布局
                    step.Target.Loaded -= OnTargetLoaded;
                    step.Target.Loaded += OnTargetLoaded;
                }
                var size = new Size(step.Target.ActualWidth, step.Target.ActualHeight);
                var r = new Rect(new Point(0, 0), size);
                try
                {
                    var tx = step.Target.TransformToVisual(_root);
                    rect = tx.TransformBounds(r);
                }
                catch
                {
                    rect = step.FallbackHighlightRect ?? new Rect(new Point(_root.ActualWidth / 2 - 80, _root.ActualHeight / 2 - 40), new Size(160, 80));
                }
            }
            else if (step.FallbackHighlightRect.HasValue)
            {
                rect = step.FallbackHighlightRect.Value;
            }
            else
            {
                rect = new Rect(new Point(_root.ActualWidth / 2 - 80, _root.ActualHeight / 2 - 40), new Size(160, 80));
            }

            var pad = step.HighlightPadding;
            // 使用四边独立留白，构造新的外扩矩形
            rect = new Rect(
                rect.X - pad.Left,
                rect.Y - pad.Top,
                rect.Width + pad.Left + pad.Right,
                rect.Height + pad.Top + pad.Bottom);
            return rect;
        }

        private void OnTargetLoaded(object sender, RoutedEventArgs e)
        {
            // 目标加载后重新布局
            Reposition();
        }

        private static IEnumerable<Point> GetCandidatePositions(TourPlacement placement, Rect target, Size bubble, double gap)
        {
            IEnumerable<TourPlacement> order = placement == TourPlacement.Auto
                ? new[] { TourPlacement.Bottom, TourPlacement.Right, TourPlacement.Top, TourPlacement.Left }
                : new[] { placement };

            foreach (var p in order)
            {
                switch (p)
                {
                    case TourPlacement.Top:
                        yield return new Point(target.X + (target.Width - bubble.Width) / 2, target.Y - bubble.Height - gap);
                        break;
                    case TourPlacement.Bottom:
                        yield return new Point(target.X + (target.Width - bubble.Width) / 2, target.Bottom + gap);
                        break;
                    case TourPlacement.Left:
                        yield return new Point(target.X - bubble.Width - gap, target.Y + (target.Height - bubble.Height) / 2);
                        break;
                    case TourPlacement.Right:
                        yield return new Point(target.Right + gap, target.Y + (target.Height - bubble.Height) / 2);
                        break;
                }
            }
        }

        private static bool FitsInWindow(Point p, Size bubble, Size win)
        {
            const double margin = 8;
            return p.X >= margin && p.Y >= margin && p.X + bubble.Width <= win.Width - margin && p.Y + bubble.Height <= win.Height - margin;
        }

        /// <summary>
        /// 简易命令实现，避免引入额外依赖。
        /// </summary>
        private sealed class DelegateCommand : ICommand
        {
            private readonly Action<object?> _execute;
            private readonly Func<object?, bool>? _canExecute;

            public DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

            public void Execute(object? parameter) => _execute(parameter);

            public event EventHandler? CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
        }
    }
}
