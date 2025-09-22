using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiChi.I18n
{
    /// <summary>
    /// 资源文件自动生成工具
    /// 扫描代码中的资源键，自动生成JSON资源文件模板
    /// </summary>
    public class ResourceGenerator
    {
        private readonly LocalizationConfig _config;
        private readonly HashSet<string> _discoveredKeys;
        private readonly Dictionary<string, ResourceKeyInfo> _keyInfos;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">本地化配置</param>
        public ResourceGenerator(LocalizationConfig? config = null)
        {
            _config = config ?? LocalizationConfig.Default;
            _discoveredKeys = new HashSet<string>();
            _keyInfos = new Dictionary<string, ResourceKeyInfo>();
        }

        /// <summary>
        /// 生成配置
        /// </summary>
        public class GeneratorConfig
        {
            /// <summary>
            /// 要扫描的文件夹路径
            /// </summary>
            public List<string> ScanDirectories { get; set; } = new List<string>();

            /// <summary>
            /// 要扫描的文件模式
            /// </summary>
            public List<string> FilePatterns { get; set; } = new List<string> { "*.cs", "*.xaml", "*.razor", "*.cshtml" };

            /// <summary>
            /// 要排除的文件夹
            /// </summary>
            public List<string> ExcludeDirectories { get; set; } = new List<string> { "bin", "obj", ".git", ".vs", "node_modules" };

            /// <summary>
            /// 资源键的正则表达式模式
            /// </summary>
            public List<string> KeyPatterns { get; set; } = new List<string>
            {
                @"GetString\([""']([^""']+)[""']\)",
                @"LocalizeString\([""']([^""']+)[""']\)",
                @"Localize\([""']([^""']+)[""']\)",
                @"Translate\([""']([^""']+)[""']\)",
                @"T\([""']([^""']+)[""']\)",
                @"LocalizedBinding\s+ResourceKey\s*=\s*[""']([^""']+)[""']",
                @"LocalizeImage\([""']([^""']+)[""']\)",
                @"LocalizeAudio\([""']([^""']+)[""']\)"
            };

            /// <summary>
            /// 输出目录
            /// </summary>
            public string OutputDirectory { get; set; } = "Resources";

            /// <summary>
            /// 生成的文化列表
            /// </summary>
            public List<CultureInfo> Cultures { get; set; } = new List<CultureInfo>
            {
                new CultureInfo("zh-CN"),
                new CultureInfo("en-US")
            };

            /// <summary>
            /// 是否覆盖现有文件
            /// </summary>
            public bool OverwriteExisting { get; set; } = false;

            /// <summary>
            /// 是否生成注释
            /// </summary>
            public bool GenerateComments { get; set; } = true;

            /// <summary>
            /// 是否按类别分组
            /// </summary>
            public bool GroupByCategory { get; set; } = true;

            /// <summary>
            /// 默认值生成策略
            /// </summary>
            public DefaultValueStrategy DefaultValueStrategy { get; set; } = DefaultValueStrategy.UseKey;
        }

        /// <summary>
        /// 默认值生成策略
        /// </summary>
        public enum DefaultValueStrategy
        {
            /// <summary>
            /// 使用键名作为默认值
            /// </summary>
            UseKey,
            /// <summary>
            /// 生成友好的显示名称
            /// </summary>
            GenerateFriendlyName,
            /// <summary>
            /// 生成占位符
            /// </summary>
            GeneratePlaceholder,
            /// <summary>
            /// 留空
            /// </summary>
            Empty
        }

        /// <summary>
        /// 资源键信息
        /// </summary>
        public class ResourceKeyInfo
        {
            /// <summary>
            /// 资源键
            /// </summary>
            public string Key { get; set; } = string.Empty;

            /// <summary>
            /// 发现的文件路径列表
            /// </summary>
            public List<string> FoundInFiles { get; set; } = new List<string>();

            /// <summary>
            /// 资源类型
            /// </summary>
            public ResourceType Type { get; set; } = ResourceType.String;

            /// <summary>
            /// 推断的类别
            /// </summary>
            public string Category { get; set; } = "General";

            /// <summary>
            /// 使用次数
            /// </summary>
            public int UsageCount { get; set; } = 0;
        }

        /// <summary>
        /// 资源类型
        /// </summary>
        public enum ResourceType
        {
            String,
            Image,
            Audio
        }

        /// <summary>
        /// 扫描代码并生成资源文件
        /// </summary>
        /// <param name="config">生成配置</param>
        /// <returns>生成报告</returns>
        public async Task<GenerationReport> GenerateResourceFilesAsync(GeneratorConfig config)
        {
            var report = new GenerationReport();

            try
            {
                // 清理之前的结果
                _discoveredKeys.Clear();
                _keyInfos.Clear();

                // 扫描代码文件
                await ScanCodeFilesAsync(config, report);

                // 生成资源文件
                await GenerateJsonFilesAsync(config, report);

                report.Success = true;
                report.Message = $"成功生成 {report.GeneratedFiles.Count} 个资源文件";
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.Message = $"生成失败: {ex.Message}";
                report.Errors.Add(ex.Message);
            }

            return report;
        }

        /// <summary>
        /// 扫描代码文件
        /// </summary>
        private async Task ScanCodeFilesAsync(GeneratorConfig config, GenerationReport report)
        {
            foreach (var directory in config.ScanDirectories)
            {
                if (!Directory.Exists(directory))
                {
                    report.Warnings.Add($"目录不存在: {directory}");
                    continue;
                }

                await ScanDirectoryAsync(directory, config, report);
            }

            report.DiscoveredKeys = _discoveredKeys.Count;
        }

        /// <summary>
        /// 扫描目录
        /// </summary>
        private async Task ScanDirectoryAsync(string directory, GeneratorConfig config, GenerationReport report)
        {
            foreach (var pattern in config.FilePatterns)
            {
                var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories)
                    .Where(f => !ShouldExcludeFile(f, config))
                    .ToList();

                foreach (var file in files)
                {
                    await ScanFileAsync(file, config, report);
                }
            }
        }

        /// <summary>
        /// 检查文件是否应该被排除
        /// </summary>
        private bool ShouldExcludeFile(string filePath, GeneratorConfig config)
        {
            var directoryName = Path.GetDirectoryName(filePath);
            return config.ExcludeDirectories.Any(exclude =>
                directoryName?.Contains(exclude, StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// 扫描单个文件
        /// </summary>
        private async Task ScanFileAsync(string filePath, GeneratorConfig config, GenerationReport report)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                var keysFound = 0;

                foreach (var pattern in config.KeyPatterns)
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    var matches = regex.Matches(content);

                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var key = match.Groups[1].Value;
                            ProcessDiscoveredKey(key, filePath, pattern);
                            keysFound++;
                        }
                    }
                }

                if (keysFound > 0)
                {
                    report.ScannedFiles.Add(new ScannedFileInfo
                    {
                        FilePath = filePath,
                        KeysFound = keysFound
                    });
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"扫描文件失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理发现的资源键
        /// </summary>
        private void ProcessDiscoveredKey(string key, string filePath, string pattern)
        {
            _discoveredKeys.Add(key);

            if (!_keyInfos.ContainsKey(key))
            {
                _keyInfos[key] = new ResourceKeyInfo
                {
                    Key = key,
                    Type = InferResourceType(key, pattern),
                    Category = InferCategory(key)
                };
            }

            var keyInfo = _keyInfos[key];
            keyInfo.UsageCount++;
            if (!keyInfo.FoundInFiles.Contains(filePath))
            {
                keyInfo.FoundInFiles.Add(filePath);
            }
        }

        /// <summary>
        /// 推断资源类型
        /// </summary>
        private ResourceType InferResourceType(string key, string pattern)
        {
            if (pattern.Contains("Image", StringComparison.OrdinalIgnoreCase))
                return ResourceType.Image;
            if (pattern.Contains("Audio", StringComparison.OrdinalIgnoreCase))
                return ResourceType.Audio;
            return ResourceType.String;
        }

        /// <summary>
        /// 推断资源类别
        /// </summary>
        private string InferCategory(string key)
        {
            var parts = key.Split('.', '_');
            if (parts.Length > 1)
                return parts[0];

            // 根据常见前缀推断类别
            if (key.StartsWith("Btn", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("Button", StringComparison.OrdinalIgnoreCase))
                return "Buttons";

            if (key.StartsWith("Msg", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("Message", StringComparison.OrdinalIgnoreCase))
                return "Messages";

            if (key.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
                return "Errors";

            if (key.StartsWith("Label", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("Lbl", StringComparison.OrdinalIgnoreCase))
                return "Labels";

            return "General";
        }

        /// <summary>
        /// 生成JSON文件
        /// </summary>
        private async Task GenerateJsonFilesAsync(GeneratorConfig config, GenerationReport report)
        {
            if (!Directory.Exists(config.OutputDirectory))
            {
                Directory.CreateDirectory(config.OutputDirectory);
            }

            foreach (var culture in config.Cultures)
            {
                var fileName = $"{culture.Name}.json";
                var filePath = Path.Combine(config.OutputDirectory, fileName);

                if (File.Exists(filePath) && !config.OverwriteExisting)
                {
                    // 合并现有文件
                    await MergeWithExistingFileAsync(filePath, config, culture, report);
                }
                else
                {
                    // 创建新文件
                    await CreateNewFileAsync(filePath, config, culture, report);
                }

                report.GeneratedFiles.Add(filePath);
            }
        }

        /// <summary>
        /// 合并现有文件
        /// </summary>
        private async Task MergeWithExistingFileAsync(string filePath, GeneratorConfig config, CultureInfo culture, GenerationReport report)
        {
            try
            {
                var existingContent = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                var existingJson = JObject.Parse(existingContent);

                var resourceData = CreateResourceData(config, culture);
                MergeResourceData(existingJson, resourceData, config);

                var json = JsonConvert.SerializeObject(existingJson, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include
                });

                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                report.MergedFiles++;
            }
            catch (Exception ex)
            {
                report.Errors.Add($"合并文件失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建新文件
        /// </summary>
        private async Task CreateNewFileAsync(string filePath, GeneratorConfig config, CultureInfo culture, GenerationReport report)
        {
            try
            {
                var resourceData = CreateResourceData(config, culture);

                var json = JsonConvert.SerializeObject(resourceData, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include
                });

                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                report.NewFiles++;
            }
            catch (Exception ex)
            {
                report.Errors.Add($"创建文件失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建资源数据
        /// </summary>
        private JObject CreateResourceData(GeneratorConfig config, CultureInfo culture)
        {
            var root = new JObject();

            if (config.GenerateComments)
            {
                root["_info"] = new JObject
                {
                    ["culture"] = culture.Name,
                    ["generated"] = DateTime.UtcNow,
                    ["generator"] = "TaiChi.I18n.ResourceGenerator",
                    ["totalKeys"] = _discoveredKeys.Count
                };
            }

            if (config.GroupByCategory)
            {
                var categories = _keyInfos.Values.GroupBy(k => k.Category);
                foreach (var category in categories)
                {
                    var categoryObj = new JObject();
                    foreach (var keyInfo in category.OrderBy(k => k.Key))
                    {
                        var value = GenerateDefaultValue(keyInfo.Key, culture, config.DefaultValueStrategy);
                        categoryObj[keyInfo.Key] = value;
                    }
                    root[category.Key] = categoryObj;
                }
            }
            else
            {
                foreach (var keyInfo in _keyInfos.Values.OrderBy(k => k.Key))
                {
                    var value = GenerateDefaultValue(keyInfo.Key, culture, config.DefaultValueStrategy);
                    root[keyInfo.Key] = value;
                }
            }

            return root;
        }

        /// <summary>
        /// 生成默认值
        /// </summary>
        private string GenerateDefaultValue(string key, CultureInfo culture, DefaultValueStrategy strategy)
        {
            return strategy switch
            {
                DefaultValueStrategy.UseKey => key,
                DefaultValueStrategy.GenerateFriendlyName => GenerateFriendlyName(key),
                DefaultValueStrategy.GeneratePlaceholder => $"[{key}]",
                DefaultValueStrategy.Empty => string.Empty,
                _ => key
            };
        }

        /// <summary>
        /// 生成友好名称
        /// </summary>
        private string GenerateFriendlyName(string key)
        {
            // 将驼峰式和下划线分隔的字符串转换为友好名称
            var result = Regex.Replace(key, @"([A-Z])", " $1").Trim();
            result = result.Replace("_", " ").Replace(".", " ");
            return char.ToUpper(result[0]) + result[1..].ToLower();
        }

        /// <summary>
        /// 合并资源数据
        /// </summary>
        private void MergeResourceData(JObject existing, JObject newData, GeneratorConfig config)
        {
            foreach (var property in newData.Properties())
            {
                if (property.Name.StartsWith("_")) continue; // 跳过元数据

                if (!existing.ContainsKey(property.Name))
                {
                    existing[property.Name] = property.Value;
                }
                else if (property.Value is JObject nestedNew && existing[property.Name] is JObject nestedExisting)
                {
                    MergeResourceData(nestedExisting, nestedNew, config);
                }
            }
        }

        /// <summary>
        /// 生成报告
        /// </summary>
        public class GenerationReport
        {
            /// <summary>
            /// 是否成功
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// 消息
            /// </summary>
            public string Message { get; set; } = string.Empty;

            /// <summary>
            /// 发现的资源键数量
            /// </summary>
            public int DiscoveredKeys { get; set; }

            /// <summary>
            /// 扫描的文件信息
            /// </summary>
            public List<ScannedFileInfo> ScannedFiles { get; set; } = new List<ScannedFileInfo>();

            /// <summary>
            /// 生成的文件列表
            /// </summary>
            public List<string> GeneratedFiles { get; set; } = new List<string>();

            /// <summary>
            /// 新建文件数量
            /// </summary>
            public int NewFiles { get; set; }

            /// <summary>
            /// 合并文件数量
            /// </summary>
            public int MergedFiles { get; set; }

            /// <summary>
            /// 错误列表
            /// </summary>
            public List<string> Errors { get; set; } = new List<string>();

            /// <summary>
            /// 警告列表
            /// </summary>
            public List<string> Warnings { get; set; } = new List<string>();
        }

        /// <summary>
        /// 扫描文件信息
        /// </summary>
        public class ScannedFileInfo
        {
            /// <summary>
            /// 文件路径
            /// </summary>
            public string FilePath { get; set; } = string.Empty;

            /// <summary>
            /// 发现的键数量
            /// </summary>
            public int KeysFound { get; set; }
        }

        /// <summary>
        /// 获取发现的资源键信息
        /// </summary>
        /// <returns>资源键信息列表</returns>
        public IReadOnlyDictionary<string, ResourceKeyInfo> GetDiscoveredKeys()
        {
            return _keyInfos;
        }

        /// <summary>
        /// 生成使用统计报告
        /// </summary>
        /// <returns>统计报告</returns>
        public string GenerateUsageReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("资源键使用统计报告");
            sb.AppendLine("===================");
            sb.AppendLine($"总键数: {_keyInfos.Count}");
            sb.AppendLine();

            var categories = _keyInfos.Values.GroupBy(k => k.Category);
            foreach (var category in categories.OrderBy(g => g.Key))
            {
                sb.AppendLine($"类别: {category.Key} ({category.Count()} 个键)");
                foreach (var keyInfo in category.OrderByDescending(k => k.UsageCount))
                {
                    sb.AppendLine($"  {keyInfo.Key} - 使用 {keyInfo.UsageCount} 次 在 {keyInfo.FoundInFiles.Count} 个文件中");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}