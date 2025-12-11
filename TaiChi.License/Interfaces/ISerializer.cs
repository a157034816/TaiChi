using System;

namespace TaiChi.License.Interfaces
{
    /// <summary>
    /// 简单序列化抽象，便于后续切换为 MessagePack。
    /// </summary>
    public interface ISerializer
    {
        byte[] Serialize<T>(T value);
        T? Deserialize<T>(byte[] data);
        string Name { get; }
    }
}

