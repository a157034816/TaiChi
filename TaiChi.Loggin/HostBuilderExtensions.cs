using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaiChi.Loggin
{
    /// <summary>
    /// <see cref="IHostBuilder"/> 扩展方法。
    /// </summary>
    public static class HostBuilderExtensions
    {
        /// <summary>
        /// 为 <see cref="IHostBuilder"/> 一键集成日志（Microsoft.Extensions.Logging 抽象，Serilog 实现）。
        /// </summary>
        /// <param name="builder">Host 构建器。</param>
        /// <param name="serilogLogger">Serilog 日志记录器。</param>
        /// <param name="dispose">释放 Host 时是否同时释放 Serilog logger。</param>
        /// <param name="clearProviders">是否清空默认日志 Provider。</param>
        /// <returns>Host 构建器。</returns>
        /// <exception cref="ArgumentNullException">builder 或 serilogLogger 为空时抛出。</exception>
        public static IHostBuilder UseLoggin(
            this IHostBuilder builder,
            Serilog.ILogger serilogLogger,
            bool dispose = true,
            bool clearProviders = true)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (serilogLogger == null)
            {
                throw new ArgumentNullException(nameof(serilogLogger));
            }

            builder.ConfigureLogging((_, logging) =>
            {
                if (clearProviders)
                {
                    logging.ClearProviders();
                }

                logging.AddSerilogProvider(serilogLogger, dispose);
            });

            return builder;
        }

        /// <summary>
        /// 使用回调配置创建 Serilog logger，并为 <see cref="IHostBuilder"/> 一键集成日志。
        /// </summary>
        /// <param name="builder">Host 构建器。</param>
        /// <param name="configure">Serilog 配置回调。</param>
        /// <param name="minimumLevel">最低日志级别。</param>
        /// <param name="dispose">释放 Host 时是否同时释放 Serilog logger。</param>
        /// <param name="clearProviders">是否清空默认日志 Provider。</param>
        /// <returns>Host 构建器。</returns>
        /// <exception cref="ArgumentNullException">builder 或 configure 为空时抛出。</exception>
        public static IHostBuilder UseLoggin(
            this IHostBuilder builder,
            Action<Serilog.LoggerConfiguration> configure,
            Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information,
            bool dispose = true,
            bool clearProviders = true)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var serilogLogger = TaiChiLogginFactory.CreateSerilogLogger(configure, minimumLevel);
            return builder.UseLoggin(serilogLogger, dispose, clearProviders);
        }
    }
}

