using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace TaiChi.Wpf.Behaviors
{
    /// <summary>
    /// 为ScrollViewer提供拖动功能的行为
    /// 支持鼠标右键拖动滚动和边界循环功能
    /// </summary>
    public class ScrollViewerDragBehavior : Behavior<ScrollViewer>
    {
        #region 依赖属性

        /// <summary>
        /// 拖动阈值，超过此距离才开始拖动
        /// </summary>
        public static readonly DependencyProperty DragThresholdProperty =
            DependencyProperty.Register(nameof(DragThreshold), typeof(double), typeof(ScrollViewerDragBehavior), 
                new PropertyMetadata(20.0));

        /// <summary>
        /// 是否启用边界循环
        /// </summary>
        public static readonly DependencyProperty EnableBoundaryCyclingProperty =
            DependencyProperty.Register(nameof(EnableBoundaryCycling), typeof(bool), typeof(ScrollViewerDragBehavior), 
                new PropertyMetadata(true));

        /// <summary>
        /// 使用哪个鼠标按键进行拖动
        /// </summary>
        public static readonly DependencyProperty DragMouseButtonProperty =
            DependencyProperty.Register(nameof(DragMouseButton), typeof(MouseButton), typeof(ScrollViewerDragBehavior), 
                new PropertyMetadata(MouseButton.Right));

        #endregion

        #region 属性

        /// <summary>
        /// 拖动阈值，超过此距离才开始拖动
        /// </summary>
        public double DragThreshold
        {
            get => (double)GetValue(DragThresholdProperty);
            set => SetValue(DragThresholdProperty, value);
        }

        /// <summary>
        /// 是否启用边界循环
        /// </summary>
        public bool EnableBoundaryCycling
        {
            get => (bool)GetValue(EnableBoundaryCyclingProperty);
            set => SetValue(EnableBoundaryCyclingProperty, value);
        }

        /// <summary>
        /// 使用哪个鼠标按键进行拖动
        /// </summary>
        public MouseButton DragMouseButton
        {
            get => (MouseButton)GetValue(DragMouseButtonProperty);
            set => SetValue(DragMouseButtonProperty, value);
        }

        #endregion

        #region 私有字段

        private Point _startPoint;
        private bool _isDragging = false;

        #endregion

        #region Win32 API

        // 导入设置鼠标位置的API
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        // 获取鼠标位置的API
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        // 定义POINT结构
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion

        #region 行为生命周期方法

        protected override void OnAttached()
        {
            base.OnAttached();
            
            // 添加鼠标事件处理
            AssociatedObject.PreviewMouseDown += ScrollViewer_PreviewMouseDown;
            AssociatedObject.PreviewMouseMove += ScrollViewer_PreviewMouseMove;
            AssociatedObject.PreviewMouseUp += ScrollViewer_PreviewMouseUp;
        }

        protected override void OnDetaching()
        {
            // 移除鼠标事件处理
            AssociatedObject.PreviewMouseDown -= ScrollViewer_PreviewMouseDown;
            AssociatedObject.PreviewMouseMove -= ScrollViewer_PreviewMouseMove;
            AssociatedObject.PreviewMouseUp -= ScrollViewer_PreviewMouseUp;
            
            base.OnDetaching();
        }

        #endregion

        #region 事件处理

        private void ScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 检查是否是指定的鼠标按键
            if (IsTargetMouseButtonPressed(e))
            {
                _startPoint = e.GetPosition(AssociatedObject);
                _isDragging = false;
                AssociatedObject.CaptureMouse(); // 捕获鼠标
                e.Handled = true;
            }
        }

        private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            // 检查是否是指定的鼠标按键按下状态
            if (IsTargetMouseButtonPressed(e) && AssociatedObject.IsMouseCaptured)
            {
                Point currentPoint = e.GetPosition(AssociatedObject);
                Vector delta = _startPoint - currentPoint;

                // 计算移动距离
                double distance = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);

                // 如果移动距离超过阈值，开始拖动
                if (!_isDragging && distance >= DragThreshold)
                {
                    _isDragging = true;
                }

                // 如果已经开始拖动，则移动ScrollViewer的滚动位置
                if (_isDragging)
                {
                    if (EnableBoundaryCycling)
                    {
                        HandleBoundaryCycling(delta);
                    }
                    else
                    {
                        // 正常滚动
                        AssociatedObject.ScrollToHorizontalOffset(AssociatedObject.HorizontalOffset + delta.X);
                        AssociatedObject.ScrollToVerticalOffset(AssociatedObject.VerticalOffset + delta.Y);
                        _startPoint = currentPoint; // 更新起始点，使移动更平滑
                    }
                }

                e.Handled = true;
            }
        }

        private void ScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            // 检查是否是指定的鼠标按键
            if (e.ChangedButton == DragMouseButton)
            {
                AssociatedObject.ReleaseMouseCapture();
                _isDragging = false;
                e.Handled = true;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查目标鼠标按键是否按下
        /// </summary>
        private bool IsTargetMouseButtonPressed(MouseEventArgs e)
        {
            switch (DragMouseButton)
            {
                case MouseButton.Left:
                    return e.LeftButton == MouseButtonState.Pressed;
                case MouseButton.Right:
                    return e.RightButton == MouseButtonState.Pressed;
                case MouseButton.Middle:
                    return e.MiddleButton == MouseButtonState.Pressed;
                case MouseButton.XButton1:
                    return e.XButton1 == MouseButtonState.Pressed;
                case MouseButton.XButton2:
                    return e.XButton2 == MouseButtonState.Pressed;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 处理边界循环
        /// </summary>
        private void HandleBoundaryCycling(Vector delta)
        {
            // 获取当前鼠标在屏幕上的位置
            GetCursorPos(out POINT currentCursorPos);

            // 获取工作区尺寸（排除任务栏等系统UI元素）
            Rect workArea = SystemParameters.WorkArea;

            // 获取当前窗口所在屏幕的DPI缩放因子
            PresentationSource source = PresentationSource.FromVisual(AssociatedObject);
            double dpiX = 1.0;
            double dpiY = 1.0;

            if (source != null)
            {
                Matrix transformToDevice = source.CompositionTarget.TransformToDevice;
                dpiX = transformToDevice.M11;
                dpiY = transformToDevice.M22;
            }

            // 计算实际的工作区边界（考虑DPI缩放）
            int leftBoundary = (int)(workArea.Left * dpiX);
            int rightBoundary = (int)((workArea.Left + workArea.Width) * dpiX) - 2;
            int topBoundary = (int)(workArea.Top * dpiY);
            int bottomBoundary = (int)((workArea.Top + workArea.Height) * dpiY) - 2;

            bool cursorMoved = false;

            // 检查鼠标是否到达屏幕边界
            // 如果鼠标到达左边界并且还想往左移动（delta.X > 0）
            if (currentCursorPos.X <= leftBoundary && delta.X > 0)
            {
                // 将鼠标瞬移到右边界
                SetCursorPos(rightBoundary, currentCursorPos.Y);
                cursorMoved = true;
            }
            // 如果鼠标到达右边界并且还想往右移动（delta.X < 0）
            else if (currentCursorPos.X >= rightBoundary && delta.X < 0)
            {
                // 将鼠标瞬移到左边界
                SetCursorPos(leftBoundary + 1, currentCursorPos.Y);
                cursorMoved = true;
            }

            // 如果鼠标到达上边界并且还想往上移动（delta.Y > 0）
            if (currentCursorPos.Y <= topBoundary && delta.Y > 0)
            {
                // 将鼠标瞬移到下边界
                SetCursorPos(currentCursorPos.X, bottomBoundary);
                cursorMoved = true;
            }
            // 如果鼠标到达下边界并且还想往下移动（delta.Y < 0）
            else if (currentCursorPos.Y >= bottomBoundary && delta.Y < 0)
            {
                // 将鼠标瞬移到上边界
                SetCursorPos(currentCursorPos.X, topBoundary + 1);
                cursorMoved = true;
            }

            // 如果鼠标位置被改变，需要重新计算起始点
            if (cursorMoved)
            {
                // 获取新的鼠标位置
                GetCursorPos(out POINT newCursorPos);

                // 将屏幕坐标转换为控件坐标
                Point screenPoint = new Point(newCursorPos.X, newCursorPos.Y);
                Point controlPoint = AssociatedObject.PointFromScreen(screenPoint);

                // 更新起始点
                _startPoint = controlPoint;
            }
            else
            {
                // 正常滚动
                AssociatedObject.ScrollToHorizontalOffset(AssociatedObject.HorizontalOffset + delta.X);
                AssociatedObject.ScrollToVerticalOffset(AssociatedObject.VerticalOffset + delta.Y);
                
                // 获取当前鼠标位置
                Point currentPoint = Mouse.GetPosition(AssociatedObject);
                _startPoint = currentPoint; // 更新起始点，使移动更平滑
            }
        }

        #endregion
    }
}
