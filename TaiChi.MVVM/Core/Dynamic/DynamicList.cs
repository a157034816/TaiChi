using System;
using System.Collections.Generic;
using System.Linq;

namespace TaiChi.Mvvm.Core.Dynamic
{
    public class DynamicList : List<DynamicObject>, IDisposable
    {
        public List<DynamicColumn> Columns { get; set; } = new List<DynamicColumn>();

        public void DynamicAdd(DynamicObject obj)
        {
            obj.Container = this;
            base.Add(obj);
        }

        public void DynamicRemove(DynamicObject obj)
        {
            obj.Container = null;
            base.Remove(obj);
        }

        public void Dispose()
        {
            Columns = null;
            foreach (var obj in this)
            {
                obj.Dispose();
            }

            this.Clear();
        }
    }

    public static class DynamicListExtension
    {
        public static void TryAddColumn(this DynamicList dynamicList, string columnName, Type dataType, object defaultValue = null)
        {
            if (dynamicList.Columns.Any(t => t.Name == columnName))
                return;

            dynamicList.Columns.Add(new DynamicColumn()
            {
                Name = columnName,
                DataType = dataType,
                DefaultValue = defaultValue,
            });
        }

        public static void TryAddColumn<T>(this DynamicList dynamicList, string columnName, object defaultValue = null)
        {
            TryAddColumn(dynamicList, columnName, typeof(T), defaultValue);
        }
    }
}