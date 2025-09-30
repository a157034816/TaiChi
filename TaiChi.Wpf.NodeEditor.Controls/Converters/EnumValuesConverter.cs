using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 枚举类型到枚举值集合的转换器
/// 用于为枚举类型的ComboBox动态填充所有可用的枚举值
/// </summary>
public class EnumValuesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Type dataType && dataType.IsEnum)
        {
            // 返回枚举的所有值
            return Enum.GetValues(dataType).Cast<object>().ToArray();
        }

        // 如果不是枚举类型，返回空数组
        return Array.Empty<object>();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}