using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 判断一个 Type 是否为 bool
/// </summary>
public class TypeIsBooleanConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isBool = false;
        if (value is Type t)
        {
            isBool = t == typeof(bool) || t == typeof(bool?);
        }

        if (Invert) isBool = !isBool;

        return isBool ? Visibility.Visible : Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
