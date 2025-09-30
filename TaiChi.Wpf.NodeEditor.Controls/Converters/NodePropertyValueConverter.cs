using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 在绑定中，通过属性名读/写节点对象的属性值。
/// 绑定用法：
///   Source: 节点对象（即 NodeControl.NodeData）
///   ConverterParameter: 属性名（string）
/// </summary>
public class NodePropertyValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter is not string propertyName) return null;
        var type = value.GetType();
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null || !prop.CanRead) return null;
        try { return prop.GetValue(value); } catch { return null; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // targetType 在此未使用；parameter 是属性名
        if (parameter is not string propertyName)
            return Binding.DoNothing;

        return new PropertyUpdateRequest(propertyName, value);
    }

    /// <summary>
    /// 用于在绑定回写时携带“属性名+新值”的包装对象。
    /// 将由 MultiBinding 或中转逻辑处理到具体对象上。
    /// 这里我们在 XAML 中不会直接使用 MultiBinding，因此在控件内部将手动解析。
    /// </summary>
    public class PropertyUpdateRequest
    {
        public string PropertyName { get; }
        public object? NewValue { get; }
        public PropertyUpdateRequest(string name, object? value) { PropertyName = name; NewValue = value; }
    }
}

