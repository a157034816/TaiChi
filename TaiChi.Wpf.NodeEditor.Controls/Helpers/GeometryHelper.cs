using System;
using System.Windows;
using System.Windows.Media;

namespace TaiChi.Wpf.NodeEditor.Controls.Helpers;

/// <summary>
/// 几何图形辅助类
/// </summary>
public static class GeometryHelper
{
    /// <summary>
    /// 创建贝塞尔曲线路径
    /// </summary>
    /// <param name="startPoint">起始点</param>
    /// <param name="endPoint">结束点</param>
    /// <param name="curvature">曲率（0-1之间）</param>
    /// <returns>贝塞尔曲线几何图形</returns>
    public static PathGeometry CreateBezierPath(Point startPoint, Point endPoint, double curvature = 0.5)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = startPoint };

        // 计算控制点
        var deltaX = Math.Abs(endPoint.X - startPoint.X);
        var controlOffset = Math.Max(deltaX * curvature, 50);

        var control1 = new Point(startPoint.X + controlOffset, startPoint.Y);
        var control2 = new Point(endPoint.X - controlOffset, endPoint.Y);

        var bezier = new BezierSegment(control1, control2, endPoint, true);
        figure.Segments.Add(bezier);
        geometry.Figures.Add(figure);

        return geometry;
    }

    /// <summary>
    /// 创建直线路径
    /// </summary>
    /// <param name="startPoint">起始点</param>
    /// <param name="endPoint">结束点</param>
    /// <returns>直线几何图形</returns>
    public static PathGeometry CreateStraightPath(Point startPoint, Point endPoint)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = startPoint };
        
        var line = new LineSegment(endPoint, true);
        figure.Segments.Add(line);
        geometry.Figures.Add(figure);

        return geometry;
    }

    /// <summary>
    /// 创建直角连接路径
    /// </summary>
    /// <param name="startPoint">起始点</param>
    /// <param name="endPoint">结束点</param>
    /// <returns>直角连接几何图形</returns>
    public static PathGeometry CreateOrthogonalPath(Point startPoint, Point endPoint)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = startPoint };

        // 计算中间点
        var midX = startPoint.X + (endPoint.X - startPoint.X) / 2;
        var midPoint1 = new Point(midX, startPoint.Y);
        var midPoint2 = new Point(midX, endPoint.Y);

        figure.Segments.Add(new LineSegment(midPoint1, true));
        figure.Segments.Add(new LineSegment(midPoint2, true));
        figure.Segments.Add(new LineSegment(endPoint, true));

        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>
    /// 创建圆弧连接路径
    /// </summary>
    /// <param name="startPoint">起始点</param>
    /// <param name="endPoint">结束点</param>
    /// <returns>圆弧几何图形</returns>
    public static PathGeometry CreateArcPath(Point startPoint, Point endPoint)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = startPoint };

        var distance = Math.Sqrt(Math.Pow(endPoint.X - startPoint.X, 2) + Math.Pow(endPoint.Y - startPoint.Y, 2));
        var radius = distance / 2;

        var arc = new ArcSegment(
            endPoint,
            new Size(radius, radius),
            0,
            false,
            SweepDirection.Clockwise,
            true);

        figure.Segments.Add(arc);
        geometry.Figures.Add(figure);

        return geometry;
    }

    /// <summary>
    /// 计算两点之间的距离
    /// </summary>
    /// <param name="point1">点1</param>
    /// <param name="point2">点2</param>
    /// <returns>距离</returns>
    public static double CalculateDistance(Point point1, Point point2)
    {
        var deltaX = point2.X - point1.X;
        var deltaY = point2.Y - point1.Y;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    /// <summary>
    /// 计算点到线段的最短距离
    /// </summary>
    /// <param name="point">点</param>
    /// <param name="lineStart">线段起点</param>
    /// <param name="lineEnd">线段终点</param>
    /// <returns>最短距离</returns>
    public static double CalculateDistanceToLineSegment(Point point, Point lineStart, Point lineEnd)
    {
        var A = point.X - lineStart.X;
        var B = point.Y - lineStart.Y;
        var C = lineEnd.X - lineStart.X;
        var D = lineEnd.Y - lineStart.Y;

        var dot = A * C + B * D;
        var lenSq = C * C + D * D;

        if (lenSq == 0)
            return CalculateDistance(point, lineStart);

        var param = dot / lenSq;

        Point xx;
        if (param < 0)
        {
            xx = lineStart;
        }
        else if (param > 1)
        {
            xx = lineEnd;
        }
        else
        {
            xx = new Point(lineStart.X + param * C, lineStart.Y + param * D);
        }

        return CalculateDistance(point, xx);
    }

    /// <summary>
    /// 检查点是否在矩形内
    /// </summary>
    /// <param name="point">点</param>
    /// <param name="rect">矩形</param>
    /// <returns>如果在矩形内返回true</returns>
    public static bool IsPointInRectangle(Point point, Rect rect)
    {
        return point.X >= rect.Left && point.X <= rect.Right &&
               point.Y >= rect.Top && point.Y <= rect.Bottom;
    }

    /// <summary>
    /// 检查两个矩形是否相交
    /// </summary>
    /// <param name="rect1">矩形1</param>
    /// <param name="rect2">矩形2</param>
    /// <returns>如果相交返回true</returns>
    public static bool DoRectanglesIntersect(Rect rect1, Rect rect2)
    {
        return !(rect1.Right < rect2.Left || rect2.Right < rect1.Left ||
                 rect1.Bottom < rect2.Top || rect2.Bottom < rect1.Top);
    }

    /// <summary>
    /// 计算矩形的中心点
    /// </summary>
    /// <param name="rect">矩形</param>
    /// <returns>中心点</returns>
    public static Point GetRectangleCenter(Rect rect)
    {
        return new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
    }

    /// <summary>
    /// 将点限制在矩形范围内
    /// </summary>
    /// <param name="point">点</param>
    /// <param name="bounds">边界矩形</param>
    /// <returns>限制后的点</returns>
    public static Point ClampPointToRectangle(Point point, Rect bounds)
    {
        var x = Math.Max(bounds.Left, Math.Min(bounds.Right, point.X));
        var y = Math.Max(bounds.Top, Math.Min(bounds.Bottom, point.Y));
        return new Point(x, y);
    }
}
