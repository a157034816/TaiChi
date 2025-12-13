using System;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;

namespace TaiChi.Loggin
{
    /// <summary>
    /// TaiChi 生态日志工厂（Microsoft.Extensions.Logging 作为统一抽象/门面，Serilog 作为实现）。
    /// </summary>
    public static class TaiChiLogginFactory
    {
        /// <summary>
        /// 创建一个带默认富化的 Serilog 配置对象。
        /// </summary>
        /// <param name="minimumLevel">最低日志级别。</param>
        /// <param name="enrichFromLogContext">是否启用 LogContext 富化。</param>
        /// <returns>Serilog 配置对象。</returns>
        public static Serilog.LoggerConfiguration CreateSerilogConfiguration(
            Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information,
            bool enrichFromLogContext = true)
        {
            var configuration = new Serilog.LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel);

            if (enrichFromLogContext)
            {
                configuration = configuration.Enrich.FromLogContext();
            }

            return configuration;
        }

        /// <summary>
        /// 使用回调配置创建 Serilog 日志记录器。
        /// </summary>
        /// <param name="configure">Serilog 配置回调。</param>
        /// <param name="minimumLevel">最低日志级别。</param>
        /// <returns>Serilog 日志记录器。</returns>
        /// <exception cref="ArgumentNullException">configure 为空时抛出。</exception>
        public static Serilog.ILogger CreateSerilogLogger(
            Action<Serilog.LoggerConfiguration> configure,
            Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var configuration = CreateSerilogConfiguration(minimumLevel, enrichFromLogContext: true);
            configure(configuration);
            return configuration.CreateLogger();
        }

        /// <summary>
        /// 使用 Serilog logger 创建 Microsoft <see cref="ILoggerFactory"/>。
        /// </summary>
        /// <param name="serilogLogger">Serilog 日志记录器。</param>
        /// <param name="dispose">释放 <see cref="ILoggerFactory"/> 时是否同时释放 Serilog logger。</param>
        /// <returns><see cref="ILoggerFactory"/> 实例。</returns>
        /// <exception cref="ArgumentNullException">serilogLogger 为空时抛出。</exception>
        public static ILoggerFactory CreateLoggerFactory(Serilog.ILogger serilogLogger, bool dispose = true)
        {
            if (serilogLogger == null)
            {
                throw new ArgumentNullException(nameof(serilogLogger));
            }

            return new SerilogLoggerFactory(serilogLogger, dispose);
        }

        /// <summary>
        /// 使用回调配置创建 Microsoft <see cref="ILoggerFactory"/>（底层为 Serilog）。
        /// </summary>
        /// <param name="configure">Serilog 配置回调。</param>
        /// <param name="minimumLevel">最低日志级别。</param>
        /// <param name="dispose">释放 <see cref="ILoggerFactory"/> 时是否同时释放 Serilog logger。</param>
        /// <returns><see cref="ILoggerFactory"/> 实例。</returns>
        /// <exception cref="ArgumentNullException">configure 为空时抛出。</exception>
        public static ILoggerFactory CreateLoggerFactory(
            Action<Serilog.LoggerConfiguration> configure,
            Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information,
            bool dispose = true)
        {
            var serilogLogger = CreateSerilogLogger(configure, minimumLevel);
            return CreateLoggerFactory(serilogLogger, dispose);
        }

        /// <summary>
        /// 将指定 Serilog logger 设置为全局 <see cref="Serilog.Log.Logger"/>。
        /// </summary>
        /// <param name="serilogLogger">Serilog 日志记录器。</param>
        /// <exception cref="ArgumentNullException">serilogLogger 为空时抛出。</exception>
        public static void SetGlobalLogger(Serilog.ILogger serilogLogger)
        {
            if (serilogLogger == null)
            {
                throw new ArgumentNullException(nameof(serilogLogger));
            }

            Serilog.Log.Logger = serilogLogger;
        }

        /// <summary>
        /// 关闭并刷新全局 Serilog logger。
        /// </summary>
        public static void CloseAndFlush()
        {
            Serilog.Log.CloseAndFlush();
        }
    }
}
