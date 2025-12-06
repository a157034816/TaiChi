using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace TaiChi.I18n
{
    /// <summary>
    /// 本地化服务接口
    /// 定义本地化服务的标准操作和事件
    /// </summary>
    public interface ILocalizationService : IDisposable
    {
        /// <summary>
        /// 当前语言文化
        /// </summary>
        CultureInfo CurrentCulture { get; }

        /// <summary>
        /// 默认语言文化
        /// </summary>
        CultureInfo DefaultCulture { get; }

        /// <summary>
        /// 语言切换事件
        /// </summary>
        event EventHandler<CultureInfo> LanguageChanged;

        /// <summary>
        /// 获取字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>本地化字符串</returns>
        string GetString(string key, CultureInfo? culture = null);

        /// <summary>
        /// 异步获取字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>本地化字符串</returns>
        Task<string> GetStringAsync(string key, CultureInfo? culture = null);

        /// <summary>
        /// 获取格式化字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        /// <returns>格式化后的本地化字符串</returns>
        string GetFormattedString(string key, params object[] args);

        /// <summary>
        /// 获取格式化字符串资源
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化</param>
        /// <param name="args">格式化参数</param>
        /// <returns>格式化后的本地化字符串</returns>
        string GetFormattedString(string key, CultureInfo? culture, params object[] args);

        /// <summary>
        /// 获取图片资源路径
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>图片资源路径</returns>
        string? GetImagePath(string key, CultureInfo? culture = null);

        /// <summary>
        /// 获取音频资源路径
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>音频资源路径</returns>
        string? GetAudioPath(string key, CultureInfo? culture = null);

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>资源是否存在</returns>
        bool ResourceExists(string key, CultureInfo? culture = null);

        /// <summary>
        /// 获取所有支持的文化
        /// </summary>
        /// <returns>支持的文化列表</returns>
        List<CultureInfo> GetSupportedCultures();

        /// <summary>
        /// 切换语言
        /// </summary>
        /// <param name="cultureName">文化名称</param>
        /// <returns>切换是否成功</returns>
        bool SetLanguage(string cultureName);

        /// <summary>
        /// 切换语言
        /// </summary>
        /// <param name="culture">文化信息</param>
        /// <returns>切换是否成功</returns>
        bool SetLanguage(CultureInfo culture);

        /// <summary>
        /// 重置为默认语言
        /// </summary>
        void ResetToDefault();

        /// <summary>
        /// 清理资源缓存
        /// </summary>
        void ClearCache();

        /// <summary>
        /// 重载资源文件
        /// </summary>
        void ReloadResources();

        /// <summary>
        /// 异步重载资源文件
        /// </summary>
        Task ReloadResourcesAsync();

        /// <summary>
        /// 监控资源文件变化（如果启用）
        /// </summary>
        /// <param name="enable">是否启用监控</param>
        void EnableFileMonitoring(bool enable);

        /// <summary>
        /// 获取所有资源键
        /// </summary>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>资源键集合</returns>
        IEnumerable<string> GetAllKeys(CultureInfo? culture = null);
    }

    /// <summary>
    /// 扩展接口：提供更高级的本地化功能
    /// </summary>
    public interface ILocalizationServiceAdvanced : ILocalizationService
    {
        /// <summary>
        /// 获取指定前缀的资源键
        /// </summary>
        /// <param name="prefix">键前缀</param>
        /// <param name="culture">指定文化（可选）</param>
        /// <returns>匹配的资源键集合</returns>
        IEnumerable<string> GetKeysWithPrefix(string prefix, CultureInfo? culture = null);

        /// <summary>
        /// 验证资源文件完整性
        /// </summary>
        /// <returns>验证结果报告</returns>
        ResourceValidationResult ValidateResources();

        /// <summary>
        /// 导出资源到指定格式
        /// </summary>
        /// <param name="format">导出格式</param>
        /// <param name="outputPath">输出路径</param>
        /// <returns>导出是否成功</returns>
        bool ExportResources(ResourceExportFormat format, string outputPath);

        /// <summary>
        /// 从指定格式导入资源
        /// </summary>
        /// <param name="format">导入格式</param>
        /// <param name="inputPath">输入路径</param>
        /// <returns>导入是否成功</returns>
        bool ImportResources(ResourceExportFormat format, string inputPath);
    }

    /// <summary>
    /// 资源验证结果
    /// </summary>
    public class ResourceValidationResult
    {
        /// <summary>
        /// 是否验证通过
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 缺失的资源键
        /// </summary>
        public List<string> MissingKeys { get; set; } = new();

        /// <summary>
        /// 格式错误
        /// </summary>
        public List<string> FormatErrors { get; set; } = new();

        /// <summary>
        /// 文化不匹配的资源
        /// </summary>
        public List<string> CultureMismatches { get; set; } = new();

        /// <summary>
        /// 其他错误信息
        /// </summary>
        public List<string> OtherErrors { get; set; } = new();
    }

    /// <summary>
    /// 资源导出格式
    /// </summary>
    public enum ResourceExportFormat
    {
        /// <summary>
        /// JSON格式
        /// </summary>
        Json,

        /// <summary>
        /// ResX格式
        /// </summary>
        ResX,

        /// <summary>
        /// CSV格式
        /// </summary>
        Csv,

        /// <summary>
        /// XML格式
        /// </summary>
        Xml
    }
}