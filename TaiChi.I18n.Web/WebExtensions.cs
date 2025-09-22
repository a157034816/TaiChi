using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using TaiChi.I18n;

namespace TaiChi.I18n.Web
{
    /// <summary>
    /// Web应用本地化扩展方法
    /// 提供Web应用中的资源获取和语言管理
    /// </summary>
    public static class WebExtensions
    {
        private static ILocalizationService? _localizationService;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取或设置本地化服务
        /// </summary>
        public static ILocalizationService? LocalizationService
        {
            get => _localizationService;
            set
            {
                lock (_lock)
                {
                    if (_localizationService != null)
                    {
                        _localizationService.LanguageChanged -= OnLanguageChanged;
                    }

                    _localizationService = value;

                    if (_localizationService != null)
                    {
                        _localizationService.LanguageChanged += OnLanguageChanged;
                    }
                }
            }
        }

        /// <summary>
        /// 语言变化事件处理
        /// </summary>
        private static void OnLanguageChanged(object? sender, CultureInfo culture)
        {
            // 可以在这里添加全局的语言变化处理逻辑
            // 例如：更新所有客户端的语言状态等
        }

        /// <summary>
        /// 为HttpContext添加本地化服务
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="localizationService">本地化服务</param>
        public static void AddLocalizationService(this HttpContext context, ILocalizationService localizationService)
        {
            context.Items["LocalizationService"] = localizationService;
        }

        /// <summary>
        /// 从HttpContext获取本地化服务
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <returns>本地化服务或null</returns>
        public static ILocalizationService? GetLocalizationService(this HttpContext context)
        {
            return context.Items["LocalizationService"] as ILocalizationService;
        }

        /// <summary>
        /// 从HttpContext获取本地化字符串
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        /// <returns>本地化字符串</returns>
        public static string? Localize(this HttpContext context, string key, params object[] args)
        {
            var service = context.GetLocalizationService();
            if (service == null)
                return key;

            try
            {
                return service.GetFormattedString(key, args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Web localization failed for key '{key}': {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// 从HttpContext获取本地化字符串（异步）
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        /// <returns>本地化字符串</returns>
        public static async Task<string?> LocalizeAsync(this HttpContext context, string key, params object[] args)
        {
            var service = context.GetLocalizationService();
            if (service == null)
                return await Task.FromResult<string?>(key);

            try
            {
                return await service.GetStringAsync(key);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Async web localization failed for key '{key}': {ex.Message}");
                return await Task.FromResult<string?>(key);
            }
        }

        /// <summary>
        /// 从HttpContext获取本地化图片路径
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="key">资源键</param>
        /// <returns>图片路径或null</returns>
        public static string? LocalizeImage(this HttpContext context, string key)
        {
            var service = context.GetLocalizationService();
            return service?.GetImagePath(key);
        }

        /// <summary>
        /// 从HttpContext获取本地化音频路径
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="key">资源键</param>
        /// <returns>音频路径或null</returns>
        public static string? LocalizeAudio(this HttpContext context, string key)
        {
            var service = context.GetLocalizationService();
            return service?.GetAudioPath(key);
        }

        /// <summary>
        /// 获取请求的文化信息
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <returns>文化信息</returns>
        public static CultureInfo GetRequestCulture(this HttpContext context)
        {
            // 优先使用查询参数中的文化
            var cultureFromQuery = context.Request.Query["culture"].FirstOrDefault();
            if (!string.IsNullOrEmpty(cultureFromQuery))
            {
                try
                {
                    return new CultureInfo(cultureFromQuery);
                }
                catch (CultureNotFoundException)
                {
                    // 忽略无效的文化名称
                }
            }

            // 其次使用Cookie中的文化
            var cultureFromCookie = context.Request.Cookies[".AspNetCore.Culture"];
            if (!string.IsNullOrEmpty(cultureFromCookie))
            {
                try
                {
                    var cookieValue = WebUtility.UrlDecode(cultureFromCookie);
                    if (cookieValue?.StartsWith("c=") == true)
                    {
                        var cultureName = cookieValue.Substring(2);
                        return new CultureInfo(cultureName);
                    }
                }
                catch (CultureNotFoundException)
                {
                    // 忽略无效的文化名称
                }
            }

            // 然后使用Accept-Language头
            var acceptLanguage = context.Request.Headers["Accept-Language"].FirstOrDefault();
            if (!string.IsNullOrEmpty(acceptLanguage))
            {
                try
                {
                    // 解析Accept-Language头，获取首选语言
                    var languages = acceptLanguage.Split(',')
                        .Select(l => l.Split(';')[0].Trim())
                        .Where(l => !string.IsNullOrEmpty(l));

                    foreach (var lang in languages)
                    {
                        try
                        {
                            return new CultureInfo(lang);
                        }
                        catch (CultureNotFoundException)
                        {
                            continue;
                        }
                    }
                }
                catch
                {
                    // 忽略解析错误
                }
            }

            // 最后使用默认文化
            return LocalizationService?.CurrentCulture ?? new CultureInfo("zh-CN");
        }

        /// <summary>
        /// 设置请求的文化信息
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="culture">文化信息</param>
        /// <param name="setCookie">是否设置Cookie</param>
        public static void SetRequestCulture(this HttpContext context, CultureInfo culture, bool setCookie = true)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));

            // 设置当前线程的文化
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            // 设置Cookie
            if (setCookie)
            {
                context.Response.Cookies.Append(
                    ".AspNetCore.Culture",
                    $"c={culture.Name}",
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        HttpOnly = true,
                        Secure = context.Request.IsHttps,
                        SameSite = SameSiteMode.Lax
                    });
            }

            // 设置响应头
            context.Response.Headers["Content-Language"] = culture.Name;
        }

        /// <summary>
        /// 为HttpRequest添加本地化支持
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <returns>本地化服务或null</returns>
        public static ILocalizationService? GetLocalizationService(this HttpRequest request)
        {
            return request.HttpContext.GetLocalizationService();
        }

        /// <summary>
        /// 从HttpRequest获取本地化字符串
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        /// <returns>本地化字符串</returns>
        public static string? Localize(this HttpRequest request, string key, params object[] args)
        {
            return request.HttpContext.Localize(key, args);
        }

        /// <summary>
        /// 从HttpRequest获取本地化图片路径
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <param name="key">资源键</param>
        /// <returns>图片路径或null</returns>
        public static string? LocalizeImage(this HttpRequest request, string key)
        {
            return request.HttpContext.LocalizeImage(key);
        }

        /// <summary>
        /// 从HttpRequest获取本地化音频路径
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <param name="key">资源键</param>
        /// <returns>音频路径或null</returns>
        public static string? LocalizeAudio(this HttpRequest request, string key)
        {
            return request.HttpContext.LocalizeAudio(key);
        }

        /// <summary>
        /// 为HttpResponse添加本地化支持
        /// </summary>
        /// <param name="response">HTTP响应</param>
        /// <returns>本地化服务或null</returns>
        public static ILocalizationService? GetLocalizationService(this HttpResponse response)
        {
            return response.HttpContext.GetLocalizationService();
        }

        /// <summary>
        /// 从HttpResponse获取本地化字符串
        /// </summary>
        /// <param name="response">HTTP响应</param>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        /// <returns>本地化字符串</returns>
        public static string? Localize(this HttpResponse response, string key, params object[] args)
        {
            return response.HttpContext.Localize(key, args);
        }

        /// <summary>
        /// 获取请求的UI文化信息
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <returns>UI文化信息</returns>
        public static CultureInfo GetUICulture(this HttpRequest request)
        {
            return request.HttpContext.GetRequestCulture();
        }

        /// <summary>
        /// 获取请求的当前文化信息
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <returns>当前文化信息</returns>
        public static CultureInfo GetCurrentCulture(this HttpRequest request)
        {
            return request.HttpContext.GetRequestCulture();
        }

        /// <summary>
        /// 检查是否支持指定的文化
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <param name="cultureName">文化名称</param>
        /// <returns>是否支持</returns>
        public static bool IsCultureSupported(this HttpRequest request, string cultureName)
        {
            var service = request.GetLocalizationService();
            if (service == null) return false;

            var supportedCultures = service.GetSupportedCultures();
            return supportedCultures.Any(c => c.Name.Equals(cultureName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取所有支持的文化信息
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <returns>支持的文化列表</returns>
        public static List<CultureInfo> GetSupportedCultures(this HttpRequest request)
        {
            var service = request.GetLocalizationService();
            return service?.GetSupportedCultures() ?? new List<CultureInfo>();
        }

        /// <summary>
        /// 创建本地化URL
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <param name="culture">目标文化</param>
        /// <param name="path">路径（可选）</param>
        /// <returns>本地化URL</returns>
        public static string CreateLocalizedUrl(this HttpRequest request, CultureInfo culture, string? path = null)
        {
            var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
            var targetPath = path ?? request.Path;

            // 添加文化查询参数
            var separator = targetPath.Contains("?") ? "&" : "?";
            return $"{baseUrl}{targetPath}{separator}culture={culture.Name}";
        }

        /// <summary>
        /// 注册Web本地化服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="config">本地化配置</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddTaiChiLocalization(this IServiceCollection services, LocalizationConfig config)
        {
            // 注册本地化服务
            services.AddSingleton<ILocalizationService>(provider => new LocalizationService(config));
            services.AddSingleton(config);

            // 注册Web本地化扩展
            services.AddSingleton<WebLocalizationExtensions>();

            return services;
        }

        /// <summary>
        /// 注册Web本地化服务（使用默认配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddTaiChiLocalization(this IServiceCollection services)
        {
            return services.AddTaiChiLocalization(LocalizationConfig.Default);
        }

        /// <summary>
        /// 初始化Web本地化
        /// </summary>
        /// <param name="localizationService">本地化服务</param>
        /// <param name="config">配置</param>
        public static void InitializeWebLocalization(ILocalizationService localizationService, LocalizationConfig? config = null)
        {
            if (localizationService == null)
                throw new ArgumentNullException(nameof(localizationService));

            LocalizationService = localizationService;

            if (config != null)
            {
                localizationService.EnableFileMonitoring(config.EnableFileMonitoring);
            }
        }

        /// <summary>
        /// 清理Web本地化资源
        /// </summary>
        public static void CleanupWebLocalization()
        {
            if (_localizationService != null)
            {
                _localizationService.LanguageChanged -= OnLanguageChanged;
                _localizationService = null;
            }
        }
    }

    /// <summary>
    /// Web本地化扩展辅助类
    /// </summary>
    internal class WebLocalizationExtensions
    {
        // 可以在这里添加Web特定的本地化辅助方法
    }
}