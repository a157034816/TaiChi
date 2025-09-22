using System;
using TaiChi.MVVM.Core;

namespace TaiChi.Mvvm.Core.Dynamic
{
    public class DynamicProperty : TaiChiObservableObject
    {
        private object _value;
        private bool _isChanged;
        private object _originValue;

        public object Value
        {
            get => _value;
            set
            {
                var oldValue = _value;
                if (SetProperty(ref _value, value))
                {
                    ValueChanged(value, oldValue);
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

        public virtual void Set(object value)
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
    }
}