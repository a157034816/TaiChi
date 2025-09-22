using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 根据 IsFlowPin 与 Connection 共同决定输入值编辑控件的可见性：
/// - 非流程引脚 且 未连接 => Visible
/// - 其他情况 => Collapsed
/// </summary>
public class PinInputValueVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return Visibility.Collapsed;

        var isFlowPin = values[0] is bool b && b;
        var connectionIsNull = values[1] is null;

        return (!isFlowPin && connectionIsNull) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

