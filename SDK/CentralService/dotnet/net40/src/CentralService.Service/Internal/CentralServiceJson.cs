using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CentralService.Service.Internal
{
    /// <summary>
    /// 封装 net40 版本 SDK 的 JSON 序列化与反序列化能力。
    /// </summary>
    internal static class CentralServiceJson
    {
        /// <summary>
        /// 创建用于 SDK 契约模型的 JSON 序列化器。
        /// </summary>
        /// <param name="type">目标模型类型。</param>
        /// <returns>已启用简单字典格式的 JSON 序列化器。</returns>
        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings
            {
                // 元数据字典需要与现代 SDK 保持扁平的 JSON 对象格式，而不是默认数组格式。
                UseSimpleDictionaryFormat = true
            });
        }

        /// <summary>
        /// 将对象序列化为 JSON 文本。
        /// </summary>
        /// <typeparam name="T">待序列化的对象类型。</typeparam>
        /// <param name="value">待序列化的对象。</param>
        /// <returns>JSON 文本；当 <paramref name="value"/> 为 <c>null</c> 时返回空字符串。</returns>
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
        /// 将 JSON 文本反序列化为指定类型。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="json">JSON 文本。</param>
        /// <returns>反序列化结果；当 <paramref name="json"/> 为空时返回默认值。</returns>
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
        /// <param name="text">原始文本。</param>
        /// <returns>当首个非空白字符是 <c>{</c> 或 <c>[</c> 时返回 <c>true</c>。</returns>
        public static bool LooksLikeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

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
