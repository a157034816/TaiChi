using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 任意对象到可见性的转换器：非空 => Visible，空 => Collapsed（可配置为 Hidden 或反转）
/// </summary>
public class ObjectToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 是否反转（空 => Visible，非空 => Collapsed/Hidden）
    /// </summary>
    public bool Invert { get; set; }

    /// <summary>
    /// 隐藏时使用 Hidden 而不是 Collapsed
    /// </summary>
    public bool UseHidden { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNonNull = value != null;
        if (Invert) isNonNull = !isNonNull;
        if (isNonNull)
            return Visibility.Visible;
        return UseHidden ? Visibility.Hidden : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            bool visible = v == Visibility.Visible;
            return Invert ? !visible : visible;
        }
        return Invert; // 默认返回与 Invert 相反的逻辑无意义，这里简单处理
    }
}

