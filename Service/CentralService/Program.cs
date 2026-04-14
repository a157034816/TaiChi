using CentralService.Admin;
using CentralService.Admin.Config;
using CentralService.Admin.Data;
using CentralService.Admin.Security;
using CentralService.Internal;
using CentralService.Services;
using CentralService.Services.ServiceCircuiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;
using System.Reflection;

namespace CentralService;

public class Program
{
    public static void Main(string[] args)
    {
        var isContainer = string.Equals(
            Environment.GetEnvironmentVariable("TAICHI_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var builder = WebApplication.CreateBuilder(args);

        if (isContainer)
        {
            builder.Configuration.AddJsonFile("appsettings.Container.json", optional: true, reloadOnChange: false);
        }

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();
        builder.Services.AddHttpClient();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = builder.Configuration.GetValue<string>("CentralServiceAuth:CookieName") ?? "CentralServiceAdmin.Auth";

                var cookiePath = builder.Configuration.GetValue<string?>("CentralServiceAuth:CookiePath")?.Trim();
                if (!string.IsNullOrWhiteSpace(cookiePath))
                {
                    options.Cookie.Path = cookiePath;
                }

                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);

                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                };
            });
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permissionKey in CentralServicePermissions.AllKeys)
            {
                options.AddPolicy(permissionKey, policy => policy.RequireClaim(CentralServiceClaimTypes.Permission, permissionKey));
            }
        });

        // 注册服务注册表（单例模式）
        builder.Services.AddSingleton<ServiceRegistry>();
        builder.Services.AddSingleton<CentralServiceBackgroundTaskMonitor>();
        builder.Services.Configure<ServiceCircuitTomlOptions>(builder.Configuration.GetSection("ServiceCircuitToml"));
        builder.Services.AddSingleton<ServiceCircuitTomlStore>();
        builder.Services.Configure<ServiceCircuitJsonOptions>(builder.Configuration.GetSection("ServiceCircuitJson"));
        builder.Services.AddSingleton<ServiceCircuitJsonStore>();
        builder.Services.AddSingleton<ServiceCircuitRuntimeStateStore>();
        builder.Services.AddSingleton<ServiceAccessService>();
        builder.Services.AddHostedService<ServiceCircuitCleanupBackgroundService>();

        // 注册服务健康检查器（托管服务）
        builder.Services.AddHostedService<ServiceHealthChecker>();

        // 注册服务网络评估器（单例模式）
        builder.Services.AddSingleton<ServiceNetworkEvaluator>();

        // 注册服务网络评估后台服务（托管服务）
        builder.Services.AddHostedService<ServiceNetworkEvaluatorBackgroundService>();

        // 健康检查
        builder.Services.AddHealthChecks()
            .AddCheck<CentralServiceAdminDbHealthCheck>("central_service_admin_db")
            .AddCheck<CentralServiceBackgroundTasksHealthCheck>("central_service_background_tasks");

        // 中心服务后台数据库与 RBAC
        builder.Services.AddHttpContextAccessor();
        builder.Services.Configure<CentralServiceAdminDbOptions>(builder.Configuration.GetSection("CentralServiceAdminDb"));
        builder.Services.Configure<CentralServiceAdminSeedOptions>(builder.Configuration.GetSection("CentralServiceAdminSeed"));
        builder.Services.Configure<ManagedWebAppHostOptions>(builder.Configuration.GetSection("ManagedWebApps"));
        builder.Services.AddSingleton<CentralServiceAdminDb>();
        builder.Services.AddSingleton<CentralServiceRbacService>();
        builder.Services.AddSingleton<CentralServiceAuditService>();
        builder.Services.AddSingleton<IManagedProcessLifetimeBinder>(_ =>
            OperatingSystem.IsWindows()
                ? new WindowsJobObjectManagedProcessLifetimeBinder()
                : new NoOpManagedProcessLifetimeBinder());
        builder.Services.AddHostedService<CentralServiceAdminInitializer>();
        builder.Services.AddSingleton<CentralServiceRuntimeConfigProvider>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<CentralServiceRuntimeConfigProvider>());
        builder.Services.AddHostedService<ManagedWebAppSupervisor>();
        builder.Services.AddSingleton<CentralServiceConfigService>();

        // 服务发现选择器（支持配置覆盖）
        builder.Services.AddSingleton<CentralServiceServiceSelector>();

        // 添加Swagger服务
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "中心服务API",
                Version = "v1",
                Description = "服务注册、发现与健康检查等中心服务能力"
            });

            // 设置XML注释文件路径
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });

        // 添加跨域支持（运行时 API 默认放开；后台管理 API 需显式指定允许来源并启用凭据）
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });

            options.AddPolicy("CentralServiceAdmin", policy =>
            {
                var allowedOrigins = builder.Configuration.GetSection("Cors:CentralServiceAdmin:AllowedOrigins").Get<string[]>();
                if (allowedOrigins?.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
            });
        });

        var app = builder.Build();
        var httpsRedirectionEnabled = builder.Configuration.GetValue<bool?>("CentralServiceHttpsRedirection:Enabled") ?? true;

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "中心服务API v1"));
        }
        else
        {
            app.UseExceptionHandler();
            app.UseHsts();
        }

        // 反向代理适配：在携带代理密钥时恢复 Scheme / 客户端 IP，避免直连伪造 X-Forwarded-* 头。
        app.UseMiddleware<CentralServiceReverseProxyMiddleware>();

        // 启用跨域（默认策略 + [EnableCors] 覆盖）
        app.UseCors();

        // 启用 WebSocket（用于周边服务心跳通道 /api/Service/heartbeat/ws）。
        app.UseWebSockets();

        if (!app.Environment.IsDevelopment() && httpsRedirectionEnabled)
        {
            app.UseWhen(
                CentralServiceHttpsRedirectionPolicy.ShouldApply,
                branch => branch.UseHttpsRedirection());
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks("/health");
        app.MapControllers();

        app.Run();
    }
}
