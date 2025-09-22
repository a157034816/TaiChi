using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TaiChi.I18n
{
    /// <summary>
    /// 太极资源管理器 - 统一资源管理接口
    /// 支持字符串、图片、音频资源的加载和缓存管理
    /// </summary>
    public class TaiChiResourceManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly JsonResourceLoader _jsonLoader;
        private readonly LocalizationConfig _config;
        private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

        /// <summary>
        /// 当前语言文化
        /// </summary>
        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set => SetCurrentCulture(value);
        }

        /// <summary>
        /// 语言切换事件
        /// </summary>
        public event EventHandler<CultureInfo>? LanguageChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">本地化配置</param>
        public TaiChiResourceManager(LocalizationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _jsonLoader = new JsonResourceLoader(config);
            InitializeDefaultCulture();
        }

        /// <summary>
        /// 初始化默认语言
        /// </summary>
        private void InitializeDefaultCulture()
        {
            var defaultCulture = _config.DefaultCulture ?? new CultureInfo("zh-CN");
            SetCurrentCulture(defaultCulture, false);
        }

        /// <summary>
        /// 设置当前语言文化
        /// </summary>
        /// <param name="culture">目标文化</param>
        /// <param name="raiseEvent">是否触发事件</param>
        private void SetCurrentCulture(CultureInfo culture, bool raiseEvent = true)
        {
            if (culture == null) throw new ArgumentNullException(nameof(culture));

            if (_currentCulture.Name != culture.Name)
            {
                _currentCulture = culture;
                ClearCache(); // 清理缓存以便重新加载资源

                if (raiseEvent)
                {
                    LanguageChanged?.Invoke(this, culture);
                }
            }
        }

        /// <summary>
        /// 清理资源缓存
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 获取字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>本地化字符串</returns>
        public string GetString(string key, CultureInfo? culture = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Resource key cannot be null or empty.", nameof(key));

            var targetCulture = culture ?? _currentCulture;
            var cacheKey = $"string_{targetCulture.Name}_{key}";

            if (_cache.TryGetValue(cacheKey, out var cachedValue))
                return (string)cachedValue;

            try
            {
                var value = _jsonLoader.LoadString(key, targetCulture);
                _cache.TryAdd(cacheKey, value);
                return value ?? key; // 如果未找到，返回键本身
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load string resource '{key}' for culture '{targetCulture.Name}': {ex.Message}");
                return key; // 加载失败时返回键本身
            }
        }

        /// <summary>
        /// 异步获取字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>本地化字符串</returns>
        public async Task<string> GetStringAsync(string key, CultureInfo? culture = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Resource key cannot be null or empty.", nameof(key));

            var targetCulture = culture ?? _currentCulture;
            var cacheKey = $"string_{targetCulture.Name}_{key}";

            if (_cache.TryGetValue(cacheKey, out var cachedValue))
                return await Task.FromResult((string)cachedValue);

            try
            {
                var value = await _jsonLoader.LoadStringAsync(key, targetCulture);
                _cache.TryAdd(cacheKey, value);
                return value ?? key;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load string resource '{key}' for culture '{targetCulture.Name}': {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// 获取格式化字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>格式化后的本地化字符串</returns>
        public string GetFormattedString(string key, params object[] args)
        {
            return GetFormattedString(key, null, args);
        }

        /// <summary>
        /// 获取格式化字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化</param>
        /// <param name="args">格式化参数</param>
        /// <returns>格式化后的本地化字符串</returns>
        public string GetFormattedString(string key, CultureInfo? culture, params object[] args)
        {
            var format = GetString(key, culture);
            return string.Format(culture ?? _currentCulture, format, args);
        }

        /// <summary>
        /// 获取图片资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>图片资源路径</returns>
        public string? GetImagePath(string key, CultureInfo? culture = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Resource key cannot be null or empty.", nameof(key));

            var targetCulture = culture ?? _currentCulture;
            var cacheKey = $"image_{targetCulture.Name}_{key}";

            if (_cache.TryGetValue(cacheKey, out var cachedValue))
                return (string?)cachedValue;

            try
            {
                var path = _jsonLoader.LoadImagePath(key, targetCulture);
                if (!string.IsNullOrEmpty(path))
                {
                    _cache.TryAdd(cacheKey, path);
                }
                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image resource '{key}' for culture '{targetCulture.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取音频资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>音频资源路径</returns>
        public string? GetAudioPath(string key, CultureInfo? culture = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Resource key cannot be null or empty.", nameof(key));

            var targetCulture = culture ?? _currentCulture;
            var cacheKey = $"audio_{targetCulture.Name}_{key}";

            if (_cache.TryGetValue(cacheKey, out var cachedValue))
                return (string?)cachedValue;

            try
            {
                var path = _jsonLoader.LoadAudioPath(key, targetCulture);
                if (!string.IsNullOrEmpty(path))
                {
                    _cache.TryAdd(cacheKey, path);
                }
                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load audio resource '{key}' for culture '{targetCulture.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>资源是否存在</returns>
        public bool ResourceExists(string key, CultureInfo? culture = null)
        {
            if (string.IsNullOrEmpty(key)) return false;

            var targetCulture = culture ?? _currentCulture;
            return _jsonLoader.ResourceExists(key, targetCulture);
        }

        /// <summary>
        /// 获取所有支持的文化
        /// </summary>
        /// <returns>支持的文化列表</returns>
        public List<CultureInfo> GetSupportedCultures()
        {
            return _jsonLoader.GetSupportedCultures();
        }

        /// <summary>
        /// 重载资源文件
        /// </summary>
        public void ReloadResources()
        {
            ClearCache();
            _jsonLoader.Reload();
        }

        /// <summary>
        /// 异步重载资源文件
        /// </summary>
        public async Task ReloadResourcesAsync()
        {
            ClearCache();
            await _jsonLoader.ReloadAsync();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            ClearCache();
            _jsonLoader?.Dispose();
        }
    }
}