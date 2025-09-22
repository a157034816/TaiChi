using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TaiChi.I18n;

namespace TaiChi.I18n.Web
{
    /// <summary>
    /// ASP.NET Core资源中间件
    /// 处理Web请求中的本地化资源和语言切换
    /// </summary>
    public class ResourceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly LocalizationConfig _config;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="next">下一个中间件</param>
        /// <param name="config">本地化配置</param>
        /// <param name="serviceProvider">服务提供者</param>
        public ResourceMiddleware(RequestDelegate next, IOptions<LocalizationConfig> config, IServiceProvider serviceProvider)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// 处理HTTP请求
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <returns>任务</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // 获取或创建本地化服务
            var localizationService = GetOrCreateLocalizationService(context);

            // 添加到HTTP上下文
            context.AddLocalizationService(localizationService);

            // 处理语言检测和设置
            await ProcessRequestCultureAsync(context, localizationService);

            // 添加本地化头信息
            AddLocalizationHeaders(context, localizationService);

            // 处理资源API请求
            if (await HandleResourceApiRequest(context, localizationService))
            {
                return; // 如果是资源API请求，直接返回响应
            }

            // 继续处理请求
            await _next(context);
        }

        /// <summary>
        /// 获取或创建本地化服务
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <returns>本地化服务</returns>
        private ILocalizationService GetOrCreateLocalizationService(HttpContext context)
        {
            // 尝试从服务容器中获取
            var localizationService = context.RequestServices.GetService<ILocalizationService>();

            if (localizationService == null)
            {
                // 如果不存在，创建新的服务实例
                localizationService = new LocalizationService(_config);

                // 初始化Web本地化
                TaiChi.I18n.Web.WebExtensions.InitializeWebLocalization(localizationService, _config);
            }

            return localizationService;
        }

        /// <summary>
        /// 处理请求文化检测和设置
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="localizationService">本地化服务</param>
        private async Task ProcessRequestCultureAsync(HttpContext context, ILocalizationService localizationService)
        {
            var requestCulture = context.GetRequestCulture();

            // 验证是否为支持的文化
            var supportedCultures = localizationService.GetSupportedCultures();
            if (!supportedCultures.Any(c => c.Name.Equals(requestCulture.Name, StringComparison.OrdinalIgnoreCase)))
            {
                // 如果不支持，使用默认文化
                requestCulture = _config.DefaultCulture ?? new CultureInfo("zh-CN");
            }

            // 设置本地化服务的当前文化
            localizationService.SetLanguage(requestCulture);

            // 设置HTTP上下文的文化信息
            context.SetRequestCulture(requestCulture);

            // 如果文化发生变化，重新加载资源
            if (localizationService.CurrentCulture.Name != requestCulture.Name)
            {
                await localizationService.ReloadResourcesAsync();
            }
        }

        /// <summary>
        /// 添加本地化头信息
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="localizationService">本地化服务</param>
        private void AddLocalizationHeaders(HttpContext context, ILocalizationService localizationService)
        {
            // 添加Content-Language头
            context.Response.Headers["Content-Language"] = localizationService.CurrentCulture.Name;

            // 添加支持的语言列表
            var supportedCultures = localizationService.GetSupportedCultures();
            var supportedLanguages = string.Join(", ", supportedCultures.Select(c => c.Name));
            context.Response.Headers["X-Supported-Languages"] = supportedLanguages;

            // 添加Vary头，指示响应基于Accept-Language变化
            context.Response.Headers["Vary"] = "Accept-Language, Cookie";
        }

        /// <summary>
        /// 处理资源API请求
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="localizationService">本地化服务</param>
        /// <returns>是否已处理请求</returns>
        private async Task<bool> HandleResourceApiRequest(HttpContext context, ILocalizationService localizationService)
        {
            var path = context.Request.Path.Value;

            // 检查是否为资源API请求
            if (path.StartsWith("/api/resources/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/');
                if (segments.Length >= 4)
                {
                    var resourceType = segments[3].ToLowerInvariant();
                    var resourceKey = string.Join("/", segments.Skip(4));

                    switch (resourceType)
                    {
                        case "string":
                            await HandleStringResourceRequest(context, localizationService, resourceKey);
                            return true;

                        case "image":
                            await HandleImageResourceRequest(context, localizationService, resourceKey);
                            return true;

                        case "audio":
                            await HandleAudioResourceRequest(context, localizationService, resourceKey);
                            return true;

                        case "all":
                            await HandleAllResourcesRequest(context, localizationService);
                            return true;

                        case "cultures":
                            await HandleCulturesRequest(context, localizationService);
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 处理字符串资源请求
        /// </summary>
        private async Task HandleStringResourceRequest(HttpContext context, ILocalizationService localizationService, string key)
        {
            var culture = GetRequestCulture(context, localizationService);
            var value = await localizationService.GetStringAsync(key, culture);

            var response = new
            {
                Key = key,
                Value = value ?? key,
                Culture = culture.Name,
                Found = value != null
            };

            await WriteJsonResponse(context, response);
        }

        /// <summary>
        /// 处理图片资源请求
        /// </summary>
        private async Task HandleImageResourceRequest(HttpContext context, ILocalizationService localizationService, string key)
        {
            var culture = GetRequestCulture(context, localizationService);
            var path = localizationService.GetImagePath(key, culture);

            var response = new
            {
                Key = key,
                Path = path,
                Culture = culture.Name,
                Found = !string.IsNullOrEmpty(path)
            };

            await WriteJsonResponse(context, response);
        }

        /// <summary>
        /// 处理音频资源请求
        /// </summary>
        private async Task HandleAudioResourceRequest(HttpContext context, ILocalizationService localizationService, string key)
        {
            var culture = GetRequestCulture(context, localizationService);
            var path = localizationService.GetAudioPath(key, culture);

            var response = new
            {
                Key = key,
                Path = path,
                Culture = culture.Name,
                Found = !string.IsNullOrEmpty(path)
            };

            await WriteJsonResponse(context, response);
        }

        /// <summary>
        /// 处理所有资源请求
        /// </summary>
        private async Task HandleAllResourcesRequest(HttpContext context, ILocalizationService localizationService)
        {
            var culture = GetRequestCulture(context, localizationService);
            var keys = localizationService.GetAllKeys(culture);

            var response = new
            {
                Culture = culture.Name,
                Resources = keys.Select(key => new
                {
                    Key = key,
                    Value = localizationService.GetString(key, culture)
                }).ToList()
            };

            await WriteJsonResponse(context, response);
        }

        /// <summary>
        /// 处理文化列表请求
        /// </summary>
        private async Task HandleCulturesRequest(HttpContext context, ILocalizationService localizationService)
        {
            var cultures = localizationService.GetSupportedCultures();
            var currentCulture = localizationService.CurrentCulture;

            var response = new
            {
                CurrentCulture = currentCulture.Name,
                Cultures = cultures.Select(c => new
                {
                    Name = c.Name,
                    DisplayName = c.DisplayName,
                    NativeName = c.NativeName,
                    IsCurrent = c.Name.Equals(currentCulture.Name, StringComparison.OrdinalIgnoreCase)
                }).ToList()
            };

            await WriteJsonResponse(context, response);
        }

        /// <summary>
        /// 获取请求的文化信息
        /// </summary>
        private CultureInfo GetRequestCulture(HttpContext context, ILocalizationService localizationService)
        {
            // 优先使用查询参数中的文化
            var cultureParam = context.Request.Query["culture"].FirstOrDefault();
            if (!string.IsNullOrEmpty(cultureParam))
            {
                try
                {
                    return new CultureInfo(cultureParam);
                }
                catch (CultureNotFoundException)
                {
                    // 忽略无效的文化名称
                }
            }

            // 使用当前文化
            return localizationService.CurrentCulture;
        }

        /// <summary>
        /// 写入JSON响应
        /// </summary>
        private async Task WriteJsonResponse(HttpContext context, object response)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status200OK;

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(response, Newtonsoft.Json.Formatting.Indented);
            await context.Response.WriteAsync(json);
        }

        /// <summary>
        /// 处理OPTIONS预检请求
        /// </summary>
        private bool HandleOptionsRequest(HttpContext context)
        {
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
                context.Response.Headers["Access-Control-Max-Age"] = "86400";
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 中间件扩展方法
    /// </summary>
    public static class ResourceMiddlewareExtensions
    {
        /// <summary>
        /// 添加资源中间件
        /// </summary>
        /// <param name="builder">应用构建器</param>
        /// <returns>应用构建器</returns>
        public static IApplicationBuilder UseTaiChiLocalization(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ResourceMiddleware>();
        }

        /// <summary>
        /// 添加资源中间件（自定义配置）
        /// </summary>
        /// <param name="builder">应用构建器</param>
        /// <param name="config">本地化配置</param>
        /// <returns>应用构建器</returns>
        public static IApplicationBuilder UseTaiChiLocalization(this IApplicationBuilder builder, LocalizationConfig config)
        {
            return builder.UseMiddleware<ResourceMiddleware>(config);
        }
    }
}