using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TaiChi.Wpf.NodeEditor.Core.Models;

/// <summary>
/// 连接类，代表两个引脚之间的连接线，表示数据的流向
/// </summary>
public class Connection : INotifyPropertyChanged
{
    private Pin? _sourcePin;
    private Pin? _targetPin;
    private bool _isActive;
    private bool _isSelected = false;

    /// <summary>
    /// 连接的唯一标识符
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 连接的起始引脚（必须是 PinDirection.Output）
    /// </summary>
    [JsonIgnore]
    public Pin? SourcePin
    {
        get => _sourcePin;
        set
        {
            if (_sourcePin != value)
            {
                // 移除旧连接
                if (_sourcePin != null)
                {
                    _sourcePin.Connection = null;
                    _sourcePin.ValueChanged -= OnSourceValueChanged;
                }

                _sourcePin = value;

                // 建立新连接
                if (_sourcePin != null)
                {
                    _sourcePin.Connection = this;
                    _sourcePin.ValueChanged += OnSourceValueChanged;
                    
                    // 立即传递当前值
                    TransferData();
                }

                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 连接的目标引脚（必须是 PinDirection.Input）
    /// </summary>
    [JsonIgnore]
    public Pin? TargetPin
    {
        get => _targetPin;
        set
        {
            if (_targetPin != value)
            {
                // 移除旧连接
                if (_targetPin != null)
                {
                    _targetPin.Connection = null;
                }

                _targetPin = value;

                // 建立新连接
                if (_targetPin != null)
                {
                    _targetPin.Connection = this;
                    
                    // 立即传递当前值
                    TransferData();
                }

                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 源引脚的ID（用于序列化）
    /// </summary>
    public Guid SourcePinId { get; set; }

    /// <summary>
    /// 目标引脚的ID（用于序列化）
    /// </summary>
    public Guid TargetPinId { get; set; }

    /// <summary>
    /// 指示数据是否正在通过此连接流动
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 连接是否被选中
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 属性变化通知事件
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 构造函数
    /// </summary>
    public Connection()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sourcePin">源引脚</param>
    /// <param name="targetPin">目标引脚</param>
    public Connection(Pin sourcePin, Pin targetPin)
    {
        SourcePin = sourcePin;
        TargetPin = targetPin;
        SourcePinId = sourcePin.Id;
        TargetPinId = targetPin.Id;
    }

    /// <summary>
    /// 当源引脚值发生变化时的处理
    /// </summary>
    private void OnSourceValueChanged(object? sender, EventArgs e)
    {
        TransferData();
    }

    /// <summary>
    /// 传递数据从源引脚到目标引脚
    /// </summary>
    private void TransferData()
    {
        if (SourcePin != null && TargetPin != null)
        {
            IsActive = true;
            
            try
            {
                // 传递数据
                TargetPin.Value = SourcePin.Value;
            }
            catch (Exception)
            {
                // 数据传递失败，可以在这里记录日志或处理错误
                IsActive = false;
            }
            finally
            {
                // 数据传递完成后，重置活动状态
                Task.Delay(100).ContinueWith(_ => IsActive = false);
            }
        }
    }

    /// <summary>
    /// 验证连接是否有效
    /// </summary>
    /// <returns>如果连接有效返回true，否则返回false</returns>
    public bool IsValid()
    {
        if (SourcePin == null || TargetPin == null)
            return false;

        return SourcePin.CanConnectTo(TargetPin);
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        if (SourcePin != null)
        {
            SourcePin.ValueChanged -= OnSourceValueChanged;
            SourcePin.Connection = null;
        }

        if (TargetPin != null)
        {
            TargetPin.Connection = null;
        }

        _sourcePin = null;
        _targetPin = null;
    }

    /// <summary>
    /// 触发属性变化通知
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 析构函数，确保连接被正确断开
    /// </summary>
    ~Connection()
    {
        Disconnect();
    }
}
