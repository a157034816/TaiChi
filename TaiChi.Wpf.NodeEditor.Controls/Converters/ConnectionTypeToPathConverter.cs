using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TaiChi.Wpf.NodeEditor.Controls.Helpers;
using TaiChi.Wpf.NodeEditor.Core.Enums;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 连接类型到路径几何图形的转换器
/// </summary>
public class ConnectionTypeToPathConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 || 
            values[0] is not Point startPoint || 
            values[1] is not Point endPoint ||
            values[2] is not ConnectionType connectionType)
        {
            return GeometryHelper.CreateBezierPath(new Point(0, 0), new Point(100, 100));
        }

        return connectionType switch
        {
            ConnectionType.Straight => GeometryHelper.CreateStraightPath(startPoint, endPoint),
            ConnectionType.Bezier => GeometryHelper.CreateBezierPath(startPoint, endPoint),
            ConnectionType.Orthogonal => GeometryHelper.CreateOrthogonalPath(startPoint, endPoint),
            ConnectionType.Arc => GeometryHelper.CreateArcPath(startPoint, endPoint),
            _ => GeometryHelper.CreateBezierPath(startPoint, endPoint)
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 节点状态到颜色的转换器
/// </summary>
public class NodeStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NodeState state)
        {
            return state switch
            {
                NodeState.Normal => Brushes.Gray,
                NodeState.Executing => Brushes.Yellow,
                NodeState.Success => Brushes.Green,
                NodeState.Error => Brushes.Red,
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

/// <summary>
/// 缩放级别到字体大小的转换器
/// </summary>
public class ZoomToFontSizeConverter : IValueConverter
{
    public double BaseFontSize { get; set; } = 12.0;
    public double MinFontSize { get; set; } = 8.0;
    public double MaxFontSize { get; set; } = 24.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double zoomLevel)
        {
            var fontSize = BaseFontSize * zoomLevel;
            return Math.Max(MinFontSize, Math.Min(MaxFontSize, fontSize));
        }
        
        return BaseFontSize;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double fontSize)
        {
            return fontSize / BaseFontSize;
        }
        
        return 1.0;
    }
}

/// <summary>
/// 集合计数到可见性的转换器
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public bool InvertVisibility { get; set; } = false;
    public int Threshold { get; set; } = 0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = 0;
        
        if (value is int intValue)
            count = intValue;
        else if (value is System.Collections.ICollection collection)
            count = collection.Count;
        else if (value is System.Collections.IEnumerable enumerable)
            count = enumerable.Cast<object>().Count();

        var isVisible = count > Threshold;
        
        if (InvertVisibility)
            isVisible = !isVisible;
            
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 多重布尔值到可见性的转换器
/// </summary>
public class MultiBooleanToVisibilityConverter : IMultiValueConverter
{
    public bool UseAndLogic { get; set; } = true;
    public bool InvertResult { get; set; } = false;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0)
            return Visibility.Collapsed;

        bool result;
        
        if (UseAndLogic)
        {
            result = values.All(v => v is bool b && b);
        }
        else
        {
            result = values.Any(v => v is bool b && b);
        }

        if (InvertResult)
            result = !result;

        return result ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 矩形到路径几何图形的转换器
/// </summary>
public class RectToPathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Rect rect)
        {
            var geometry = new RectangleGeometry(rect);
            return geometry;
        }
        
        return new RectangleGeometry(Rect.Empty);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 数值范围限制转换器
/// </summary>
public class ClampValueConverter : IValueConverter
{
    public double MinValue { get; set; } = double.MinValue;
    public double MaxValue { get; set; } = double.MaxValue;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return Math.Max(MinValue, Math.Min(MaxValue, doubleValue));
        }
        
        if (value is int intValue)
        {
            return Math.Max((int)MinValue, Math.Min((int)MaxValue, intValue));
        }
        
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Convert(value, targetType, parameter, culture);
    }
}

/// <summary>
/// 数值到百分比文本的转换器
/// </summary>
public class PercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return $"{Math.Round(doubleValue * 100)}%";
        }

        if (value is float floatValue)
        {
            return $"{Math.Round(floatValue * 100)}%";
        }

        return "100%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue && stringValue.EndsWith("%"))
        {
            var percentText = stringValue.Substring(0, stringValue.Length - 1);
            if (double.TryParse(percentText, out var percent))
            {
                return percent / 100.0;
            }
        }

        return 1.0;
    }
}
