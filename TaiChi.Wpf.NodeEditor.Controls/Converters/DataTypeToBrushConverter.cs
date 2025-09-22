using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 数据类型到画刷的转换器
/// </summary>
public class DataTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Type dataType)
        {
            return dataType.Name switch
            {
                nameof(Int32) => Brushes.Blue,
                nameof(Double) => Brushes.Green,
                nameof(String) => Brushes.Red,
                nameof(Boolean) => Brushes.Orange,
                _ => Brushes.Gray
            };
        }
        
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
