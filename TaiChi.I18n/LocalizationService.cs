using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TaiChi.I18n
{
    /// <summary>
    /// 本地化服务核心实现
    /// 提供运行时语言切换、资源热更新、缓存管理等功能
    /// </summary>
    public class LocalizationService : ILocalizationServiceAdvanced
    {
        private readonly TaiChiResourceManager _resourceManager;
        private readonly LocalizationConfig _config;
        private FileSystemWatcher? _fileWatcher;

        /// <summary>
        /// 当前语言文化
        /// </summary>
        public CultureInfo CurrentCulture
        {
            get => _resourceManager.CurrentCulture;
            set => _resourceManager.CurrentCulture = value;
        }

        /// <summary>
        /// 默认语言文化
        /// </summary>
        public CultureInfo DefaultCulture { get; }

        /// <summary>
        /// 语言切换事件
        /// </summary>
        public event EventHandler<CultureInfo>? LanguageChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">本地化配置</param>
        public LocalizationService(LocalizationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            DefaultCulture = config.DefaultCulture ?? new CultureInfo("zh-CN");
            _resourceManager = new TaiChiResourceManager(config);

            // 订阅语言切换事件
            _resourceManager.LanguageChanged += (sender, culture) =>
            {
                LanguageChanged?.Invoke(sender, culture);
                CultureInfo.CurrentUICulture = culture;
            };

            // 初始化文化
            CultureInfo.CurrentUICulture = CurrentCulture;
        }

        /// <summary>
        /// 获取字符串资源
        /// </summary>
        public string GetString(string key, CultureInfo? culture = null)
        {
            return _resourceManager.GetString(key, culture);
        }

        /// <summary>
        /// 异步获取字符串资源
        /// </summary>
        public Task<string> GetStringAsync(string key, CultureInfo? culture = null)
        {
            return _resourceManager.GetStringAsync(key, culture);
        }

        /// <summary>
        /// 获取格式化字符串资源
        /// </summary>
        public string GetFormattedString(string key, params object[] args)
        {
            return _resourceManager.GetFormattedString(key, args);
        }

        /// <summary>
        /// 获取格式化字符串资源
        /// </summary>
        public string GetFormattedString(string key, CultureInfo? culture, params object[] args)
        {
            return _resourceManager.GetFormattedString(key, culture, args);
        }

        /// <summary>
        /// 获取图片资源路径
        /// </summary>
        public string? GetImagePath(string key, CultureInfo? culture = null)
        {
            return _resourceManager.GetImagePath(key, culture);
        }

        /// <summary>
        /// 获取音频资源路径
        /// </summary>
        public string? GetAudioPath(string key, CultureInfo? culture = null)
        {
            return _resourceManager.GetAudioPath(key, culture);
        }

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        public bool ResourceExists(string key, CultureInfo? culture = null)
        {
            return _resourceManager.ResourceExists(key, culture);
        }

        /// <summary>
        /// 获取所有支持的文化
        /// </summary>
        public List<CultureInfo> GetSupportedCultures()
        {
            return _resourceManager.GetSupportedCultures();
        }

        /// <summary>
        /// 切换语言
        /// </summary>
        public bool SetLanguage(string cultureName)
        {
            try
            {
                var culture = new CultureInfo(cultureName);
                return SetLanguage(culture);
            }
            catch (CultureNotFoundException)
            {
                System.Diagnostics.Debug.WriteLine($"Culture '{cultureName}' not found.");
                return false;
            }
        }

        /// <summary>
        /// 切换语言
        /// </summary>
        public bool SetLanguage(CultureInfo culture)
        {
            try
            {
                if (!GetSupportedCultures().Contains(culture))
                {
                    System.Diagnostics.Debug.WriteLine($"Culture '{culture.Name}' is not supported.");
                    return false;
                }

                CurrentCulture = culture;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set language to '{culture.Name}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重置为默认语言
        /// </summary>
        public void ResetToDefault()
        {
            SetLanguage(DefaultCulture);
        }

        /// <summary>
        /// 清理资源缓存
        /// </summary>
        public void ClearCache()
        {
            _resourceManager.ClearCache();
        }

        /// <summary>
        /// 重载资源文件
        /// </summary>
        public void ReloadResources()
        {
            _resourceManager.ReloadResources();
        }

        /// <summary>
        /// 异步重载资源文件
        /// </summary>
        public Task ReloadResourcesAsync()
        {
            return _resourceManager.ReloadResourcesAsync();
        }

        /// <summary>
        /// 监控资源文件变化
        /// </summary>
        public void EnableFileMonitoring(bool enable)
        {
            if (enable && _fileWatcher == null)
            {
                StartFileMonitoring();
            }
            else if (!enable && _fileWatcher != null)
            {
                StopFileMonitoring();
            }
        }

        /// <summary>
        /// 开始文件监控
        /// </summary>
        private void StartFileMonitoring()
        {
            if (string.IsNullOrEmpty(_config.ResourcePath) || !Directory.Exists(_config.ResourcePath))
                return;

            _fileWatcher = new FileSystemWatcher(_config.ResourcePath, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnResourceFileChanged;
            _fileWatcher.Created += OnResourceFileChanged;
            _fileWatcher.Deleted += OnResourceFileChanged;
        }

        /// <summary>
        /// 停止文件监控
        /// </summary>
        private void StopFileMonitoring()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.Changed -= OnResourceFileChanged;
                _fileWatcher.Created -= OnResourceFileChanged;
                _fileWatcher.Deleted -= OnResourceFileChanged;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
        }

        /// <summary>
        /// 资源文件变化事件处理
        /// </summary>
        private void OnResourceFileChanged(object sender, FileSystemEventArgs e)
        {
            // 使用异步方式重载资源，避免阻塞主线程
            Task.Run(async () =>
            {
                // 延迟重载，避免文件锁定问题
                await Task.Delay(500);
                await ReloadResourcesAsync();
            });
        }

        /// <summary>
        /// 获取所有资源键
        /// </summary>
        public IEnumerable<string> GetAllKeys(CultureInfo? culture = null)
        {
            // 这里需要通过JsonResourceLoader实现
            // 暂时返回空集合，等JsonResourceLoader实现后完善
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// 获取指定前缀的资源键
        /// </summary>
        public IEnumerable<string> GetKeysWithPrefix(string prefix, CultureInfo? culture = null)
        {
            return GetAllKeys(culture).Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 验证资源文件完整性
        /// </summary>
        public ResourceValidationResult ValidateResources()
        {
            var result = new ResourceValidationResult();

            try
            {
                var supportedCultures = GetSupportedCultures();
                if (supportedCultures.Count < 2)
                {
                    result.OtherErrors.Add("至少需要支持两种语言。");
                    return result;
                }

                // 获取默认文化的所有键
                var defaultKeys = GetAllKeys(DefaultCulture).ToList();
                if (!defaultKeys.Any())
                {
                    result.OtherErrors.Add("默认文化资源文件为空或格式错误。");
                    return result;
                }

                // 检查其他文化是否缺少资源键
                foreach (var culture in supportedCultures.Where(c => c.Name != DefaultCulture.Name))
                {
                    var cultureKeys = GetAllKeys(culture).ToList();
                    var missingKeys = defaultKeys.Except(cultureKeys).ToList();

                    if (missingKeys.Any())
                    {
                        result.MissingKeys.AddRange(missingKeys.Select(key => $"{key} ({culture.Name})"));
                    }
                }

                result.IsValid = !result.MissingKeys.Any() && !result.FormatErrors.Any() && !result.OtherErrors.Any();
            }
            catch (Exception ex)
            {
                result.OtherErrors.Add($"验证过程中发生错误: {ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        /// <summary>
        /// 导出资源到指定格式
        /// </summary>
        public bool ExportResources(ResourceExportFormat format, string outputPath)
        {
            try
            {
                var supportedCultures = GetSupportedCultures();

                switch (format)
                {
                    case ResourceExportFormat.Json:
                        return ExportToJson(supportedCultures, outputPath);

                    case ResourceExportFormat.Csv:
                        return ExportToCsv(supportedCultures, outputPath);

                    case ResourceExportFormat.Xml:
                        return ExportToXml(supportedCultures, outputPath);

                    case ResourceExportFormat.ResX:
                        return ExportToResX(supportedCultures, outputPath);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从指定格式导入资源
        /// </summary>
        public bool ImportResources(ResourceExportFormat format, string inputPath)
        {
            try
            {
                if (!File.Exists(inputPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Import file not found: {inputPath}");
                    return false;
                }

                switch (format)
                {
                    case ResourceExportFormat.Json:
                        return ImportFromJson(inputPath);

                    case ResourceExportFormat.Csv:
                        return ImportFromCsv(inputPath);

                    case ResourceExportFormat.Xml:
                        return ImportFromXml(inputPath);

                    case ResourceExportFormat.ResX:
                        return ImportFromResX(inputPath);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出为JSON格式
        /// </summary>
        private bool ExportToJson(IEnumerable<CultureInfo> cultures, string outputPath)
        {
            // 实现JSON导出逻辑
            // 需要JsonResourceLoader实现后完善
            return false;
        }

        /// <summary>
        /// 导出为CSV格式
        /// </summary>
        private bool ExportToCsv(IEnumerable<CultureInfo> cultures, string outputPath)
        {
            // 实现CSV导出逻辑
            return false;
        }

        /// <summary>
        /// 导出为XML格式
        /// </summary>
        private bool ExportToXml(IEnumerable<CultureInfo> cultures, string outputPath)
        {
            // 实现XML导出逻辑
            return false;
        }

        /// <summary>
        /// 导出为ResX格式
        /// </summary>
        private bool ExportToResX(IEnumerable<CultureInfo> cultures, string outputPath)
        {
            // 实现ResX导出逻辑
            return false;
        }

        /// <summary>
        /// 从JSON导入
        /// </summary>
        private bool ImportFromJson(string inputPath)
        {
            // 实现JSON导入逻辑
            return false;
        }

        /// <summary>
        /// 从CSV导入
        /// </summary>
        private bool ImportFromCsv(string inputPath)
        {
            // 实现CSV导入逻辑
            return false;
        }

        /// <summary>
        /// 从XML导入
        /// </summary>
        private bool ImportFromXml(string inputPath)
        {
            // 实现XML导入逻辑
            return false;
        }

        /// <summary>
        /// 从ResX导入
        /// </summary>
        private bool ImportFromResX(string inputPath)
        {
            // 实现ResX导入逻辑
            return false;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopFileMonitoring();
            _resourceManager?.Dispose();
        }
    }
}