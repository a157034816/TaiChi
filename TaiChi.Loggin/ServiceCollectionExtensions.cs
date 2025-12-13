using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TaiChi.Loggin
{
    /// <summary>
    /// <see cref="IServiceCollection"/> 扩展方法。
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 为 <see cref="IServiceCollection"/> 注册日志（Microsoft.Extensions.Logging 抽象，Serilog 实现）。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="serilogLogger">Serilog 日志记录器。</param>
        /// <param name="dispose">释放 Provider 时是否同时释放 Serilog logger。</param>
        /// <param name="clearProviders">是否清空默认日志 Provider。</param>
        /// <returns>服务集合。</returns>
        /// <exception cref="ArgumentNullException">services 或 serilogLogger 为空时抛出。</exception>
        public static IServiceCollection AddLoggin(
            this IServiceCollection services,
            Serilog.ILogger serilogLogger,
            bool dispose = true,
            bool clearProviders = true)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (serilogLogger == null)
            {
                throw new ArgumentNullException(nameof(serilogLogger));
            }

            services.AddLogging(builder =>
            {
                if (clearProviders)
                {
                    builder.ClearProviders();
                }

                builder.AddSerilogProvider(serilogLogger, dispose);
            });

            return services;
        }

        /// <summary>
        /// 使用回调配置创建 Serilog logger，并为 <see cref="IServiceCollection"/> 注册日志。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configure">Serilog 配置回调。</param>
        /// <param name="minimumLevel">最低日志级别。</param>
        /// <param name="dispose">释放 Provider 时是否同时释放 Serilog logger。</param>
        /// <param name="clearProviders">是否清空默认日志 Provider。</param>
        /// <returns>服务集合。</returns>
        /// <exception cref="ArgumentNullException">services 或 configure 为空时抛出。</exception>
        public static IServiceCollection AddLoggin(
            this IServiceCollection services,
            Action<Serilog.LoggerConfiguration> configure,
            Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information,
            bool dispose = true,
            bool clearProviders = true)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var serilogLogger = TaiChiLogginFactory.CreateSerilogLogger(configure, minimumLevel);
            return services.AddLoggin(serilogLogger, dispose, clearProviders);
        }
    }
}
