using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// Point到X坐标的转换器：提取Point.X
/// </summary>
public class PointToXConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Point point)
        {
            return point.X;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
