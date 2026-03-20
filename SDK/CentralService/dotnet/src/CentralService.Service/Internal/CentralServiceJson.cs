using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CentralService.Service.Internal
{
    internal static class CentralServiceJson
    {
        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings
            {
                // 让 Dictionary 使用普通 JSON 对象格式输出，避免生成键值数组后与服务端约定不一致。
                UseSimpleDictionaryFormat = true
            });
        }

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
