using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters
{
    /// <summary>
    /// 将引脚的数据类型转换为用于填充的画刷。
    /// 开发者可在外部覆盖此转换器资源以自定义配色（建议在应用资源中以相同 x:Key 覆盖）。
    /// 默认映射：
    /// - 整数(Int16/Int32/Int64) -> Blue
    /// - 浮点/小数(Single/Double/Decimal) -> Green
    /// - 字符串(String) -> Pink
    /// - 布尔(Boolean) -> Orange
    /// - 其他 -> Gray
    /// </summary>
    public class DataTypeToFillBrushConverter : IValueConverter
    {
        /// <summary>
        /// 将 Type 转换为 Brush。
        /// </summary>
        /// <param name="value">应为 System.Type</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">可选参数（未使用）</param>
        /// <param name="culture">区域信息</param>
        /// <returns>对应颜色的 Brush</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = value as Type;
            if (type == null)
            {
                return Brushes.Gray;
            }

            switch (type.Name)
            {
                case nameof(Int16):
                case nameof(Int32):
                case nameof(Int64):
                    return Brushes.Blue;
                case nameof(Single):
                case nameof(Double):
                case nameof(Decimal):
                    return Brushes.Green;
                case nameof(String):
                    return Brushes.Pink;
                case nameof(Boolean):
                    return Brushes.Orange;
                default:
                    return Brushes.Gray;
            }
        }

        /// <summary>
        /// 不支持从 Brush 反向转换为 Type。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
