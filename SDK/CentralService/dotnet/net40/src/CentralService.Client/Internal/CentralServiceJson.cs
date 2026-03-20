using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CentralService.Client.Internal
{
    /// <summary>
    /// 提供基于 <see cref="DataContractJsonSerializer"/> 的 JSON 序列化辅助方法。
    /// </summary>
    internal static class CentralServiceJson
    {
        /// <summary>
        /// 为指定类型创建 JSON 序列化器。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <returns>配置好的 JSON 序列化器。</returns>
        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings
            {
                // 让 Dictionary 使用普通 JSON 对象格式输出，避免生成键值数组后与服务端约定不一致。
                UseSimpleDictionaryFormat = true
            });
        }

        /// <summary>
        /// 将指定对象序列化为 JSON 文本。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="value">待序列化对象。</param>
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
        /// 通过首个非空白字符判断文本是否看起来像 JSON。
        /// </summary>
        /// <param name="text">待判断文本。</param>
        /// <returns>首个非空白字符为 <c>{</c> 或 <c>[</c> 时返回 <c>true</c>。</returns>
        public static bool LooksLikeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // 这里只做足够便宜的首字符探测，用来保护错误解析流程；
            // 真正的结构正确性仍由后续反序列化决定。
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
