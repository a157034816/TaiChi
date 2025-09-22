using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 字符串到可见性的转换器：非空字符串 => Visible，空字符串 => Collapsed
/// </summary>
public class StringToVisibilityConverter : IValueConverter
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
        bool hasValue = !string.IsNullOrEmpty(value?.ToString());
        
        if (Invert)
            hasValue = !hasValue;

        if (hasValue)
            return Visibility.Visible;
        else
            return UseHidden ? Visibility.Hidden : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool isVisible = visibility == Visibility.Visible;
            if (Invert)
                isVisible = !isVisible;
            return isVisible ? "Visible" : string.Empty;
        }
        return string.Empty;
    }
}
