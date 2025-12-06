using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiChi.I18n
{
    /// <summary>
    /// JSON格式资源文件加载器
    /// 支持异步加载、缓存、JSON解析和资源类型识别
    /// </summary>
    public class JsonResourceLoader
    {
        private readonly ConcurrentDictionary<string, JObject> _resourceCache = new();
        private readonly LocalizationConfig _config;
        private readonly object _lock = new object();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">本地化配置</param>
        public JsonResourceLoader(LocalizationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeResourceDirectory();
        }

        /// <summary>
        /// 初始化资源目录
        /// </summary>
        private void InitializeResourceDirectory()
        {
            if (string.IsNullOrEmpty(_config.ResourcePath))
            {
                _config.ResourcePath = Path.Combine(AppContext.BaseDirectory, "Resources");
            }

            if (!Directory.Exists(_config.ResourcePath))
            {
                Directory.CreateDirectory(_config.ResourcePath);
            }
        }

        /// <summary>
        /// 获取资源文件路径
        /// </summary>
        /// <param name="culture">文化信息</param>
        /// <returns>资源文件路径</returns>
        private string GetResourceFilePath(CultureInfo culture)
        {
            var fileName = $"{culture.Name}.json";
            return Path.Combine(_config.ResourcePath, fileName);
        }

        /// <summary>
        /// 加载JSON资源文件
        /// </summary>
        /// <param name="culture">文化信息</param>
        /// <returns>JSON对象</returns>
        private JObject LoadJsonResource(CultureInfo culture)
        {
            var cultureName = culture.Name;
            var filePath = GetResourceFilePath(culture);

            if (!File.Exists(filePath))
            {
                // 如果找不到指定文化的资源文件，尝试查找父文化
                var parentCulture = culture.Parent;
                if (parentCulture != null && parentCulture.Name != string.Empty)
                {
                    return LoadJsonResource(parentCulture);
                }

                // 如果还找不到，使用默认文化
                if (!cultureName.Equals(_config.DefaultCulture?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return LoadJsonResource(_config.DefaultCulture ?? new CultureInfo("zh-CN"));
                }

                // 返回空对象
                return new JObject();
            }

            var cacheKey = $"json_{cultureName}";
            if (_resourceCache.TryGetValue(cacheKey, out var cachedJson))
            {
                return cachedJson;
            }

            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var jsonData = JObject.Parse(jsonContent);

                lock (_lock)
                {
                    _resourceCache[cacheKey] = jsonData;
                }

                return jsonData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load JSON resource from '{filePath}': {ex.Message}");
                return new JObject();
            }
        }

        /// <summary>
        /// 异步加载JSON资源文件
        /// </summary>
        /// <param name="culture">文化信息</param>
        /// <returns>JSON对象</returns>
        private async Task<JObject> LoadJsonResourceAsync(CultureInfo culture)
        {
            var cultureName = culture.Name;
            var filePath = GetResourceFilePath(culture);

            if (!File.Exists(filePath))
            {
                // 递归查找父文化资源
                var parentCulture = culture.Parent;
                if (parentCulture != null && parentCulture.Name != string.Empty)
                {
                    return await LoadJsonResourceAsync(parentCulture);
                }

                // 使用默认文化
                if (!cultureName.Equals(_config.DefaultCulture?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return await LoadJsonResourceAsync(_config.DefaultCulture ?? new CultureInfo("zh-CN"));
                }

                return new JObject();
            }

            var cacheKey = $"json_{cultureName}";
            if (_resourceCache.TryGetValue(cacheKey, out var cachedJson))
            {
                return cachedJson;
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                var jsonData = JObject.Parse(jsonContent);

                lock (_lock)
                {
                    _resourceCache[cacheKey] = jsonData;
                }

                return jsonData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load JSON resource from '{filePath}': {ex.Message}");
                return new JObject();
            }
        }

        /// <summary>
        /// 加载字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">文化信息</param>
        /// <returns>本地化字符串</returns>
        public string? LoadString(string key, CultureInfo culture)
        {
            try
            {
                var jsonData = LoadJsonResource(culture);
                return GetNestedValue(jsonData, key)?.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load string resource '{key}' for culture '{culture.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 异步加载字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">文化信息</param>
        /// <returns>本地化字符串</returns>
        public async Task<string?> LoadStringAsync(string key, CultureInfo culture)
        {
            try
            {
                var jsonData = await LoadJsonResourceAsync(culture);
                return GetNestedValue(jsonData, key)?.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load string resource '{key}' for culture '{culture.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载图片资源路径
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">文化信息</param>
        /// <returns>图片资源路径</returns>
        public string? LoadImagePath(string key, CultureInfo culture)
        {
            try
            {
                var jsonData = LoadJsonResource(culture);
                var imagesSection = jsonData["Images"] as JObject;
                if (imagesSection == null) return null;

                return GetNestedValue(imagesSection, key)?.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image resource '{key}' for culture '{culture.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载音频资源路径
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">文化信息</param>
        /// <returns>音频资源路径</returns>
        public string? LoadAudioPath(string key, CultureInfo culture)
        {
            try
            {
                var jsonData = LoadJsonResource(culture);
                var audioSection = jsonData["Audio"] as JObject;
                if (audioSection == null) return null;

                return GetNestedValue(audioSection, key)?.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load audio resource '{key}' for culture '{culture.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取嵌套的JSON值
        /// </summary>
        /// <param name="json">JSON对象</param>
        /// <param name="key">嵌套键（支持点号分隔）</param>
        /// <returns>JSON值</returns>
        private JToken? GetNestedValue(JObject json, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            var keyParts = key.Split('.');
            JToken? current = json;

            foreach (var part in keyParts)
            {
                if (current is JObject obj && obj.TryGetValue(part, out var value))
                {
                    current = value;
                }
                else
                {
                    return null;
                }
            }

            return current;
        }

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">文化信息</param>
        /// <returns>资源是否存在</returns>
        public bool ResourceExists(string key, CultureInfo culture)
        {
            try
            {
                var jsonData = LoadJsonResource(culture);
                return GetNestedValue(jsonData, key) != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取所有支持的文化
        /// </summary>
        /// <returns>支持的文化列表</returns>
        public List<CultureInfo> GetSupportedCultures()
        {
            var cultures = new List<CultureInfo>();

            if (!Directory.Exists(_config.ResourcePath))
                return cultures;

            try
            {
                var jsonFiles = Directory.GetFiles(_config.ResourcePath, "*.json");
                foreach (var file in jsonFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    try
                    {
                        var culture = new CultureInfo(fileName);
                        cultures.Add(culture);
                    }
                    catch (CultureNotFoundException)
                    {
                        // 忽略无效的文化名称
                        continue;
                    }
                }

                return cultures.Distinct().ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get supported cultures: {ex.Message}");
                return cultures;
            }
        }

        /// <summary>
        /// 获取所有资源键
        /// </summary>
        /// <param name="culture">文化信息</param>
        /// <returns>资源键集合</returns>
        public IEnumerable<string> GetAllKeys(CultureInfo culture)
        {
            try
            {
                var jsonData = LoadJsonResource(culture);
                return GetAllNestedKeys(jsonData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get all keys for culture '{culture.Name}': {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 递归获取所有嵌套键
        /// </summary>
        /// <param name="token">JSON令牌</param>
        /// <param name="prefix">键前缀</param>
        /// <returns>键集合</returns>
        private IEnumerable<string> GetAllNestedKeys(JToken token, string prefix = "")
        {
            var keys = new List<string>();

            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

                    if (property.Value is JObject || property.Value is JArray)
                    {
                        keys.AddRange(GetAllNestedKeys(property.Value, key));
                    }
                    else
                    {
                        keys.Add(key);
                    }
                }
            }

            return keys;
        }

        /// <summary>
        /// 重载资源
        /// </summary>
        public void Reload()
        {
            lock (_lock)
            {
                _resourceCache.Clear();
            }
        }

        /// <summary>
        /// 异步重载资源
        /// </summary>
        public async Task ReloadAsync()
        {
            lock (_lock)
            {
                _resourceCache.Clear();
            }

            // 预热缓存：异步加载所有支持的文化的资源
            var cultures = GetSupportedCultures();
            var loadTasks = cultures.Select(async culture =>
            {
                try
                {
                    await LoadJsonResourceAsync(culture);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to preload culture '{culture.Name}': {ex.Message}");
                }
            });

            await Task.WhenAll(loadTasks);
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            lock (_lock)
            {
                _resourceCache.Clear();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            ClearCache();
        }
    }
}