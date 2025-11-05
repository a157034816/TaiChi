using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace TaiChi.Wpf.Tour
{
    /// <summary>
    /// 覆盖在窗口上的半透明遮罩 Adorner，并在目标区域挖出高亮“镂空”。
    /// </summary>
    internal sealed class TourAdorner : Adorner
    {
        private Rect _highlightRect;
        private double _cornerRadius = 6d;
        private Brush _overlayBrush = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));

        /// <summary>
        /// 当用户在高亮区域之外点击遮罩背景时触发。
        /// 参数为点击点在被装饰元素（窗口根）坐标系下的位置。
        /// 仅用于背景点击行为的外部配置；默认情况下不会引发任何后续动作。
        /// </summary>
        public event EventHandler<Point>? BackgroundClicked;

        /// <summary>
        /// 创建 Tour 遮罩层。
        /// </summary>
        /// <param name="adornedElement">被装饰的元素（通常是窗口的根元素）。</param>
        public TourAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = true; // 拦截底层点击，防止误操作
            Focusable = false;
        }

        /// <summary>
        /// 更新高亮矩形与圆角。
        /// </summary>
        /// <param name="highlightRect">窗口坐标系下的高亮区域。</param>
        /// <param name="cornerRadius">高亮圆角。</param>
        public void UpdateHighlight(Rect highlightRect, double cornerRadius)
        {
            _highlightRect = highlightRect;
            _cornerRadius = cornerRadius;
            InvalidateVisual();
        }

        /// <summary>
        /// 设置遮罩画刷（默认半透明黑）。
        /// </summary>
        public void SetOverlayBrush(Brush brush)
        {
            _overlayBrush = brush;
            InvalidateVisual();
        }

        /// <summary>
        /// 覆写绘制逻辑，绘制全屏遮罩并挖出高亮区域。
        /// </summary>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var size = AdornedElement.RenderSize;
            var fullRect = new Rect(new Point(0, 0), size);

            var full = new RectangleGeometry(fullRect);
            var hole = new RectangleGeometry(_highlightRect, _cornerRadius, _cornerRadius);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, full, hole);

            drawingContext.DrawGeometry(_overlayBrush, null, combined);
        }

        /// <summary>
        /// 屏蔽鼠标点击穿透到底层。
        /// </summary>
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            // 仅在高亮区域之外的“背景”点击时，通知外部；仍然屏蔽事件向下传递
            var pt = e.GetPosition(AdornedElement);
            if (!_highlightRect.Contains(pt))
            {
                try
                {
                    BackgroundClicked?.Invoke(this, pt);
                }
                catch
                {
                    // 避免外部事件处理异常影响遮罩基本行为
                }
            }
            e.Handled = true;
            base.OnPreviewMouseDown(e);
        }

        /// <summary>
        /// 屏蔽鼠标释放穿透到底层。
        /// </summary>
        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            e.Handled = true;
            base.OnPreviewMouseUp(e);
        }

        /// <summary>
        /// 屏蔽鼠标移动穿透到底层。
        /// </summary>
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            e.Handled = true;
            base.OnPreviewMouseMove(e);
        }
    }
}
