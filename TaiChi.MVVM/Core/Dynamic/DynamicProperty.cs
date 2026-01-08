using System;
using TaiChi.MVVM.Core;

namespace TaiChi.Mvvm.Core.Dynamic
{
    public class DynamicProperty : TaiChiObservableObject
    {
        private object _value;
        private bool _isChanged;
        private object _originValue;

        /// <summary>
        /// 接管 <see cref="Value"/> 的读取逻辑（可选）
        /// </summary>
        public Func<DynamicProperty, object?>? ValueGetter { get; set; }

        /// <summary>
        /// 接管 <see cref="Value"/> 的写入逻辑（可选）
        /// </summary>
        public Action<DynamicProperty, object?>? ValueSetter { get; set; }

        public object Value
        {
            get => ValueGetter != null ? ValueGetter.Invoke(this) : _value;
            set
            {
                if (ValueSetter != null)
                {
                    var oldValue = ValueGetter != null ? ValueGetter.Invoke(this) : _value;
                    if (Equals(oldValue, value))
                        return;

                    ValueSetter.Invoke(this, value);

                    var newValue = ValueGetter != null ? ValueGetter.Invoke(this) : value;
                    if (Equals(oldValue, newValue))
                    {
                        _value = newValue;
                        return;
                    }

                    if (Equals(_value, newValue))
                    {
                        OnPropertyChanging(nameof(Value));
                        OnPropertyChanged(nameof(Value));
                    }
                    else
                    {
                        SetProperty(ref _value, newValue);
                    }

                    ValueChanged(newValue, oldValue);
                    return;
                }

                var localOldValue = _value;
                if (SetProperty(ref _value, value))
                {
                    ValueChanged(value, localOldValue);
                }
            }
        }

        public bool IsChanged => _isChanged;

        public object OriginValue => _originValue;

        public DynamicProperty(object value)
        {
            _originValue = value;
            _value = value;
        }

        protected override void ReleaseManagedResources()
        {
            base.ReleaseManagedResources();
            _originValue = null;
            _value = null;
            ValueGetter = null;
            ValueSetter = null;
        }

        public virtual bool TryGet<T>(out T value)
        {
            if (Value is T v)
            {
                value = v;
                return true;
            }

            value = default;
            return false;
        }

        public virtual T Get<T>()
        {
            if (TryGet<T>(out var v))
                return v;

            if (Value == null)
                return default;

            throw new Exception($"类型不一致,期望一个:{typeof(T)},得到的是:{Value.GetType()}");
        }

        public virtual void Set(object? value)
        {
            this.Value = value;
        }

        protected virtual void ValueChanged(object newValue, object oldValue)
        {
            _isChanged = true;
        }

        /// <summary>
        /// 重置更改标志
        /// </summary>
        public void ResetChangedTag()
        {
            this._isChanged = false;
            this.Value = this.OriginValue;
        }

        /// <summary>
        /// 提交数据(避免后续Reset导致数据不受控)
        /// </summary>
        public void Submit()
        {
            this._originValue = this.Value;
            this._isChanged = false;
        }
    }
}
