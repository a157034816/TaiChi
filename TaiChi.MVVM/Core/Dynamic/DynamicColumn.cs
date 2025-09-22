using System;

namespace TaiChi.Mvvm.Core.Dynamic
{
    /// <summary>
    /// 动态字段
    /// </summary>
    public class DynamicColumn
    {
        public string Name { get; set; }
        public Type DataType { get; set; }
        public object DefaultValue { get; set; }
    }
}