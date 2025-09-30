using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;

namespace TaiChi.Wpf.NodeEditor.Controls.Helpers;

/// <summary>
/// 通过绑定 Target 对象和 PropertyName 提供一个可双向绑定的 Value 属性。
/// 用于在 XAML 中将属性名（字符串）动态绑定到实际对象的属性上。
/// </summary>
public class PropertyBindingProxy : Freezable
{
    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(nameof(Target), typeof(object), typeof(PropertyBindingProxy),
            new PropertyMetadata(null, OnTargetOrPropertyChanged));

    public static readonly DependencyProperty PropertyNameProperty =
        DependencyProperty.Register(nameof(PropertyName), typeof(string), typeof(PropertyBindingProxy),
            new PropertyMetadata(string.Empty, OnTargetOrPropertyChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(object), typeof(PropertyBindingProxy),
            new PropertyMetadata(null, OnValueChanged));

    private bool _updating;

    public object? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public string PropertyName
    {
        get => (string)GetValue(PropertyNameProperty);
        set => SetValue(PropertyNameProperty, value);
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnTargetOrPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PropertyBindingProxy proxy)
        {
            proxy.UpdateValueFromTarget();
        }
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PropertyBindingProxy proxy)
        {
            proxy.UpdateTargetFromValue(e.NewValue);
        }
    }

    private void UpdateValueFromTarget()
    {
        if (_updating) return;
        var target = Target;
        var name = PropertyName;
        if (target == null || string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanRead)
            {
                _updating = true;
                SetCurrentValue(ValueProperty, prop.GetValue(target));
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdateTargetFromValue(object? newValue)
    {
        if (_updating) return;
        var target = Target;
        var name = PropertyName;
        if (target == null || string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                var converted = ConvertToType(newValue, prop.PropertyType);
                _updating = true;
                prop.SetValue(target, converted);
            }
        }
        catch
        {
            // ignore conversion errors
        }
        finally
        {
            _updating = false;
        }
    }

    private static object? ConvertToType(object? value, Type targetType)
    {
        if (value == null) return null;

        if (targetType.IsInstanceOfType(value))
            return value;

        // Unwrap string and attempt conversions
        if (value is string s)
        {
            if (targetType == typeof(string)) return s;
            if (targetType == typeof(bool) && bool.TryParse(s, out var b)) return b;
            if (targetType == typeof(char)) return s.Length > 0 ? s[0] : '\0';
            if (targetType == typeof(Guid) && Guid.TryParse(s, out var g)) return g;
            if (targetType == typeof(TimeSpan) && TimeSpan.TryParse(s, out var ts)) return ts;
#if NET6_0_OR_GREATER
            if (targetType.FullName == "System.DateOnly")
            {
                // DateOnly.ParseExact 需要反射以避免在较老框架失败
                var t = targetType;
                var parse = t.GetMethod("Parse", new[] { typeof(string), typeof(IFormatProvider), typeof(DateTimeStyles) })
                            ?? t.GetMethod("Parse", new[] { typeof(string), typeof(IFormatProvider) })
                            ?? t.GetMethod("Parse", new[] { typeof(string) });
                if (parse != null)
                {
                    try { return parse.Invoke(null, new object[] { s, CultureInfo.CurrentCulture }); } catch { }
                }
            }
            if (targetType.FullName == "System.TimeOnly")
            {
                var t = targetType;
                var parse = t.GetMethod("Parse", new[] { typeof(string), typeof(IFormatProvider), typeof(DateTimeStyles) })
                            ?? t.GetMethod("Parse", new[] { typeof(string), typeof(IFormatProvider) })
                            ?? t.GetMethod("Parse", new[] { typeof(string) });
                if (parse != null)
                {
                    try { return parse.Invoke(null, new object[] { s, CultureInfo.CurrentCulture }); } catch { }
                }
            }
#endif
        }

        try
        {
            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter != null && converter.CanConvertFrom(value.GetType()))
            {
                return converter.ConvertFrom(null, CultureInfo.CurrentCulture, value);
            }
        }
        catch { }

        try
        {
            return System.Convert.ChangeType(value, targetType, CultureInfo.CurrentCulture);
        }
        catch { return value; }
    }

    protected override Freezable CreateInstanceCore()
    {
        return new PropertyBindingProxy();
    }
}

