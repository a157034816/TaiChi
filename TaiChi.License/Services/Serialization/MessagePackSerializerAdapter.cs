// 可选：启用 MessagePack 支持
// 使用方法：
// 1) 安装 NuGet 包：MessagePack（>=2.x）
// 2) 在 TaiChi.License.csproj 添加常量：
//    <PropertyGroup>
//      <DefineConstants>$(DefineConstants);TAI_CHI_USE_MESSAGEPACK</DefineConstants>
//    </PropertyGroup>
// 3) 代码中注入：new MessagePackSerializerAdapter()

#if TAI_CHI_USE_MESSAGEPACK
using System;
using MessagePack;
using MessagePack.Resolvers;
using TaiChi.License.Interfaces;

namespace TaiChi.License.Services.Serialization
{
    public sealed class MessagePackSerializerAdapter : ISerializer
    {
        private static readonly MessagePackSerializerOptions Options =
            MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

        public string Name => "msgpack";

        public byte[] Serialize<T>(T value)
        {
            return MessagePack.MessagePackSerializer.Serialize(value, Options);
        }

        public T? Deserialize<T>(byte[] data)
        {
            return MessagePack.MessagePackSerializer.Deserialize<T>(data, Options);
        }
    }
}
#endif

