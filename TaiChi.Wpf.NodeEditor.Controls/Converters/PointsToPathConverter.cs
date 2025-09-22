using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 点到路径的转换器，用于创建贝塞尔曲线连接线
/// </summary>
public class PointsToPathConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is Point source && values[1] is Point target)
        {
            return CreateBezierPath(source, target);
        }
        
        return new PathGeometry();
    }

    /// <summary>
    /// 创建贝塞尔曲线路径
    /// </summary>
    private static PathGeometry CreateBezierPath(Point source, Point target)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = source };

        // 计算控制点
        var deltaX = Math.Abs(target.X - source.X);
        var controlOffset = Math.Max(deltaX * 0.5, 50);

        var control1 = new Point(source.X + controlOffset, source.Y);
        var control2 = new Point(target.X - controlOffset, target.Y);

        var bezier = new BezierSegment(control1, control2, target, true);
        figure.Segments.Add(bezier);
        geometry.Figures.Add(figure);

        return geometry;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
