using System;
using System.Windows;
using System.Windows.Controls;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Controls.Selectors;

/// <summary>
/// 默认的输入值模板选择器：
/// - string -> 使用 <see cref="StringTemplate"/>
/// - int/long -> 使用 <see cref="IntTemplate"/>/<see cref="LongTemplate"/>
/// - float/double/decimal -> 使用 <see cref="FloatTemplate"/>/<see cref="DoubleTemplate"/>/<see cref="DecimalTemplate"/>
/// - bool -> 使用 <see cref="BoolTemplate"/>
/// - DateTime -> 使用 <see cref="DateTimeTemplate"/>
/// - enum -> 使用 <see cref="EnumTemplate"/>
/// - 其他 -> 使用 <see cref="DefaultTemplate"/>
/// </summary>
public class DefaultPinInputValueTemplateSelector : DataTemplateSelector, IPinInputValueTemplateSelector
{
    /// <summary>
    /// string 类型对应模板（可选）。
    /// </summary>
    public DataTemplate? StringTemplate { get; set; }

    /// <summary>
    /// int 类型对应模板（可选）。
    /// </summary>
    public DataTemplate? IntTemplate { get; set; }

    /// <summary>
    /// long 类型对应模板（可选）。
    /// </summary>
    public DataTemplate? LongTemplate { get; set; }

    /// <summary>
    /// float 类型对应模板（可选）。
    /// </summary>
    public DataTemplate? FloatTemplate { get; set; }

    /// <summary>
    /// double 类型对应模板（可选）。
    /// </summary>
    public DataTemplate? DoubleTemplate { get; set; }

    /// <summary>
    /// decimal 类型对应模板（可选）。
    /// </summary>
    public DataTemplate? DecimalTemplate { get; set; }

    /// <summary>
    /// bool 类型对应模板（可选）。
    /// </summary>
    public DataTemplate? BoolTemplate { get; set; }

    /// <summary>
    /// DateTime 类型对应模板（可选）。
    /// </summary>
    public DataTemplate? DateTimeTemplate { get; set; }

    /// <summary>
    /// 枚举类型对应模板（可选）。
    /// </summary>
    public DataTemplate? EnumTemplate { get; set; }

    /// <summary>
    /// 默认回退模板。
    /// </summary>
    public DataTemplate? DefaultTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is Pin pin)
        {
            var type = pin.DataType;

            // 处理可空类型，获取底层类型
            if (type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            // 明确匹配 string
            if (type == typeof(string) && StringTemplate != null)
                return StringTemplate;

            // 数值类型 - 整数
            if (type == typeof(int) && IntTemplate != null)
                return IntTemplate;
            if (type == typeof(long) && LongTemplate != null)
                return LongTemplate;

            // 数值类型 - 浮点数
            if (type == typeof(float) && FloatTemplate != null)
                return FloatTemplate;
            if (type == typeof(double) && DoubleTemplate != null)
                return DoubleTemplate;
            if (type == typeof(decimal) && DecimalTemplate != null)
                return DecimalTemplate;

            // 布尔类型
            if (type == typeof(bool) && BoolTemplate != null)
                return BoolTemplate;

            // 日期时间类型
            if (type == typeof(DateTime) && DateTimeTemplate != null)
                return DateTimeTemplate;

            // 枚举类型
            if (type != null && type.IsEnum && EnumTemplate != null)
                return EnumTemplate;
        }

        return DefaultTemplate;
    }

    public DataTemplate GetDefaultTemplate()
    {
        // 返回显式设置的默认模板，否则返回一个空模板，避免Null
        return DefaultTemplate ?? new DataTemplate();
    }
}

