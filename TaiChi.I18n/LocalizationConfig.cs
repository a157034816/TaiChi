using System;
using System.Collections.Generic;
using System.Globalization;

namespace TaiChi.I18n
{
    /// <summary>
    /// 本地化配置类
    /// 管理语言设置、资源路径、缓存策略等配置
    /// </summary>
    public class LocalizationConfig
    {
        private CultureInfo? _defaultCulture;
        private string _resourcePath = string.Empty;
        private bool _enableCache = true;
        private int _cacheTimeout = 300; // 5分钟
        private bool _enableFileMonitoring = false;
        private bool _autoReloadOnFileChange = true;
        private ResourceFallbackBehavior _fallbackBehavior = ResourceFallbackBehavior.ParentCultureThenDefault;

        /// <summary>
        /// 默认语言文化
        /// </summary>
        public CultureInfo? DefaultCulture
        {
            get => _defaultCulture ?? new CultureInfo("zh-CN");
            set => _defaultCulture = value;
        }

        /// <summary>
        /// 资源文件路径
        /// </summary>
        public string ResourcePath
        {
            get => _resourcePath;
            set => _resourcePath = string.IsNullOrWhiteSpace(value) ?
                System.IO.Path.Combine(AppContext.BaseDirectory, "Resources") : value;
        }

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool EnableCache
        {
            get => _enableCache;
            set => _enableCache = value;
        }

        /// <summary>
        /// 缓存超时时间（秒）
        /// </summary>
        public int CacheTimeout
        {
            get => _cacheTimeout;
            set => _cacheTimeout = Math.Max(0, value); // 不能为负数
        }

        /// <summary>
        /// 是否启用文件监控
        /// </summary>
        public bool EnableFileMonitoring
        {
            get => _enableFileMonitoring;
            set => _enableFileMonitoring = value;
        }

        /// <summary>
        /// 文件变化时自动重载
        /// </summary>
        public bool AutoReloadOnFileChange
        {
            get => _autoReloadOnFileChange;
            set => _autoReloadOnFileChange = value;
        }

        /// <summary>
        /// 资源回退行为
        /// </summary>
        public ResourceFallbackBehavior FallbackBehavior
        {
            get => _fallbackBehavior;
            set => _fallbackBehavior = value;
        }

        /// <summary>
        /// 支持的文化列表
        /// </summary>
        public List<CultureInfo> SupportedCultures { get; set; } = new();

        /// <summary>
        /// 资源文件扩展名
        /// </summary>
        public string ResourceFileExtension { get; set; } = ".json";

        /// <summary>
        /// 是否启用调试模式
        /// </summary>
        public bool EnableDebugMode { get; set; } = false;

        /// <summary>
        /// 是否启用日志记录
        /// </summary>
        public bool EnableLogging { get; set; } = false;

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Warning;

        /// <summary>
        /// 自定义资源键前缀
        /// </summary>
        public string ResourceKeyPrefix { get; set; } = string.Empty;

        /// <summary>
        /// 是否忽略大小写
        /// </summary>
        public bool IgnoreCase { get; set; } = true;

        /// <summary>
        /// 默认字符串回退值
        /// </summary>
        public string DefaultStringFallback { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用异步加载
        /// </summary>
        public bool EnableAsyncLoading { get; set; } = true;

        /// <summary>
        /// 最大并发加载数
        /// </summary>
        public int MaxConcurrentLoads { get; set; } = 4;

        /// <summary>
        /// 资源加载超时时间（毫秒）
        /// </summary>
        public int ResourceLoadTimeout { get; set; } = 5000;

        /// <summary>
        /// 是否启用资源压缩
        /// </summary>
        public bool EnableResourceCompression { get; set; } = false;

        /// <summary>
        /// 图片资源基础路径
        /// </summary>
        public string ImageResourceBasePath { get; set; } = "Images";

        /// <summary>
        /// 音频资源基础路径
        /// </summary>
        public string AudioResourceBasePath { get; set; } = "Audio";

        /// <summary>
        /// 是否启用资源验证
        /// </summary>
        public bool EnableResourceValidation { get; set; } = true;

        /// <summary>
        /// 验证失败时是否抛出异常
        /// </summary>
        public bool ThrowOnValidationFailure { get; set; } = false;

        /// <summary>
        /// 默认配置实例
        /// </summary>
        public static LocalizationConfig Default => new LocalizationConfig();

        /// <summary>
        /// 创建开发环境配置
        /// </summary>
        public static LocalizationConfig CreateDevelopmentConfig()
        {
            return new LocalizationConfig
            {
                EnableCache = false,
                EnableFileMonitoring = true,
                AutoReloadOnFileChange = true,
                EnableDebugMode = true,
                EnableLogging = true,
                LogLevel = LogLevel.Debug,
                EnableResourceValidation = true,
                ThrowOnValidationFailure = false
            };
        }

        /// <summary>
        /// 创建生产环境配置
        /// </summary>
        public static LocalizationConfig CreateProductionConfig()
        {
            return new LocalizationConfig
            {
                EnableCache = true,
                CacheTimeout = 1800, // 30分钟
                EnableFileMonitoring = false,
                AutoReloadOnFileChange = false,
                EnableDebugMode = false,
                EnableLogging = false,
                LogLevel = LogLevel.Error,
                EnableResourceValidation = true,
                ThrowOnValidationFailure = true,
                EnableAsyncLoading = true,
                MaxConcurrentLoads = 8,
                ResourceLoadTimeout = 3000,
                EnableResourceCompression = true
            };
        }

        /// <summary>
        /// 创建测试环境配置
        /// </summary>
        public static LocalizationConfig CreateTestConfig()
        {
            return new LocalizationConfig
            {
                EnableCache = false,
                EnableFileMonitoring = false,
                AutoReloadOnFileChange = false,
                EnableDebugMode = true,
                EnableLogging = true,
                LogLevel = LogLevel.Information,
                EnableResourceValidation = false,
                ThrowOnValidationFailure = false,
                DefaultStringFallback = "[TEST]"
            };
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <returns>验证结果</returns>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            // 验证默认文化
            if (DefaultCulture == null)
            {
                result.Errors.Add("默认文化不能为null");
            }

            // 验证资源路径
            if (string.IsNullOrWhiteSpace(ResourcePath))
            {
                result.Errors.Add("资源路径不能为空");
            }

            // 验证缓存配置
            if (CacheTimeout < 0)
            {
                result.Warnings.Add("缓存超时时间不能为负数，已自动调整为0");
                CacheTimeout = 0;
            }

            // 验证并发配置
            if (MaxConcurrentLoads < 1)
            {
                result.Warnings.Add("最大并发加载数不能小于1，已自动调整为1");
                MaxConcurrentLoads = 1;
            }

            // 验证超时配置
            if (ResourceLoadTimeout < 1000)
            {
                result.Warnings.Add("资源加载超时时间不应小于1000毫秒，建议适当调整");
            }

            // 验证支持的文化
            if (SupportedCultures.Count == 0)
            {
                result.Warnings.Add("未配置支持的文化列表，将使用默认检测");
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        /// <returns>克隆的配置对象</returns>
        public LocalizationConfig Clone()
        {
            return new LocalizationConfig
            {
                _defaultCulture = _defaultCulture,
                _resourcePath = _resourcePath,
                _enableCache = _enableCache,
                _cacheTimeout = _cacheTimeout,
                _enableFileMonitoring = _enableFileMonitoring,
                _autoReloadOnFileChange = _autoReloadOnFileChange,
                _fallbackBehavior = _fallbackBehavior,
                SupportedCultures = new List<CultureInfo>(SupportedCultures),
                ResourceFileExtension = ResourceFileExtension,
                EnableDebugMode = EnableDebugMode,
                EnableLogging = EnableLogging,
                LogLevel = LogLevel,
                ResourceKeyPrefix = ResourceKeyPrefix,
                IgnoreCase = IgnoreCase,
                DefaultStringFallback = DefaultStringFallback,
                EnableAsyncLoading = EnableAsyncLoading,
                MaxConcurrentLoads = MaxConcurrentLoads,
                ResourceLoadTimeout = ResourceLoadTimeout,
                EnableResourceCompression = EnableResourceCompression,
                ImageResourceBasePath = ImageResourceBasePath,
                AudioResourceBasePath = AudioResourceBasePath,
                EnableResourceValidation = EnableResourceValidation,
                ThrowOnValidationFailure = ThrowOnValidationFailure
            };
        }

        /// <summary>
        /// 应用配置覆盖
        /// </summary>
        /// <param name="overrides">覆盖配置</param>
        public void ApplyOverrides(LocalizationConfig overrides)
        {
            if (overrides == null) return;

            if (overrides._defaultCulture != null)
                _defaultCulture = overrides._defaultCulture;

            if (!string.IsNullOrWhiteSpace(overrides._resourcePath))
                _resourcePath = overrides._resourcePath;

            _enableCache = overrides._enableCache;
            _cacheTimeout = overrides._cacheTimeout;
            _enableFileMonitoring = overrides._enableFileMonitoring;
            _autoReloadOnFileChange = overrides._autoReloadOnFileChange;
            _fallbackBehavior = overrides._fallbackBehavior;

            if (overrides.SupportedCultures.Any())
                SupportedCultures = new List<CultureInfo>(overrides.SupportedCultures);

            ResourceFileExtension = overrides.ResourceFileExtension;
            EnableDebugMode = overrides.EnableDebugMode;
            EnableLogging = overrides.EnableLogging;
            LogLevel = overrides.LogLevel;
            ResourceKeyPrefix = overrides.ResourceKeyPrefix;
            IgnoreCase = overrides.IgnoreCase;
            DefaultStringFallback = overrides.DefaultStringFallback;
            EnableAsyncLoading = overrides.EnableAsyncLoading;
            MaxConcurrentLoads = overrides.MaxConcurrentLoads;
            ResourceLoadTimeout = overrides.ResourceLoadTimeout;
            EnableResourceCompression = overrides.EnableResourceCompression;
            ImageResourceBasePath = overrides.ImageResourceBasePath;
            AudioResourceBasePath = overrides.AudioResourceBasePath;
            EnableResourceValidation = overrides.EnableResourceValidation;
            ThrowOnValidationFailure = overrides.ThrowOnValidationFailure;
        }
    }

    /// <summary>
    /// 资源回退行为
    /// </summary>
    public enum ResourceFallbackBehavior
    {
        /// <summary>
        /// 先回退到父文化，然后回退到默认文化
        /// </summary>
        ParentCultureThenDefault,

        /// <summary>
        /// 直接回退到默认文化
        /// </summary>
        DefaultOnly,

        /// <summary>
        /// 回退到键名
        /// </summary>
        KeyName,

        /// <summary>
        /// 回退到默认字符串
        /// </summary>
        DefaultString,

        /// <summary>
        /// 抛出异常
        /// </summary>
        ThrowException
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 信息
        /// </summary>
        Information = 1,

        /// <summary>
        /// 警告
        /// </summary>
        Warning = 2,

        /// <summary>
        /// 错误
        /// </summary>
        Error = 3,

        /// <summary>
        /// 严重错误
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// 配置验证结果
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }
}