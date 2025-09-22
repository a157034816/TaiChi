using System;
using System.Windows;
using System.Windows.Input;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace TaiChi.Wpf.NodeEditor.Controls.Helpers;

/// <summary>
/// 拖拽操作辅助类
/// </summary>
public static class DragDropHelper
{
    /// <summary>
    /// 节点元数据拖拽格式
    /// </summary>
    public const string NodeMetadataFormat = "NodeMetadata";

    /// <summary>
    /// 开始拖拽节点元数据
    /// </summary>
    /// <param name="source">拖拽源控件</param>
    /// <param name="metadata">节点元数据</param>
    public static void StartDragNodeMetadata(DependencyObject source, NodeMetadata metadata)
    {
        if (source == null || metadata == null)
            return;

        var dataObject = new DataObject();
        dataObject.SetData(NodeMetadataFormat, metadata);
        dataObject.SetData(DataFormats.Text, metadata.Name);

        try
        {
            DragDrop.DoDragDrop(source, dataObject, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            // 记录拖拽错误，但不抛出异常
            System.Diagnostics.Debug.WriteLine($"Drag operation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查是否包含节点元数据
    /// </summary>
    /// <param name="dataObject">数据对象</param>
    /// <returns>如果包含节点元数据返回true</returns>
    public static bool ContainsNodeMetadata(IDataObject dataObject)
    {
        return dataObject?.GetDataPresent(NodeMetadataFormat) == true;
    }

    /// <summary>
    /// 获取节点元数据
    /// </summary>
    /// <param name="dataObject">数据对象</param>
    /// <returns>节点元数据，如果不存在返回null</returns>
    public static NodeMetadata? GetNodeMetadata(IDataObject dataObject)
    {
        if (!ContainsNodeMetadata(dataObject))
            return null;

        try
        {
            return dataObject.GetData(NodeMetadataFormat) as NodeMetadata;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get node metadata: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 检查拖拽操作是否有效
    /// </summary>
    /// <param name="e">拖拽事件参数</param>
    /// <returns>如果拖拽有效返回true</returns>
    public static bool IsValidDragOperation(DragEventArgs e)
    {
        return ContainsNodeMetadata(e.Data) && 
               (e.AllowedEffects & DragDropEffects.Copy) == DragDropEffects.Copy;
    }

    /// <summary>
    /// 设置拖拽效果
    /// </summary>
    /// <param name="e">拖拽事件参数</param>
    /// <param name="isValid">是否为有效拖拽</param>
    public static void SetDragEffect(DragEventArgs e, bool isValid = true)
    {
        if (isValid && IsValidDragOperation(e))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    /// <summary>
    /// 检查鼠标是否开始拖拽
    /// </summary>
    /// <param name="startPoint">开始点</param>
    /// <param name="currentPoint">当前点</param>
    /// <returns>如果开始拖拽返回true</returns>
    public static bool HasMouseMovedEnoughForDrag(Point startPoint, Point currentPoint)
    {
        var deltaX = Math.Abs(currentPoint.X - startPoint.X);
        var deltaY = Math.Abs(currentPoint.Y - startPoint.Y);
        
        return deltaX > SystemParameters.MinimumHorizontalDragDistance ||
               deltaY > SystemParameters.MinimumVerticalDragDistance;
    }

    /// <summary>
    /// 获取拖拽预览元素
    /// </summary>
    /// <param name="metadata">节点元数据</param>
    /// <returns>预览元素</returns>
    public static FrameworkElement CreateDragPreview(NodeMetadata metadata)
    {
        var border = new System.Windows.Controls.Border
        {
            Background = System.Windows.Media.Brushes.LightBlue,
            BorderBrush = System.Windows.Media.Brushes.Blue,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 4, 8, 4),
            Opacity = 0.8
        };

        var textBlock = new System.Windows.Controls.TextBlock
        {
            Text = metadata.Name,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.DarkBlue
        };

        border.Child = textBlock;
        return border;
    }
}
