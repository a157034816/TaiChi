using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TaiChi.I18n
{
    /// <summary>
    /// 资源文件验证工具
    /// 检查资源文件完整性、缺失键值、格式错误等
    /// </summary>
    public class ResourceValidator
    {
        private readonly LocalizationConfig _config;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">本地化配置</param>
        public ResourceValidator(LocalizationConfig? config = null)
        {
            _config = config ?? LocalizationConfig.Default;
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        public class ValidationConfig
        {
            /// <summary>
            /// 资源文件目录
            /// </summary>
            public string ResourceDirectory { get; set; } = "Resources";

            /// <summary>
            /// 要验证的文化列表
            /// </summary>
            public List<CultureInfo> Cultures { get; set; } = new List<CultureInfo>
            {
                new CultureInfo("zh-CN"),
                new CultureInfo("en-US")
            };

            /// <summary>
            /// 基准文化（用于检查缺失键）
            /// </summary>
            public CultureInfo ReferenceCulture { get; set; } = new CultureInfo("zh-CN");

            /// <summary>
            /// 是否检查格式化字符串
            /// </summary>
            public bool ValidateFormatStrings { get; set; } = true;

            /// <summary>
            /// 是否检查空值
            /// </summary>
            public bool CheckEmptyValues { get; set; } = true;

            /// <summary>
            /// 是否检查重复键
            /// </summary>
            public bool CheckDuplicateKeys { get; set; } = true;

            /// <summary>
            /// 是否检查未使用的键
            /// </summary>
            public bool CheckUnusedKeys { get; set; } = false;

            /// <summary>
            /// 代码扫描目录（用于检查未使用的键）
            /// </summary>
            public List<string> CodeDirectories { get; set; } = new List<string>();

            /// <summary>
            /// 是否检查文件编码
            /// </summary>
            public bool CheckFileEncoding { get; set; } = true;

            /// <summary>
            /// 是否严格模式（所有错误都作为错误处理）
            /// </summary>
            public bool StrictMode { get; set; } = false;
        }

        /// <summary>
        /// 验证结果严重级别
        /// </summary>
        public enum ValidationSeverity
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// 验证问题类型
        /// </summary>
        public enum ValidationIssueType
        {
            MissingKey,
            EmptyValue,
            InvalidFormat,
            DuplicateKey,
            UnusedKey,
            EncodingIssue,
            JsonSyntaxError,
            StructureError,
            FormatStringMismatch
        }

        /// <summary>
        /// 验证问题
        /// </summary>
        public class ValidationIssue
        {
            /// <summary>
            /// 问题类型
            /// </summary>
            public ValidationIssueType Type { get; set; }

            /// <summary>
            /// 严重级别
            /// </summary>
            public ValidationSeverity Severity { get; set; }

            /// <summary>
            /// 文件路径
            /// </summary>
            public string FilePath { get; set; } = string.Empty;

            /// <summary>
            /// 文化名称
            /// </summary>
            public string Culture { get; set; } = string.Empty;

            /// <summary>
            /// 资源键
            /// </summary>
            public string Key { get; set; } = string.Empty;

            /// <summary>
            /// 问题描述
            /// </summary>
            public string Message { get; set; } = string.Empty;

            /// <summary>
            /// 建议的修复方案
            /// </summary>
            public string Suggestion { get; set; } = string.Empty;

            /// <summary>
            /// 行号（如果适用）
            /// </summary>
            public int? LineNumber { get; set; }

            /// <summary>
            /// 列号（如果适用）
            /// </summary>
            public int? ColumnNumber { get; set; }
        }

        /// <summary>
        /// 验证报告
        /// </summary>
        public class ValidationReport
        {
            /// <summary>
            /// 验证是否成功
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// 验证开始时间
            /// </summary>
            public DateTime StartTime { get; set; }

            /// <summary>
            /// 验证结束时间
            /// </summary>
            public DateTime EndTime { get; set; }

            /// <summary>
            /// 验证持续时间
            /// </summary>
            public TimeSpan Duration => EndTime - StartTime;

            /// <summary>
            /// 验证的文件列表
            /// </summary>
            public List<string> ValidatedFiles { get; set; } = new List<string>();

            /// <summary>
            /// 问题列表
            /// </summary>
            public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();

            /// <summary>
            /// 统计信息
            /// </summary>
            public ValidationStatistics Statistics { get; set; } = new ValidationStatistics();

            /// <summary>
            /// 错误数量
            /// </summary>
            public int ErrorCount => Issues.Count(i => i.Severity == ValidationSeverity.Error);

            /// <summary>
            /// 警告数量
            /// </summary>
            public int WarningCount => Issues.Count(i => i.Severity == ValidationSeverity.Warning);

            /// <summary>
            /// 信息数量
            /// </summary>
            public int InfoCount => Issues.Count(i => i.Severity == ValidationSeverity.Info);
        }

        /// <summary>
        /// 验证统计信息
        /// </summary>
        public class ValidationStatistics
        {
            /// <summary>
            /// 总文件数
            /// </summary>
            public int TotalFiles { get; set; }

            /// <summary>
            /// 总键数
            /// </summary>
            public int TotalKeys { get; set; }

            /// <summary>
            /// 文化数量
            /// </summary>
            public int CultureCount { get; set; }

            /// <summary>
            /// 缺失键数量
            /// </summary>
            public int MissingKeys { get; set; }

            /// <summary>
            /// 空值数量
            /// </summary>
            public int EmptyValues { get; set; }

            /// <summary>
            /// 重复键数量
            /// </summary>
            public int DuplicateKeys { get; set; }

            /// <summary>
            /// 未使用键数量
            /// </summary>
            public int UnusedKeys { get; set; }
        }

        /// <summary>
        /// 验证资源文件
        /// </summary>
        /// <param name="config">验证配置</param>
        /// <returns>验证报告</returns>
        public async Task<ValidationReport> ValidateAsync(ValidationConfig config)
        {
            var report = new ValidationReport
            {
                StartTime = DateTime.UtcNow
            };

            try
            {
                // 检查资源目录是否存在
                if (!Directory.Exists(config.ResourceDirectory))
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.StructureError,
                        Severity = ValidationSeverity.Error,
                        Message = $"资源目录不存在: {config.ResourceDirectory}",
                        Suggestion = "请创建资源目录并添加资源文件"
                    });
                    return report;
                }

                // 加载所有资源文件
                var resourceFiles = await LoadResourceFilesAsync(config, report);

                // 执行各种验证
                await ValidateFileStructureAsync(resourceFiles, config, report);
                await ValidateKeyCompletenessAsync(resourceFiles, config, report);
                await ValidateValueQualityAsync(resourceFiles, config, report);
                await ValidateFormatConsistencyAsync(resourceFiles, config, report);

                if (config.CheckUnusedKeys && config.CodeDirectories.Any())
                {
                    await ValidateKeyUsageAsync(resourceFiles, config, report);
                }

                // 更新统计信息
                UpdateStatistics(resourceFiles, report);

                report.Success = report.ErrorCount == 0;
            }
            catch (Exception ex)
            {
                report.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.StructureError,
                    Severity = ValidationSeverity.Error,
                    Message = $"验证过程中发生错误: {ex.Message}",
                    Suggestion = "请检查配置和文件权限"
                });
                report.Success = false;
            }
            finally
            {
                report.EndTime = DateTime.UtcNow;
            }

            return report;
        }

        /// <summary>
        /// 加载资源文件
        /// </summary>
        private async Task<Dictionary<string, JObject>> LoadResourceFilesAsync(ValidationConfig config, ValidationReport report)
        {
            var resourceFiles = new Dictionary<string, JObject>();

            foreach (var culture in config.Cultures)
            {
                var fileName = $"{culture.Name}.json";
                var filePath = Path.Combine(config.ResourceDirectory, fileName);

                if (!File.Exists(filePath))
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.StructureError,
                        Severity = ValidationSeverity.Warning,
                        FilePath = filePath,
                        Culture = culture.Name,
                        Message = $"资源文件不存在: {fileName}",
                        Suggestion = "请创建对应文化的资源文件"
                    });
                    continue;
                }

                try
                {
                    // 检查文件编码
                    if (config.CheckFileEncoding)
                    {
                        await ValidateFileEncodingAsync(filePath, report);
                    }

                    var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                    var json = JObject.Parse(content);
                    resourceFiles[culture.Name] = json;
                    report.ValidatedFiles.Add(filePath);
                }
                catch (JsonException ex)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.JsonSyntaxError,
                        Severity = ValidationSeverity.Error,
                        FilePath = filePath,
                        Culture = culture.Name,
                        Message = $"JSON语法错误: {ex.Message}",
                        Suggestion = "请检查JSON格式是否正确"
                    });
                }
                catch (Exception ex)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.StructureError,
                        Severity = ValidationSeverity.Error,
                        FilePath = filePath,
                        Culture = culture.Name,
                        Message = $"加载文件失败: {ex.Message}",
                        Suggestion = "请检查文件是否损坏或权限是否正确"
                    });
                }
            }

            return resourceFiles;
        }

        /// <summary>
        /// 验证文件编码
        /// </summary>
        private async Task ValidateFileEncodingAsync(string filePath, ValidationReport report)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath);

                // 检查BOM
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    report.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.EncodingIssue,
                        Severity = ValidationSeverity.Info,
                        FilePath = filePath,
                        Message = "文件包含UTF-8 BOM",
                        Suggestion = "建议使用无BOM的UTF-8编码"
                    });
                }
            }
            catch (Exception ex)
            {
                report.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.EncodingIssue,
                    Severity = ValidationSeverity.Warning,
                    FilePath = filePath,
                    Message = $"无法检查文件编码: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 验证文件结构
        /// </summary>
        private async Task ValidateFileStructureAsync(Dictionary<string, JObject> resourceFiles, ValidationConfig config, ValidationReport report)
        {
            foreach (var kvp in resourceFiles)
            {
                var culture = kvp.Key;
                var json = kvp.Value;

                // 检查重复键
                if (config.CheckDuplicateKeys)
                {
                    var allKeys = new HashSet<string>();
                    CheckDuplicateKeysRecursive(json, allKeys, culture, string.Empty, report);
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 递归检查重复键
        /// </summary>
        private void CheckDuplicateKeysRecursive(JToken token, HashSet<string> allKeys, string culture, string prefix, ValidationReport report)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    var fullKey = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

                    if (!allKeys.Add(fullKey))
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Type = ValidationIssueType.DuplicateKey,
                            Severity = ValidationSeverity.Error,
                            Culture = culture,
                            Key = fullKey,
                            Message = $"发现重复键: {fullKey}",
                            Suggestion = "请删除重复的键定义"
                        });
                    }

                    CheckDuplicateKeysRecursive(property.Value, allKeys, culture, fullKey, report);
                }
            }
        }

        /// <summary>
        /// 验证键的完整性
        /// </summary>
        private async Task ValidateKeyCompletenessAsync(Dictionary<string, JObject> resourceFiles, ValidationConfig config, ValidationReport report)
        {
            if (!resourceFiles.ContainsKey(config.ReferenceCulture.Name))
            {
                report.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.StructureError,
                    Severity = ValidationSeverity.Error,
                    Message = $"基准文化文件不存在: {config.ReferenceCulture.Name}",
                    Suggestion = "请创建基准文化的资源文件"
                });
                return;
            }

            var referenceKeys = GetAllKeysRecursive(resourceFiles[config.ReferenceCulture.Name]);

            foreach (var kvp in resourceFiles)
            {
                if (kvp.Key == config.ReferenceCulture.Name) continue;

                var culture = kvp.Key;
                var json = kvp.Value;
                var cultureKeys = GetAllKeysRecursive(json);

                // 检查缺失的键
                foreach (var referenceKey in referenceKeys)
                {
                    if (!cultureKeys.Contains(referenceKey))
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Type = ValidationIssueType.MissingKey,
                            Severity = ValidationSeverity.Warning,
                            Culture = culture,
                            Key = referenceKey,
                            Message = $"缺失键: {referenceKey}",
                            Suggestion = $"请在 {culture} 文化中添加此键的翻译"
                        });
                    }
                }

                // 检查多余的键
                foreach (var cultureKey in cultureKeys)
                {
                    if (!referenceKeys.Contains(cultureKey))
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Type = ValidationIssueType.UnusedKey,
                            Severity = ValidationSeverity.Info,
                            Culture = culture,
                            Key = cultureKey,
                            Message = $"多余键: {cultureKey}",
                            Suggestion = $"此键在基准文化 {config.ReferenceCulture.Name} 中不存在"
                        });
                    }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 递归获取所有键
        /// </summary>
        private HashSet<string> GetAllKeysRecursive(JToken token, string prefix = "")
        {
            var keys = new HashSet<string>();

            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (property.Name.StartsWith("_")) continue; // 跳过元数据

                    var fullKey = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

                    if (property.Value is JObject)
                    {
                        var nestedKeys = GetAllKeysRecursive(property.Value, fullKey);
                        keys.UnionWith(nestedKeys);
                    }
                    else
                    {
                        keys.Add(fullKey);
                    }
                }
            }

            return keys;
        }

        /// <summary>
        /// 验证值的质量
        /// </summary>
        private async Task ValidateValueQualityAsync(Dictionary<string, JObject> resourceFiles, ValidationConfig config, ValidationReport report)
        {
            foreach (var kvp in resourceFiles)
            {
                var culture = kvp.Key;
                var json = kvp.Value;

                ValidateValuesRecursive(json, culture, string.Empty, config, report);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 递归验证值
        /// </summary>
        private void ValidateValuesRecursive(JToken token, string culture, string prefix, ValidationConfig config, ValidationReport report)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (property.Name.StartsWith("_")) continue; // 跳过元数据

                    var fullKey = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

                    if (property.Value is JObject)
                    {
                        ValidateValuesRecursive(property.Value, culture, fullKey, config, report);
                    }
                    else if (property.Value is JValue value)
                    {
                        var stringValue = value.ToString();

                        // 检查空值
                        if (config.CheckEmptyValues && string.IsNullOrWhiteSpace(stringValue))
                        {
                            report.Issues.Add(new ValidationIssue
                            {
                                Type = ValidationIssueType.EmptyValue,
                                Severity = ValidationSeverity.Warning,
                                Culture = culture,
                                Key = fullKey,
                                Message = $"空值: {fullKey}",
                                Suggestion = "请为此键提供翻译文本"
                            });
                        }

                        // 检查格式化字符串
                        if (config.ValidateFormatStrings)
                        {
                            ValidateFormatString(stringValue, culture, fullKey, report);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 验证格式化字符串
        /// </summary>
        private void ValidateFormatString(string value, string culture, string key, ValidationReport report)
        {
            try
            {
                // 检查.NET格式化字符串
                var formatMatches = System.Text.RegularExpressions.Regex.Matches(value, @"\{(\d+)([^}]*)\}");
                var indices = new HashSet<int>();

                foreach (System.Text.RegularExpressions.Match match in formatMatches)
                {
                    if (int.TryParse(match.Groups[1].Value, out int index))
                    {
                        indices.Add(index);
                    }
                }

                // 检查索引是否连续
                if (indices.Count > 0)
                {
                    var maxIndex = indices.Max();
                    for (int i = 0; i <= maxIndex; i++)
                    {
                        if (!indices.Contains(i))
                        {
                            report.Issues.Add(new ValidationIssue
                            {
                                Type = ValidationIssueType.FormatStringMismatch,
                                Severity = ValidationSeverity.Warning,
                                Culture = culture,
                                Key = key,
                                Message = $"格式化参数索引不连续: 缺少 {{{i}}}",
                                Suggestion = "请确保格式化参数索引从0开始连续编号"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                report.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.InvalidFormat,
                    Severity = ValidationSeverity.Warning,
                    Culture = culture,
                    Key = key,
                    Message = $"格式化字符串验证失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 验证格式一致性
        /// </summary>
        private async Task ValidateFormatConsistencyAsync(Dictionary<string, JObject> resourceFiles, ValidationConfig config, ValidationReport report)
        {
            // 比较不同文化间的格式化参数是否一致
            var referenceJson = resourceFiles.GetValueOrDefault(config.ReferenceCulture.Name);
            if (referenceJson == null) return;

            var referenceFormats = GetFormatStrings(referenceJson);

            foreach (var kvp in resourceFiles)
            {
                if (kvp.Key == config.ReferenceCulture.Name) continue;

                var culture = kvp.Key;
                var json = kvp.Value;
                var cultureFormats = GetFormatStrings(json);

                foreach (var refFormat in referenceFormats)
                {
                    if (cultureFormats.TryGetValue(refFormat.Key, out var cultureFormat))
                    {
                        if (!AreFormatsCompatible(refFormat.Value, cultureFormat))
                        {
                            report.Issues.Add(new ValidationIssue
                            {
                                Type = ValidationIssueType.FormatStringMismatch,
                                Severity = ValidationSeverity.Error,
                                Culture = culture,
                                Key = refFormat.Key,
                                Message = $"格式化参数不匹配: 基准 '{refFormat.Value}' vs 当前 '{cultureFormat}'",
                                Suggestion = "请确保所有文化的格式化参数数量和类型一致"
                            });
                        }
                    }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 获取格式化字符串
        /// </summary>
        private Dictionary<string, string> GetFormatStrings(JToken token, string prefix = "")
        {
            var formats = new Dictionary<string, string>();

            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (property.Name.StartsWith("_")) continue;

                    var fullKey = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

                    if (property.Value is JObject)
                    {
                        var nested = GetFormatStrings(property.Value, fullKey);
                        foreach (var kvp in nested)
                        {
                            formats[kvp.Key] = kvp.Value;
                        }
                    }
                    else if (property.Value is JValue value)
                    {
                        var stringValue = value.ToString();
                        if (stringValue.Contains('{') && stringValue.Contains('}'))
                        {
                            formats[fullKey] = stringValue;
                        }
                    }
                }
            }

            return formats;
        }

        /// <summary>
        /// 检查格式是否兼容
        /// </summary>
        private bool AreFormatsCompatible(string format1, string format2)
        {
            var params1 = ExtractFormatParameters(format1);
            var params2 = ExtractFormatParameters(format2);

            return params1.SetEquals(params2);
        }

        /// <summary>
        /// 提取格式化参数
        /// </summary>
        private HashSet<int> ExtractFormatParameters(string format)
        {
            var parameters = new HashSet<int>();
            var matches = System.Text.RegularExpressions.Regex.Matches(format, @"\{(\d+)[^}]*\}");

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int index))
                {
                    parameters.Add(index);
                }
            }

            return parameters;
        }

        /// <summary>
        /// 验证键的使用情况
        /// </summary>
        private async Task ValidateKeyUsageAsync(Dictionary<string, JObject> resourceFiles, ValidationConfig config, ValidationReport report)
        {
            // 扫描代码文件中使用的键
            var usedKeys = new HashSet<string>();

            foreach (var directory in config.CodeDirectories)
            {
                if (Directory.Exists(directory))
                {
                    await ScanCodeDirectoryAsync(directory, usedKeys);
                }
            }

            // 检查未使用的键
            foreach (var kvp in resourceFiles)
            {
                var culture = kvp.Key;
                var allKeys = GetAllKeysRecursive(kvp.Value);

                foreach (var key in allKeys)
                {
                    if (!usedKeys.Contains(key))
                    {
                        report.Issues.Add(new ValidationIssue
                        {
                            Type = ValidationIssueType.UnusedKey,
                            Severity = ValidationSeverity.Info,
                            Culture = culture,
                            Key = key,
                            Message = $"未使用的键: {key}",
                            Suggestion = "考虑删除此键或确认是否确实需要"
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 扫描代码目录
        /// </summary>
        private async Task ScanCodeDirectoryAsync(string directory, HashSet<string> usedKeys)
        {
            var filePatterns = new[] { "*.cs", "*.xaml", "*.razor", "*.cshtml" };

            foreach (var pattern in filePatterns)
            {
                var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file, Encoding.UTF8);
                        var keyPatterns = new[]
                        {
                            @"GetString\([""']([^""']+)[""']\)",
                            @"LocalizeString\([""']([^""']+)[""']\)",
                            @"Localize\([""']([^""']+)[""']\)",
                            @"ResourceKey\s*=\s*[""']([^""']+)[""']"
                        };

                        foreach (var keyPattern in keyPatterns)
                        {
                            var matches = System.Text.RegularExpressions.Regex.Matches(content, keyPattern);
                            foreach (System.Text.RegularExpressions.Match match in matches)
                            {
                                if (match.Groups.Count > 1)
                                {
                                    usedKeys.Add(match.Groups[1].Value);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 忽略读取失败的文件
                    }
                }
            }
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics(Dictionary<string, JObject> resourceFiles, ValidationReport report)
        {
            report.Statistics.TotalFiles = resourceFiles.Count;
            report.Statistics.CultureCount = resourceFiles.Count;

            if (resourceFiles.Any())
            {
                report.Statistics.TotalKeys = resourceFiles.Values
                    .SelectMany(json => GetAllKeysRecursive(json))
                    .Distinct()
                    .Count();
            }

            report.Statistics.MissingKeys = report.Issues.Count(i => i.Type == ValidationIssueType.MissingKey);
            report.Statistics.EmptyValues = report.Issues.Count(i => i.Type == ValidationIssueType.EmptyValue);
            report.Statistics.DuplicateKeys = report.Issues.Count(i => i.Type == ValidationIssueType.DuplicateKey);
            report.Statistics.UnusedKeys = report.Issues.Count(i => i.Type == ValidationIssueType.UnusedKey);
        }

        /// <summary>
        /// 生成验证报告文本
        /// </summary>
        /// <param name="report">验证报告</param>
        /// <returns>报告文本</returns>
        public string GenerateReportText(ValidationReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("资源文件验证报告");
            sb.AppendLine("===================");
            sb.AppendLine($"验证时间: {report.StartTime:yyyy-MM-dd HH:mm:ss} - {report.EndTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"验证耗时: {report.Duration.TotalMilliseconds:F0} 毫秒");
            sb.AppendLine($"验证结果: {(report.Success ? "成功" : "失败")}");
            sb.AppendLine();

            sb.AppendLine("统计信息:");
            sb.AppendLine($"  文件数量: {report.Statistics.TotalFiles}");
            sb.AppendLine($"  文化数量: {report.Statistics.CultureCount}");
            sb.AppendLine($"  键总数: {report.Statistics.TotalKeys}");
            sb.AppendLine();

            sb.AppendLine("问题统计:");
            sb.AppendLine($"  错误: {report.ErrorCount}");
            sb.AppendLine($"  警告: {report.WarningCount}");
            sb.AppendLine($"  信息: {report.InfoCount}");
            sb.AppendLine();

            if (report.Issues.Any())
            {
                sb.AppendLine("详细问题:");
                var groupedIssues = report.Issues.GroupBy(i => i.Severity);

                foreach (var group in groupedIssues.OrderBy(g => g.Key))
                {
                    sb.AppendLine($"\n{group.Key}:");
                    foreach (var issue in group)
                    {
                        sb.AppendLine($"  [{issue.Type}] {issue.Message}");
                        if (!string.IsNullOrEmpty(issue.Key))
                            sb.AppendLine($"    键: {issue.Key}");
                        if (!string.IsNullOrEmpty(issue.Culture))
                            sb.AppendLine($"    文化: {issue.Culture}");
                        if (!string.IsNullOrEmpty(issue.Suggestion))
                            sb.AppendLine($"    建议: {issue.Suggestion}");
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }
    }
}