using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CentralService.Client.Internal
{
    /// <summary>
    /// 提供中心服务客户端统一使用的 JSON 序列化辅助方法。
    /// </summary>
    internal static class CentralServiceJson
    {
        /// <summary>
        /// 为指定类型创建 JSON 序列化器。
        /// </summary>
        /// <param name="type">要序列化或反序列化的目标类型。</param>
        /// <returns>已配置完成的 <see cref="DataContractJsonSerializer"/>。</returns>
        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            // 启用简单字典格式，避免 DataContract 默认把字典序列化为 Key/Value 数组，便于与服务端 JSON 契约对齐。
            return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });
        }

        /// <summary>
        /// 将对象序列化为 JSON 字符串。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="value">待序列化对象。</param>
        /// <returns>JSON 字符串；当值为 <c>null</c> 时返回空字符串。</returns>
        public static string Serialize<T>(T value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var serializer = CreateSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为目标类型。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="json">JSON 字符串。</param>
        /// <returns>反序列化后的对象；当输入为空时返回默认值。</returns>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return default(T);
            }

            var serializer = CreateSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)serializer.ReadObject(ms);
            }
        }

        /// <summary>
        /// 快速判断文本是否可能是 JSON。
        /// </summary>
        /// <param name="text">待判断文本。</param>
        /// <returns>文本首个非空白字符为对象或数组起始符时返回 <c>true</c>。</returns>
        public static bool LooksLikeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // 这里只做轻量前置判断，避免在明显不是 JSON 的场景下触发反序列化异常与额外开销。
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                return c == '{' || c == '[';
            }

            return false;
        }
    }
}
