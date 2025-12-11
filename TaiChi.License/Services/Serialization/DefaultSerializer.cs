using System;
using System.IO;
using System.Xml.Serialization;
using TaiChi.License.Interfaces;

namespace TaiChi.License.Services.Serialization
{
    /// <summary>
    /// 默认序列化器：使用 XmlSerializer 实现（零依赖）。
    /// 后续可替换为 MessagePack 实现，保持调用方无感知。
    /// </summary>
    public sealed class DefaultSerializer : ISerializer
    {
        public static readonly DefaultSerializer Instance = new DefaultSerializer();

        public string Name => "xml";

        public byte[] Serialize<T>(T value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var xs = new XmlSerializer(typeof(T));
            using var ms = new MemoryStream();
            xs.Serialize(ms, value!);
            return ms.ToArray();
        }

        public T? Deserialize<T>(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var xs = new XmlSerializer(typeof(T));
            using var ms = new MemoryStream(data);
            var obj = xs.Deserialize(ms);
            return (T?)obj;
        }
    }
}

