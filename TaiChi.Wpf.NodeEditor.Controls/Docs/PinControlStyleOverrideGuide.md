# PinControl Style 覆盖使用指南

## 概述

PinControl 已经重构为支持易于样式覆盖的架构。外部项目可以通过多种方式覆盖和自定义 PinControl 的样式。

## 快速开始

### 1. 基础样式覆盖

在您的 App.xaml 或窗口资源中，重定义 PinControl 的基础样式：

```xml
<Application.Resources>
    <!-- 引入库的资源 -->
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/TaiChi.Wpf.NodeEditor.Controls;component/Themes/Colors.xaml" />
            <ResourceDictionary Source="pack://application:,,,/TaiChi.Wpf.NodeEditor.Controls;component/Themes/Dimensions.xaml" />
            <ResourceDictionary Source="pack://application:,,,/TaiChi.Wpf.NodeEditor.Controls;component/Themes/PinControlStyles.xaml" />
        </ResourceDictionary.MergedDictionaries>
        
        <!-- 自定义主题颜色 -->
        <SolidColorBrush x:Key="PinControl_PrimaryBrush" Color="#2196F3" />
        <SolidColorBrush x:Key="PinControl_HighlightBrush" Color="#FFC107" />
        <SolidColorBrush x:Key="PinControl_ConnectingBrush" Color="#4CAF50" />
        
        <!-- 覆盖基础样式 -->
        <Style x:Key="{x:Static controls:PinControl.PinStyleKey}" 
               TargetType="{x:Type controls:PinControl}"
               BasedOn="{StaticResource {x:Static controls:PinControl.PinStyleKey}}">
            <Setter Property="Background" Value="LightGray" />
            <Setter Property="BorderBrush" Value="{StaticResource PinControl_PrimaryBrush}" />
            <Setter Property="BorderThickness" Value="2" />
            <Setter Property="Padding" Value="4,2" />
        </Style>
        
    </ResourceDictionary>
</Application.Resources>
```

### 2. 输入/输出引脚分别覆盖

```xml
<Window.Resources>
    <!-- 输入引脚特定样式 -->
    <Style x:Key="{x:Static controls:PinControl.InputPinStyleKey}" 
           TargetType="{x:Type controls:PinControl}"
           BasedOn="{StaticResource {x:Static controls:PinControl.PinStyleKey}}">
        <Setter Property="BorderBrush" Value="Green" />
        <Setter Property="Margin" Value="0,2,0,0" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type controls:PinControl}">
                    <!-- 自定义输入引脚模板 -->
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="3">
                        <StackPanel Orientation="Horizontal">
                            <Ellipse Width="8" Height="8" 
                                     Fill="Green" 
                                     Margin="2"/>
                            <TextBlock Text="{Binding PinData.Name, RelativeSource={RelativeSource TemplatedParent}}"
                                       VerticalAlignment="Center"
                                       FontSize="10"
                                       Margin="5,0,0,0" />
                        </StackPanel>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    
    <!-- 输出引脚特定样式 -->
    <Style x:Key="{x:Static controls:PinControl.OutputPinStyleKey}" 
           TargetType="{x:Type controls:PinControl}"
           BasedOn="{StaticResource {x:Static controls:PinControl.PinStyleKey}}">
        <Setter Property="BorderBrush" Value="Red" />
        <Setter Property="Margin" Value="0,0,0,2" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type controls:PinControl}">
                    <!-- 自定义输出引脚模板 -->
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="3">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="{Binding PinData.Name, RelativeSource={RelativeSource TemplatedParent}}"
                                       VerticalAlignment="Center"
                                       FontSize="10"
                                       Margin="0,0,5,0"
                                       HorizontalAlignment="Right" />
                            <Ellipse Width="8" Height="8" 
                                     Fill="Red" 
                                     Margin="2"/>
                        </StackPanel>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</Window.Resources>
```

### 3. 通过资源字典实现主题化

#### 创建自定义主题文件

**DarkTheme.xaml**
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="clr-namespace:TaiChi.Wpf.NodeEditor.Controls">
    
    <!-- 颜色覆盖 -->
    <Color x:Key="PinControl_PrimaryColor">#BB86FC</Color>
    <Color x:Key="PinControl_HighlightColor">#FFC107</Color>
    <Color x:Key="PinControl_ConnectingColor">#4CAF50</Color>
    <Color x:Key="PinControl_SelectedColor">#CF6679</Color>
    <Color x:Key="PinControl_ActiveColor">#03DAC6</Color>
    <Color x:Key="PinControl_NormalColor">#FFFFFF</Color>
    <Color x:Key="PinControl_BackgroundColor">#121212</Color>
    
    <!-- 画刷覆盖 -->
    <SolidColorBrush x:Key="PinControl_PrimaryBrush" Color="{StaticResource PinControl_PrimaryColor}" />
    <SolidColorBrush x:Key="PinControl_HighlightBrush" Color="{StaticResource PinControl_HighlightColor}" />
    <SolidColorBrush x:Key="PinControl_ConnectingBrush" Color="{StaticResource PinControl_ConnectingColor}" />
    <SolidColorBrush x:Key="PinControl_SelectedBrush" Color="{StaticResource PinControl_SelectedColor}" />
    <SolidColorBrush x:Key="PinControl_ActiveBrush" Color="{StaticResource PinControl_ActiveColor}" />
    <SolidColorBrush x:Key="PinControl_Foreground" Color="{StaticResource PinControl_NormalColor}" />
    <SolidColorBrush x:Key="PinControl_PinBackground" Color="{StaticResource PinControl_BackgroundColor}" />
    <SolidColorBrush x:Key="PinControl_PinBorder" Color="{StaticResource PinControl_PrimaryColor}" />
    
    <!-- 尺寸覆盖 -->
    <sys:Double x:Key="PinControl_FontSize_Normal">12</sys:Double>
    <Thickness x:Key="PinControl_Margin">0,4</Thickness>
    <Thickness x:Key="PinControl_BorderThickness">1,1,1,1</Thickness>
    
    <!-- 效果覆盖 -->
    <sys:Double x:Key="PinControl_ShadowBlurRadius">6</sys:Double>
    <sys:Double x:Key="PinControl_GlowBlurRadius">8</sys:Double>
    
</ResourceDictionary>
```

**LightTheme.xaml**
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="clr-namespace:TaiChi.Wpf.NodeEditor.Controls"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    
    <!-- 颜色覆盖 -->
    <Color x:Key="PinControl_PrimaryColor">#2196F3</Color>
    <Color x:Key="PinControl_HighlightColor">#FFC107</Color>
    <Color x:Key="PinControl_ConnectingColor">#4CAF50</Color>
    <Color x:Key="PinControl_SelectedColor">#F44336</Color>
    <Color x:Key="PinControl_ActiveColor">#009688</Color>
    <Color x:Key="PinControl_NormalColor">#000000</Color>
    <Color x:Key="PinControl_BackgroundColor">#FFFFFF</Color>
    
    <!-- 画刷覆盖 -->
    <SolidColorBrush x:Key="PinControl_PrimaryBrush" Color="{StaticResource PinControl_PrimaryColor}" />
    <SolidColorBrush x:Key="PinControl_HighlightBrush" Color="{StaticResource PinControl_HighlightColor}" />
    <SolidColorBrush x:Key="PinControl_ConnectingBrush" Color="{StaticResource PinControl_ConnectingColor}" />
    <SolidColorBrush x:Key="PinControl_SelectedBrush" Color="{StaticResource PinControl_SelectedColor}" />
    <SolidColorBrush x:Key="PinControl_ActiveBrush" Color="{StaticResource PinControl_ActiveColor}" />
    <SolidColorBrush x:Key="PinControl_Foreground" Color="{StaticResource PinControl_NormalColor}" />
    <SolidColorBrush x:Key="PinControl_PinBackground" Color="{StaticResource PinControl_BackgroundColor}" />
    <SolidColorBrush x:Key="PinControl_PinBorder" Color="{StaticResource PinControl_PrimaryColor}" />
    
</ResourceDictionary>
```

#### 在代码中切换主题

```csharp
// 在窗口或应用中定义主题切换方法
public static void ApplyTheme(ResourceDictionary theme)
{
    // 获取当前应用的资源字典
    var appResources = Application.Current.Resources;
    
    // 移除旧的主题资源
    var oldThemeResources = appResources.MergedDictionaries
        .Where(d => d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"))
        .ToList();
    
    foreach (var oldResource in oldThemeResources)
    {
        appResources.MergedDictionaries.Remove(oldResource);
    }
    
    // 添加新的主题资源
    appResources.MergedDictionaries.Add(theme);
    
    // 强制重绘所有 PinControl
    UpdateAllPinControls();
}

private static void UpdateAllPinControls()
{
    // 查找所有 PinControl 实例并更新其样式
    var pinControls = FindVisualChildren<PinControl>(Application.Current.MainWindow);
    foreach (var pinControl in pinControls)
    {
        pinControl.InvalidateVisual();
    }
}

// 查找视觉树中所有指定类型的子元素
public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
{
    var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
    for (var i = 0; i < childrenCount; i++)
    {
        var child = VisualTreeHelper.GetChild(parent, i);
        if (child is T)
        {
            yield return (T)child;
        }
        
        foreach (var grandChild in FindVisualChildren<T>(child))
        {
            yield return grandChild;
        }
    }
}
```

## 高级自定义

### 1. 完全自定义模板

```xml
<Style x:Key="CustomPinControlStyle" 
       TargetType="{x:Type controls:PinControl}"
       BasedOn="{StaticResource {x:Static controls:PinControl.PinStyleKey}}">
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="{x:Type controls:PinControl}">
                <Grid>
                    <!-- 背景 -->
                    <Border x:Name="PART_Background"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="5"
                            Opacity="0.8"/>
                    
                    <!-- 内容 -->
                    <Grid Margin="4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        
                        <!-- 引脚类型图标 -->
                        <Border Grid.Column="0" 
                                Width="16" Height="16"
                                CornerRadius="8"
                                Background="{Binding PinData.DataType.Name, 
                                              RelativeSource={RelativeSource TemplatedParent},
                                              Converter={StaticResource DataTypeToBrushConverter}}"/>
                        
                        <!-- 引脚名称 -->
                        <TextBlock Grid.Column="1" 
                                   Text="{Binding PinData.Name, RelativeSource={RelativeSource TemplatedParent}}"
                                   VerticalAlignment="Center"
                                   FontSize="11"
                                   FontWeight="Medium"
                                   Margin="8,0,0,0"
                                   Foreground="{TemplateBinding Foreground}"/>
                    </Grid>
                    
                    <!-- 状态指示器 -->
                    <Ellipse x:Name="PART_StatusIndicator"
                             Width="6" Height="6"
                             Stroke="White"
                             StrokeThickness="1"
                             Fill="Transparent"
                             HorizontalAlignment="Right"
                             VerticalAlignment="Top"
                             Margin="2"/>
                </Grid>
                
                <ControlTemplate.Triggers>
                    <!-- 高亮状态 -->
                    <Trigger Property="IsHighlighted" Value="True">
                        <Setter TargetName="PART_Background" Property="Opacity" Value="1.0" />
                        <Setter TargetName="PART_StatusIndicator" Property="Fill" Value="Yellow" />
                    </Trigger>
                    
                    <!-- 连接状态 -->
                    <Trigger Property="IsConnecting" Value="True">
                        <Setter TargetName="PART_StatusIndicator" Property="Fill" Value="Lime" />
                        <Setter TargetName="PART_Background" Property="BorderBrush" Value="Green" />
                    </Trigger>
                    
                    <!-- 已连接状态 -->
                    <DataTrigger Binding="{Binding PinData.IsConnected, RelativeSource={RelativeSource TemplatedParent}}" Value="True">
                        <Setter TargetName="PART_StatusIndicator" Property="Fill" Value="Blue" />
                    </DataTrigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

### 2. 数据类型颜色映射

```csharp
// 创建数据类型到颜色的转换器
public class DataTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string dataType)
        {
            return dataType switch
            {
                "Int32" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),   // 蓝色
                "Double" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),    // 绿色
                "String" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),     // 红色
                "Boolean" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),  // 黄色
                "Object" => new SolidColorBrush(Color.FromRgb(156, 39, 176)),   // 紫色
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))    // 灰色
            };
        }
        
        return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // 默认灰色
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

## 性能优化建议

### 1. 资源字典优化

- 将常用样式和资源放在应用级别，避免重复定义
- 使用 `{x:Static}` 引用静态资源键，减少查找时间
- 对于大量 PinControl，考虑使用虚拟化面板

### 2. 模板优化

- 避免在模板中使用复杂的布局结构
- 使用简单的几何形状代替复杂的路径
- 合理使用 VisualStateManager 管理状态变化

### 3. 主题切换优化

- 使用 ResourceDictionary 的合并机制，避免完全重建资源
- 考虑使用 Freezable 对象缓存常用画刷
- 实现延迟加载大型资源字典

## 故障排除

### 常见问题

1. **样式不生效**
   - 确保资源字典已正确引入
   - 检查样式键是否正确引用
   - 验证 BasedOn 属性设置正确

2. **主题切换闪烁**
   - 使用 BeginInit/EndInit 包装资源字典操作
   - 考虑使用双缓冲技术
   - 减少重绘范围

3. **性能问题**
   - 检查是否有大量的样式触发器
   - 避免在模板中使用复杂的转换器
   - 使用性能分析工具识别瓶颈

## 总结

通过本指南，您可以：

1. **简单覆盖**：通过资源字典重定义实现快速主题化
2. **精确控制**：使用样式键实现精细的样式覆盖
3. **完整自定义**：通过完全自定义模板实现独特外观
4. **动态主题**：实现运行时主题切换功能

这种架构设计既保持了向后兼容性，又提供了最大程度的灵活性，让外部项目能够轻松地集成和自定义 PinControl 的样式。
## 输入引脚未连接时显示编辑控件（按 DataType 选择）

控件库已为 Input Pin 提供“未连接时按数据类型显示编辑控件”的内置支持：

- string 类型：显示 TextBox，并双向绑定到 Pin.Value
- 其他类型：回退到一个占位文本（可自定义）

### 如何自定义第三方控件

通过实现并替换选择器接口 IPinInputValueTemplateSelector，即可在外部项目中使用任意第三方控件（如数值输入、开关、下拉等）。

1) 在资源中提供对应模板（示例）：

`xml
<DataTemplate x:Key="MyStringTemplate">
    <!-- 第三方文本框 -->
    <third:BetterTextBox Text="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
  </DataTemplate>
`

2) 在外部项目中创建自定义选择器（继承 DataTemplateSelector 并实现 IPinInputValueTemplateSelector）：

`csharp
public sealed class MyPinInputValueTemplateSelector : DataTemplateSelector, IPinInputValueTemplateSelector
{
    public DataTemplate DefaultTemplate { get; set; }
    public DataTemplate MyStringTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is Pin pin && pin.DataType == typeof(string))
            return MyStringTemplate;
        return GetDefaultTemplate();
    }

    public DataTemplate GetDefaultTemplate() => DefaultTemplate;
}
`

3) 将选择器应用到 PinControl（可全局样式覆盖）：

`xml
<ResourceDictionary>
  <local:MyPinInputValueTemplateSelector x:Key="MyPinInputValueTemplateSelector"
                                         DefaultTemplate="{StaticResource DefaultPinInputValueTemplate}"
                                         MyStringTemplate="{StaticResource MyStringTemplate}" />

  <Style TargetType="{x:Type controls:PinControl}"
         BasedOn="{StaticResource {x:Static controls:PinControl.PinStyleKey}}">
    <Setter Property="PinInputValueTemplateSelector" Value="{StaticResource MyPinInputValueTemplateSelector}" />
  </Style>
</ResourceDictionary>
`

说明：
- 当 Input Pin 没有任何连接（Pin.Connection == null）时，模板选择器生效显示编辑控件；一旦连接建立则自动切换回显示引脚名称标签。
- 若不设置选择器，库内置的 DefaultPinInputValueTemplateSelector 会按上述默认策略工作。
