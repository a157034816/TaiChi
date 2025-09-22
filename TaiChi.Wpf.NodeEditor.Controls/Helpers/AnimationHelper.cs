using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TaiChi.Wpf.NodeEditor.Controls.Helpers;

/// <summary>
/// 动画辅助类
/// </summary>
public static class AnimationHelper
{
    #region 常用动画时长

    /// <summary>
    /// 快速动画时长
    /// </summary>
    public static readonly Duration FastDuration = new Duration(TimeSpan.FromMilliseconds(150));

    /// <summary>
    /// 正常动画时长
    /// </summary>
    public static readonly Duration NormalDuration = new Duration(TimeSpan.FromMilliseconds(300));

    /// <summary>
    /// 慢速动画时长
    /// </summary>
    public static readonly Duration SlowDuration = new Duration(TimeSpan.FromMilliseconds(500));

    #endregion

    #region 缓动函数

    /// <summary>
    /// 缓入缓出缓动函数
    /// </summary>
    public static readonly IEasingFunction EaseInOut = new CubicEase { EasingMode = EasingMode.EaseInOut };

    /// <summary>
    /// 缓入缓动函数
    /// </summary>
    public static readonly IEasingFunction EaseIn = new CubicEase { EasingMode = EasingMode.EaseIn };

    /// <summary>
    /// 缓出缓动函数
    /// </summary>
    public static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// 弹性缓动函数
    /// </summary>
    public static readonly IEasingFunction Elastic = new ElasticEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// 反弹缓动函数
    /// </summary>
    public static readonly IEasingFunction Bounce = new BounceEase { EasingMode = EasingMode.EaseOut };

    #endregion

    #region 位置动画

    /// <summary>
    /// 创建位置动画
    /// </summary>
    /// <param name="element">目标元素</param>
    /// <param name="toPosition">目标位置</param>
    /// <param name="duration">动画时长</param>
    /// <param name="easingFunction">缓动函数</param>
    /// <param name="completed">完成回调</param>
    public static void AnimatePosition(UIElement element, Point toPosition, 
        Duration? duration = null, IEasingFunction? easingFunction = null, EventHandler? completed = null)
    {
        var animationDuration = duration ?? NormalDuration;
        var easing = easingFunction ?? EaseInOut;

        var storyboard = new Storyboard();

        // X轴动画
        var xAnimation = new DoubleAnimation
        {
            To = toPosition.X,
            Duration = animationDuration,
            EasingFunction = easing
        };
        Storyboard.SetTarget(xAnimation, element);
        Storyboard.SetTargetProperty(xAnimation, new PropertyPath("(Canvas.Left)"));
        storyboard.Children.Add(xAnimation);

        // Y轴动画
        var yAnimation = new DoubleAnimation
        {
            To = toPosition.Y,
            Duration = animationDuration,
            EasingFunction = easing
        };
        Storyboard.SetTarget(yAnimation, element);
        Storyboard.SetTargetProperty(yAnimation, new PropertyPath("(Canvas.Top)"));
        storyboard.Children.Add(yAnimation);

        if (completed != null)
        {
            storyboard.Completed += completed;
        }

        storyboard.Begin();
    }

    #endregion

    #region 透明度动画

    /// <summary>
    /// 淡入动画
    /// </summary>
    /// <param name="element">目标元素</param>
    /// <param name="duration">动画时长</param>
    /// <param name="easingFunction">缓动函数</param>
    /// <param name="completed">完成回调</param>
    public static void FadeIn(UIElement element, Duration? duration = null, 
        IEasingFunction? easingFunction = null, EventHandler? completed = null)
    {
        AnimateOpacity(element, 1.0, duration, easingFunction, completed);
    }

    /// <summary>
    /// 淡出动画
    /// </summary>
    /// <param name="element">目标元素</param>
    /// <param name="duration">动画时长</param>
    /// <param name="easingFunction">缓动函数</param>
    /// <param name="completed">完成回调</param>
    public static void FadeOut(UIElement element, Duration? duration = null, 
        IEasingFunction? easingFunction = null, EventHandler? completed = null)
    {
        AnimateOpacity(element, 0.0, duration, easingFunction, completed);
    }

    /// <summary>
    /// 透明度动画
    /// </summary>
    /// <param name="element">目标元素</param>
    /// <param name="toOpacity">目标透明度</param>
    /// <param name="duration">动画时长</param>
    /// <param name="easingFunction">缓动函数</param>
    /// <param name="completed">完成回调</param>
    public static void AnimateOpacity(UIElement element, double toOpacity, 
        Duration? duration = null, IEasingFunction? easingFunction = null, EventHandler? completed = null)
    {
        var animation = new DoubleAnimation
        {
            To = toOpacity,
            Duration = duration ?? NormalDuration,
            EasingFunction = easingFunction ?? EaseInOut
        };

        if (completed != null)
        {
            animation.Completed += completed;
        }

        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    #endregion

    #region 缩放动画

    /// <summary>
    /// 缩放动画
    /// </summary>
    /// <param name="element">目标元素</param>
    /// <param name="toScale">目标缩放比例</param>
    /// <param name="duration">动画时长</param>
    /// <param name="easingFunction">缓动函数</param>
    /// <param name="completed">完成回调</param>
    public static void AnimateScale(UIElement element, double toScale, 
        Duration? duration = null, IEasingFunction? easingFunction = null, EventHandler? completed = null)
    {
        AnimateScale(element, toScale, toScale, duration, easingFunction, completed);
    }

    /// <summary>
    /// 缩放动画（分别指定X和Y轴）
    /// </summary>
    /// <param name="element">目标元素</param>
    /// <param name="toScaleX">X轴目标缩放比例</param>
    /// <param name="toScaleY">Y轴目标缩放比例</param>
    /// <param name="duration">动画时长</param>
    /// <param name="easingFunction">缓动函数</param>
    /// <param name="completed">完成回调</param>
    public static void AnimateScale(UIElement element, double toScaleX, double toScaleY, 
        Duration? duration = null, IEasingFunction? easingFunction = null, EventHandler? completed = null)
    {
        // 确保元素有ScaleTransform
        if (element.RenderTransform is not ScaleTransform scaleTransform)
        {
            scaleTransform = new ScaleTransform();
            element.RenderTransform = scaleTransform;
            element.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var animationDuration = duration ?? NormalDuration;
        var easing = easingFunction ?? EaseInOut;

        var storyboard = new Storyboard();

        // X轴缩放动画
        var scaleXAnimation = new DoubleAnimation
        {
            To = toScaleX,
            Duration = animationDuration,
            EasingFunction = easing
        };
        Storyboard.SetTarget(scaleXAnimation, scaleTransform);
        Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath(ScaleTransform.ScaleXProperty));
        storyboard.Children.Add(scaleXAnimation);

        // Y轴缩放动画
        var scaleYAnimation = new DoubleAnimation
        {
            To = toScaleY,
            Duration = animationDuration,
            EasingFunction = easing
        };
        Storyboard.SetTarget(scaleYAnimation, scaleTransform);
        Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath(ScaleTransform.ScaleYProperty));
        storyboard.Children.Add(scaleYAnimation);

        if (completed != null)
        {
            storyboard.Completed += completed;
        }

        storyboard.Begin();
    }

    #endregion

    #region 颜色动画

    /// <summary>
    /// 颜色动画
    /// </summary>
    /// <param name="brush">目标画刷</param>
    /// <param name="toColor">目标颜色</param>
    /// <param name="duration">动画时长</param>
    /// <param name="easingFunction">缓动函数</param>
    /// <param name="completed">完成回调</param>
    public static void AnimateColor(SolidColorBrush brush, Color toColor, 
        Duration? duration = null, IEasingFunction? easingFunction = null, EventHandler? completed = null)
    {
        var animation = new ColorAnimation
        {
            To = toColor,
            Duration = duration ?? NormalDuration,
            EasingFunction = easingFunction ?? EaseInOut
        };

        if (completed != null)
        {
            animation.Completed += completed;
        }

        brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    #endregion

    #region 组合动画

    /// <summary>
    /// 弹出动画（缩放+透明度）
    /// </summary>
    /// <param name="element">目标元素</param>
    /// <param name="duration">动画时长</param>
    /// <param name="completed">完成回调</param>
    public static void PopIn(UIElement element, Duration? duration = null, EventHandler? completed = null)
    {
        element.Opacity = 0;
        element.RenderTransform = new ScaleTransform(0.8, 0.8);
        element.RenderTransformOrigin = new Point(0.5, 0.5);

        var animationDuration = duration ?? NormalDuration;
        var storyboard = new Storyboard();

        // 透明度动画
        var opacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = animationDuration,
            EasingFunction = EaseOut
        };
        Storyboard.SetTarget(opacityAnimation, element);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(opacityAnimation);

        // 缩放动画
        var scaleAnimation = new DoubleAnimation
        {
            From = 0.8,
            To = 1.0,
            Duration = animationDuration,
            EasingFunction = Elastic
        };
        Storyboard.SetTarget(scaleAnimation, element.RenderTransform);
        Storyboard.SetTargetProperty(scaleAnimation, new PropertyPath("ScaleX"));
        storyboard.Children.Add(scaleAnimation);

        var scaleYAnimation = new DoubleAnimation
        {
            From = 0.8,
            To = 1.0,
            Duration = animationDuration,
            EasingFunction = Elastic
        };
        Storyboard.SetTarget(scaleYAnimation, element.RenderTransform);
        Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("ScaleY"));
        storyboard.Children.Add(scaleYAnimation);

        if (completed != null)
        {
            storyboard.Completed += completed;
        }

        storyboard.Begin();
    }

    /// <summary>
    /// 收缩动画（缩放+透明度）
    /// </summary>
    /// <param name="element">目标元素</param>
    /// <param name="duration">动画时长</param>
    /// <param name="completed">完成回调</param>
    public static void PopOut(UIElement element, Duration? duration = null, EventHandler? completed = null)
    {
        var animationDuration = duration ?? FastDuration;
        var storyboard = new Storyboard();

        // 透明度动画
        var opacityAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = animationDuration,
            EasingFunction = EaseIn
        };
        Storyboard.SetTarget(opacityAnimation, element);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(opacityAnimation);

        // 缩放动画
        if (element.RenderTransform is ScaleTransform scaleTransform)
        {
            var scaleAnimation = new DoubleAnimation
            {
                To = 0.8,
                Duration = animationDuration,
                EasingFunction = EaseIn
            };
            Storyboard.SetTarget(scaleAnimation, scaleTransform);
            Storyboard.SetTargetProperty(scaleAnimation, new PropertyPath("ScaleX"));
            storyboard.Children.Add(scaleAnimation);

            var scaleYAnimation = new DoubleAnimation
            {
                To = 0.8,
                Duration = animationDuration,
                EasingFunction = EaseIn
            };
            Storyboard.SetTarget(scaleYAnimation, scaleTransform);
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("ScaleY"));
            storyboard.Children.Add(scaleYAnimation);
        }

        if (completed != null)
        {
            storyboard.Completed += completed;
        }

        storyboard.Begin();
    }

    #endregion

    #region 实用方法

    /// <summary>
    /// 停止元素上的所有动画
    /// </summary>
    /// <param name="element">目标元素</param>
    public static void StopAllAnimations(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.BeginAnimation(Canvas.LeftProperty, null);
        element.BeginAnimation(Canvas.TopProperty, null);
        
        if (element.RenderTransform is Transform transform)
        {
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
    }

    #endregion
}
