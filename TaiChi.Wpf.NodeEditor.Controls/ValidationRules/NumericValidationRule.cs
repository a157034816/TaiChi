using System;
using System.Globalization;
using System.Windows.Controls;

namespace TaiChi.Wpf.NodeEditor.Controls.ValidationRules
{
    /// <summary>
    /// 整数类型验证规则（int）
    /// </summary>
    public class IntValidationRule : ValidationRule
    {
        /// <summary>
        /// 可选的最小/最大范围（含边界）。未设置时不进行范围限制。
        /// </summary>
        public int? Min { get; set; }
        public int? Max { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value == null)
                return new ValidationResult(false, "值不能为空");

            string stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
                return new ValidationResult(false, "值不能为空");

            if (int.TryParse(stringValue, NumberStyles.Integer, cultureInfo, out int result))
            {
                if (Min.HasValue && result < Min.Value)
                    return new ValidationResult(false, $"值必须 ≥ {Min.Value}");
                if (Max.HasValue && result > Max.Value)
                    return new ValidationResult(false, $"值必须 ≤ {Max.Value}");

                // 在int范围内即有效
                return ValidationResult.ValidResult;
            }

            return new ValidationResult(false, $"请输入有效的整数 ({int.MinValue} 到 {int.MaxValue})");
        }
    }

    /// <summary>
    /// 长整数类型验证规则（long）
    /// </summary>
    public class LongValidationRule : ValidationRule
    {
        public long? Min { get; set; }
        public long? Max { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value == null)
                return new ValidationResult(false, "值不能为空");

            string stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
                return new ValidationResult(false, "值不能为空");

            if (long.TryParse(stringValue, NumberStyles.Integer, cultureInfo, out long result))
            {
                if (Min.HasValue && result < Min.Value)
                    return new ValidationResult(false, $"值必须 ≥ {Min.Value}");
                if (Max.HasValue && result > Max.Value)
                    return new ValidationResult(false, $"值必须 ≤ {Max.Value}");

                return ValidationResult.ValidResult;
            }

            return new ValidationResult(false, $"请输入有效的长整数 ({long.MinValue} 到 {long.MaxValue})");
        }
    }

    /// <summary>
    /// 单精度浮点数验证规则（float）
    /// </summary>
    public class FloatValidationRule : ValidationRule
    {
        public float? Min { get; set; }
        public float? Max { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value == null)
                return new ValidationResult(false, "值不能为空");

            string stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
                return new ValidationResult(false, "值不能为空");

            if (float.TryParse(stringValue, NumberStyles.Float, cultureInfo, out float result))
            {
                if (float.IsNaN(result))
                    return new ValidationResult(false, "不能是NaN");
                if (float.IsInfinity(result))
                    return new ValidationResult(false, "数值过大或过小");

                if (Min.HasValue && result < Min.Value)
                    return new ValidationResult(false, $"值必须 ≥ {Min.Value}");
                if (Max.HasValue && result > Max.Value)
                    return new ValidationResult(false, $"值必须 ≤ {Max.Value}");

                return ValidationResult.ValidResult;
            }

            return new ValidationResult(false, "请输入有效的浮点数");
        }
    }

    /// <summary>
    /// 双精度浮点数验证规则（double）
    /// </summary>
    public class DoubleValidationRule : ValidationRule
    {
        public double? Min { get; set; }
        public double? Max { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value == null)
                return new ValidationResult(false, "值不能为空");

            string stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
                return new ValidationResult(false, "值不能为空");

            if (double.TryParse(stringValue, NumberStyles.Float, cultureInfo, out double result))
            {
                if (double.IsNaN(result))
                    return new ValidationResult(false, "不能是NaN");
                if (double.IsInfinity(result))
                    return new ValidationResult(false, "数值过大或过小");

                if (Min.HasValue && result < Min.Value)
                    return new ValidationResult(false, $"值必须 ≥ {Min.Value}");
                if (Max.HasValue && result > Max.Value)
                    return new ValidationResult(false, $"值必须 ≤ {Max.Value}");

                return ValidationResult.ValidResult;
            }

            return new ValidationResult(false, "请输入有效的双精度浮点数");
        }
    }

    /// <summary>
    /// 高精度浮点数验证规则（decimal）
    /// </summary>
    public class DecimalValidationRule : ValidationRule
    {
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value == null)
                return new ValidationResult(false, "值不能为空");

            string stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
                return new ValidationResult(false, "值不能为空");

            if (decimal.TryParse(stringValue, NumberStyles.Number, cultureInfo, out decimal result))
            {
                if (Min.HasValue && result < Min.Value)
                    return new ValidationResult(false, $"值必须 ≥ {Min.Value}");
                if (Max.HasValue && result > Max.Value)
                    return new ValidationResult(false, $"值必须 ≤ {Max.Value}");

                return ValidationResult.ValidResult;
            }

            return new ValidationResult(false, $"请输入有效的十进制数 ({decimal.MinValue} 到 {decimal.MaxValue})");
        }
    }
}
