using System.Windows;
using System.Windows.Controls;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Controls.Selectors;

/// <summary>
/// 输入引脚未连接时，基于 Pin 的 DataType 选择用于编辑值的 DataTemplate 的选择器接口。
/// 通过实现该接口，外部项目可以替换为第三方控件。
/// </summary>
public interface IPinInputValueTemplateSelector
{
    /// <summary>
    /// 获取默认的输入值模板（找不到匹配类型时回退）。
    /// </summary>
    DataTemplate GetDefaultTemplate();
}

