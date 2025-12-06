using System;
using System.Windows;
using System.Windows.Controls;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Controls.Selectors;

/// <summary>
/// 默认的输入值模板选择器：
/// - string -> 使用 <see cref="StringTemplate"/>
/// - 其他 -> 使用 <see cref="DefaultTemplate"/>
/// </summary>
public class DefaultPinInputValueTemplateSelector : DataTemplateSelector, IPinInputValueTemplateSelector
{
    /// <summary>
    /// string 类型对应模板（可选）。
    /// </summary>
    public DataTemplate? StringTemplate { get; set; }

    /// <summary>
    /// 默认回退模板。
    /// </summary>
    public DataTemplate? DefaultTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is Pin pin)
        {
            var type = pin.DataType;

            // 明确匹配 string
            if (type == typeof(string) && StringTemplate != null)
                return StringTemplate;

            // 可在此扩展其他内置类型（int/double/bool/enum等）
            // if (type == typeof(int) && IntTemplate != null) return IntTemplate;
            // if (type == typeof(double) && DoubleTemplate != null) return DoubleTemplate;
            // if (type == typeof(bool) && BoolTemplate != null) return BoolTemplate;
        }

        return GetDefaultTemplate();
    }

    public DataTemplate GetDefaultTemplate()
    {
        // 返回显式设置的默认模板，否则返回一个空模板，避免Null
        return DefaultTemplate ?? new DataTemplate();
    }
}

