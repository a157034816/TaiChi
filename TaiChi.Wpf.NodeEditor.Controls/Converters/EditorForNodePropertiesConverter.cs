using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using TaiChi.Wpf.NodeEditor.Controls.Models;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 从节点实例中提取带有 EditorForNode 特性的属性集合
/// 注意：为避免控件库对应用层装配的编译时依赖，这里通过 FullName 判断特性类型。
/// </summary>
public class EditorForNodePropertiesConverter : IValueConverter
{
    private const string EditorAttributeFullName = "TaiChi.Wpf.NodeEditor.Core.Attributes.EditorForNodeAttribute";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return null;

        var type = value.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        var result = new List<NodeEditablePropertyInfo>();

        foreach (var p in props)
        {
            try
            {
                var editorAttr = p.GetCustomAttributes(inherit: true)
                    .FirstOrDefault(a => a.GetType().FullName == EditorAttributeFullName);
                if (editorAttr == null) continue;

                // 读取可选的 DisplayName 属性（如果存在）
                string displayName = p.Name;
                var dnProp = editorAttr.GetType().GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
                if (dnProp?.GetValue(editorAttr) is string dn && !string.IsNullOrWhiteSpace(dn))
                {
                    displayName = dn;
                }

                result.Add(new NodeEditablePropertyInfo
                {
                    Name = p.Name,
                    DisplayName = displayName,
                    PropertyType = p.PropertyType
                });
            }
            catch
            {
                // 忽略异常，避免渲染中断
            }
        }

        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

