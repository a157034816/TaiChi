using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;

namespace TaiChi.Mvvm.Core.Dynamic
{
    /// <summary>
    /// 动态对象
    /// </summary>
    public class DynamicObject : System.Dynamic.DynamicObject, IDisposable
    {
        /// <summary>
        /// 属于哪个容器(如:DynamicList)
        /// </summary>
        public object? Container { get; set; }

        public object this[string key]
        {
            get => GetProperty(key)?.Value;
            set => SetProperty(key, value);
        }

        /// <summary>
        /// 获取动态属性
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public DynamicProperty? GetProperty(string name)
        {
            if (Container is DynamicList dynamicList)
            {
                var first = dynamicList.Columns.FirstOrDefault(t => t.Name == name);
                if (first != null) //集合中有字段
                {
                    if (!Properties.ContainsKey(name)) //属性里没有
                    {
                        var dynamicProperty = new DynamicProperty(first.DefaultValue);
                        Properties.TryAdd(name, dynamicProperty);
                        return dynamicProperty;
                    }
                }
            }

            return Properties.GetValueOrDefault(name);
            throw new Exception($"没有找到属性名为:[{name}]的属性");
        }

        /// <summary>
        /// 设置属性
        /// </summary>
        /// <param name="name"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public DynamicProperty SetProperty(string name, object obj)
        {
            if (!Properties.TryAdd(name, new DynamicProperty(obj)))
            {
                Properties[name].Set(obj);
            }

            return Properties[name];
        }

        /// <summary>
        /// 属性字典
        /// </summary>
        public Dictionary<string, DynamicProperty> Properties { get; set; } = new Dictionary<string, DynamicProperty>();

        public void Dispose()
        {
            Container = null;
            Properties = null;
        }

        public override bool TryGetMember(
            GetMemberBinder binder, out object result)
        {
            var tryGetMember = Properties.TryGetValue(binder.Name, out var dynamicProperty);
            result = tryGetMember ? dynamicProperty.Value : null;
            return tryGetMember;
        }

        public override bool TrySetMember(
            SetMemberBinder binder, object value)
        {
            var trySetMember = Properties.TryAdd(binder.Name, new DynamicProperty(value));
            if (!trySetMember)
            {
                Properties[binder.Name].Value = value;
            }

            return trySetMember;
        }
    }

    public static class DynamicObjectExtensions
    {
        public static DynamicList Create(string dynamicTypeName, DataRow row)
        {
            var dynamicList = new DynamicList();
            {
                dynamicList.Columns.AddRange(row.Table.Columns.Cast<DataColumn>().Select(t => new DynamicColumn()
                {
                    Name = t.ColumnName,
                    DataType = t.DataType,
                }).ToList());

                var dynamicObject = new DynamicObject();
                foreach (var dynamicColumn in dynamicList.Columns)
                {
                    var columnName = dynamicColumn.Name;
                    dynamicObject.Properties.Add(columnName, new DynamicProperty(row[columnName]));
                }

                dynamicList.DynamicAdd(dynamicObject);
            }

            return dynamicList;
        }

        public static DynamicList Create(string dynamicTypeName, DataRowView rowView)
        {
            return Create(dynamicTypeName, rowView.Row);
        }

        public static List<DynamicObject> Create(string dynamicTypeName, DataTable dataTable)
        {
            var dynamicList = new DynamicList();
            {
                dynamicList.Columns.AddRange(dataTable.Columns.Cast<DataColumn>().Select(t => new DynamicColumn()
                {
                    Name = t.ColumnName,
                    DataType = t.DataType,
                }).ToList());

                foreach (DataRow row in dataTable.Rows)
                {
                    var dynamicObject = new DynamicObject();
                    foreach (var dynamicColumn in dynamicList.Columns)
                    {
                        var columnName = dynamicColumn.Name;
                        dynamicObject.Properties.Add(columnName, new DynamicProperty(row[columnName]));
                    }

                    dynamicList.DynamicAdd(dynamicObject);
                }
            }

            return dynamicList;
        }
    }
}