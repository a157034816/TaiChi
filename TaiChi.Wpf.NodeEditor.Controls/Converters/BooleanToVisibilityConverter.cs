using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 布尔值到可见性的转换器
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 是否反转结果
    /// </summary>
    public bool Invert { get; set; }

    /// <summary>
    /// 隐藏时使用Hidden而不是Collapsed
    /// </summary>
    public bool UseHidden { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = false;

        if (value is bool b)
            boolValue = b;
        else if (value != null)
            bool.TryParse(value.ToString(), out boolValue);

        if (Invert)
            boolValue = !boolValue;

        if (boolValue)
            return Visibility.Visible;
        else
            return UseHidden ? Visibility.Hidden : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool result = visibility == Visibility.Visible;
            return Invert ? !result : result;
        }

        return false;
    }
}
