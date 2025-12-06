using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace TaiChi.Wpf.NodeEditor.Controls.Helpers;

/// <summary>
/// 可视化树辅助类
/// </summary>
public static class VisualTreeHelper
{
    /// <summary>
    /// 查找指定类型的父元素
    /// </summary>
    /// <typeparam name="T">要查找的类型</typeparam>
    /// <param name="child">子元素</param>
    /// <returns>找到的父元素，如果没找到返回null</returns>
    public static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        
        return null;
    }

    /// <summary>
    /// 查找指定类型的子元素
    /// </summary>
    /// <typeparam name="T">要查找的类型</typeparam>
    /// <param name="parent">父元素</param>
    /// <returns>找到的第一个子元素，如果没找到返回null</returns>
    public static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        
        for (int i = 0; i < childrenCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            
            if (child is T typedChild)
                return typedChild;
            
            var foundChild = FindChild<T>(child);
            if (foundChild != null)
                return foundChild;
        }
        
        return null;
    }

    /// <summary>
    /// 查找指定名称的子元素
    /// </summary>
    /// <param name="parent">父元素</param>
    /// <param name="name">元素名称</param>
    /// <returns>找到的元素，如果没找到返回null</returns>
    public static FrameworkElement? FindChildByName(DependencyObject parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;

        var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        
        for (int i = 0; i < childrenCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            
            if (child is FrameworkElement element && element.Name == name)
                return element;
            
            var foundChild = FindChildByName(child, name);
            if (foundChild != null)
                return foundChild;
        }
        
        return null;
    }

    /// <summary>
    /// 查找所有指定类型的子元素
    /// </summary>
    /// <typeparam name="T">要查找的类型</typeparam>
    /// <param name="parent">父元素</param>
    /// <returns>找到的所有子元素</returns>
    public static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var children = new List<T>();
        
        if (parent == null) return children;

        var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        
        for (int i = 0; i < childrenCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            
            if (child is T typedChild)
                children.Add(typedChild);
            
            children.AddRange(FindChildren<T>(child));
        }
        
        return children;
    }

    /// <summary>
    /// 获取元素在指定父元素中的相对位置
    /// </summary>
    /// <param name="element">元素</param>
    /// <param name="relativeTo">相对于的父元素</param>
    /// <returns>相对位置</returns>
    public static Point GetRelativePosition(UIElement element, UIElement relativeTo)
    {
        try
        {
            var transform = element.TransformToVisual(relativeTo);
            return transform.Transform(new Point(0, 0));
        }
        catch
        {
            return new Point(0, 0);
        }
    }

    /// <summary>
    /// 获取元素的边界矩形
    /// </summary>
    /// <param name="element">元素</param>
    /// <param name="relativeTo">相对于的父元素</param>
    /// <returns>边界矩形</returns>
    public static Rect GetBounds(FrameworkElement element, UIElement? relativeTo = null)
    {
        if (element == null) return Rect.Empty;

        try
        {
            var position = relativeTo != null 
                ? GetRelativePosition(element, relativeTo) 
                : new Point(0, 0);
            
            return new Rect(position.X, position.Y, element.ActualWidth, element.ActualHeight);
        }
        catch
        {
            return Rect.Empty;
        }
    }

    /// <summary>
    /// 检查元素是否在可视化树中
    /// </summary>
    /// <param name="element">要检查的元素</param>
    /// <returns>如果在可视化树中返回true</returns>
    public static bool IsInVisualTree(DependencyObject element)
    {
        if (element == null) return false;

        try
        {
            return PresentationSource.FromDependencyObject(element) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取鼠标位置下的元素
    /// </summary>
    /// <param name="parent">父容器</param>
    /// <param name="position">鼠标位置</param>
    /// <returns>命中的元素</returns>
    public static DependencyObject? HitTest(Visual parent, Point position)
    {
        if (parent == null) return null;

        try
        {
            var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(parent, position);
            return hitResult?.VisualHit;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取指定类型的命中测试结果
    /// </summary>
    /// <typeparam name="T">要查找的类型</typeparam>
    /// <param name="parent">父容器</param>
    /// <param name="position">测试位置</param>
    /// <returns>命中的元素</returns>
    public static T? HitTest<T>(Visual parent, Point position) where T : DependencyObject
    {
        var hit = HitTest(parent, position);
        
        while (hit != null)
        {
            if (hit is T typedHit)
                return typedHit;
            
            hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
        }
        
        return null;
    }

    /// <summary>
    /// 获取元素的渲染变换矩阵
    /// </summary>
    /// <param name="element">元素</param>
    /// <returns>变换矩阵</returns>
    public static Matrix GetRenderTransformMatrix(UIElement element)
    {
        if (element?.RenderTransform == null)
            return Matrix.Identity;

        try
        {
            return element.RenderTransform.Value;
        }
        catch
        {
            return Matrix.Identity;
        }
    }

    /// <summary>
    /// 检查元素是否可见
    /// </summary>
    /// <param name="element">要检查的元素</param>
    /// <returns>如果可见返回true</returns>
    public static bool IsVisible(UIElement element)
    {
        if (element == null) return false;

        return element.IsVisible && 
               element.Visibility == Visibility.Visible && 
               element.Opacity > 0;
    }
}
