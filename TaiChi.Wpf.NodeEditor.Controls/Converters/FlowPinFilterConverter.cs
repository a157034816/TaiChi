using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Controls.Converters;

/// <summary>
/// 流程引脚过滤转换器，用于分离流程引脚和数据引脚
/// </summary>
public class FlowPinFilterConverter : IValueConverter
{
    /// <summary>
    /// 转换方法
    /// </summary>
    /// <param name="value">Pin集合</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">参数：true表示过滤流程引脚，false表示过滤数据引脚</param>
    /// <param name="culture">文化信息</param>
    /// <returns>过滤后的Pin集合</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<Pin> pins)
            return new ObservableCollection<Pin>();

        // 支持 bool 和 string 两种入参；string 比较不区分大小写
        bool filterFlowPins = parameter?.ToString() == "true";

        // 根据parameter参数决定过滤逻辑
        // true: 只返回流程引脚
        // false: 只返回数据引脚
        var filteredPins = pins.Where(pin => pin.IsFlowPin == filterFlowPins).ToList();

        // 调试输出
        System.Diagnostics.Debug.WriteLine($"FlowPinFilterConverter: filterFlowPins={filterFlowPins}, total pins={pins.Count()}, filtered={filteredPins.Count}");
        foreach (var pin in pins)
        {
            System.Diagnostics.Debug.WriteLine($"  Pin: {pin.Name}, IsFlowPin={pin.IsFlowPin}, Direction={pin.Direction}");
        }

        return new ObservableCollection<Pin>(filteredPins);
    }

    /// <summary>
    /// 反向转换方法（不支持）
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException("FlowPinFilterConverter does not support ConvertBack");
    }
}
