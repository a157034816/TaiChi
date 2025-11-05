using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters
{
    /// <summary>
    /// 将引脚的数据类型转换为用于描边的画刷。
    /// 开发者可在外部覆盖此转换器资源以自定义配色或规则（建议在应用资源中以相同 x:Key 覆盖）。
    /// 默认实现基于填充色的同色系加深，便于和 Fill 保持一致又具备层次对比。
    /// </summary>
    public class DataTypeToStrokeBrushConverter : IValueConverter
    {
        /// <summary>
        /// 将 Type 转换为 Brush（描边）。
        /// </summary>
        /// <param name="value">应为 System.Type</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">可选参数：加深比例(0~1)，默认 0.25</param>
        /// <param name="culture">区域信息</param>
        /// <returns>对应颜色的 Brush（已加深）</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var type = value as Type;
            if (type == null)
            {
                return Brushes.Gray;
            }

            // 基础填充色（与 DataTypeToFillBrushConverter 保持一致的色系）
            var baseBrush = GetBaseFillBrush(type);
            var factor = 0.25;
            if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0 && parsed <= 1)
            {
                factor = parsed;
            }

            return Darken(baseBrush, factor);
        }

        /// <summary>
        /// 不支持从 Brush 反向转换为 Type。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 获取与类型对应的基础填充色。
        /// </summary>
        private static SolidColorBrush GetBaseFillBrush(Type type)
        {
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
        /// 将画刷颜色按比例加深。
        /// </summary>
        private static SolidColorBrush Darken(SolidColorBrush brush, double amount)
        {
            var c = brush.Color;
            byte Dark(byte v) => (byte)(v * (1 - amount));
            return new SolidColorBrush(Color.FromRgb(Dark(c.R), Dark(c.G), Dark(c.B)));
        }
    }
}
