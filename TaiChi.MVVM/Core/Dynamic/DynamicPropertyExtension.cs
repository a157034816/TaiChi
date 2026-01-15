using System;

namespace TaiChi.Mvvm.Core.Dynamic
{
    public static class DynamicPropertyExtension
    {
        public static void Hook(this DynamicProperty dp, Func<DynamicProperty, object?>? getter, Action<DynamicProperty, object?>? setter)
        {
            dp.ValueGetter = getter;
            dp.ValueSetter = setter;
        }
    }
}