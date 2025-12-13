using System;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;

namespace TaiChi.Loggin
{
    /// <summary>
    /// <see cref="ILoggingBuilder"/> 扩展方法。
    /// </summary>
    public static class LoggingBuilderExtensions
    {
        /// <summary>
        /// 将日志（Serilog Provider）添加到 <see cref="ILoggingBuilder"/>。
        /// </summary>
        /// <param name="builder">日志构建器。</param>
        /// <param name="serilogLogger">Serilog 日志记录器。</param>
        /// <param name="dispose">释放 Provider 时是否同时释放 Serilog logger。</param>
        /// <returns>日志构建器。</returns>
        /// <exception cref="ArgumentNullException">builder 或 serilogLogger 为空时抛出。</exception>
        public static ILoggingBuilder AddLoggin(
            this ILoggingBuilder builder,
            Serilog.ILogger serilogLogger,
            bool dispose = true)
        {
            return builder.AddSerilogProvider(serilogLogger, dispose);
        }

        /// <summary>
        /// 通过回调配置创建 Serilog logger，并将日志（Serilog Provider）添加到 <see cref="ILoggingBuilder"/>。
        /// </summary>
        /// <param name="builder">日志构建器。</param>
        /// <param name="configure">Serilog 配置回调。</param>
        /// <param name="minimumLevel">最低日志级别。</param>
        /// <param name="dispose">释放 Provider 时是否同时释放 Serilog logger。</param>
        /// <returns>日志构建器。</returns>
        /// <exception cref="ArgumentNullException">builder 或 configure 为空时抛出。</exception>
        public static ILoggingBuilder AddLoggin(
            this ILoggingBuilder builder,
            Action<Serilog.LoggerConfiguration> configure,
            Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information,
            bool dispose = true)
        {
            return builder.AddSerilogProvider(configure, minimumLevel, dispose);
        }

        /// <summary>
        /// 将 Serilog 作为日志 Provider 添加到 <see cref="ILoggingBuilder"/>。
        /// </summary>
        /// <param name="builder">日志构建器。</param>
        /// <param name="serilogLogger">Serilog 日志记录器。</param>
        /// <param name="dispose">释放 Provider 时是否同时释放 Serilog logger。</param>
        /// <returns>日志构建器。</returns>
        /// <exception cref="ArgumentNullException">builder 或 serilogLogger 为空时抛出。</exception>
        public static ILoggingBuilder AddSerilogProvider(
            this ILoggingBuilder builder,
            Serilog.ILogger serilogLogger,
            bool dispose = true)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (serilogLogger == null)
            {
                throw new ArgumentNullException(nameof(serilogLogger));
            }

            builder.AddProvider(new SerilogLoggerProvider(serilogLogger, dispose));
            return builder;
        }

        /// <summary>
        /// 使用回调配置创建 Serilog logger，并将其作为日志 Provider 添加到 <see cref="ILoggingBuilder"/>。
        /// </summary>
        /// <param name="builder">日志构建器。</param>
        /// <param name="configure">Serilog 配置回调。</param>
        /// <param name="minimumLevel">最低日志级别。</param>
        /// <param name="dispose">释放 Provider 时是否同时释放 Serilog logger。</param>
        /// <returns>日志构建器。</returns>
        /// <exception cref="ArgumentNullException">builder 或 configure 为空时抛出。</exception>
        public static ILoggingBuilder AddSerilogProvider(
            this ILoggingBuilder builder,
            Action<Serilog.LoggerConfiguration> configure,
            Serilog.Events.LogEventLevel minimumLevel = Serilog.Events.LogEventLevel.Information,
            bool dispose = true)
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
            return builder.AddSerilogProvider(serilogLogger, dispose);
        }
    }
}
