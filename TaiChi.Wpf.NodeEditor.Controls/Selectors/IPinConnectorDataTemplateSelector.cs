using System.Windows;
using System.Windows.Controls;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Controls.Selectors;

/// <summary>
/// 统一的 Pin 连接器数据模板接口
/// 提供统一的默认模板，不再区分输入和输出选择器
/// </summary>
public interface IPinConnectorDataTemplateSelector
{
    /// <summary>
    /// 获取统一的 Pin 连接器默认模板
    /// </summary>
    /// <returns>默认的 DataTemplate</returns>
    DataTemplate GetDefaultTemplate();
}