namespace TaiChi.Cache
{
    /// <summary>
    /// TaiChi.Cache 诊断日志级别。
    /// </summary>
    public enum CacheLogLevel
    {
        /// <summary>
        /// 调试级别（通常只在显式启用诊断时输出）。
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 信息级别（通常只在显式启用诊断时输出）。
        /// </summary>
        Info = 1,

        /// <summary>
        /// 警告级别（默认应输出）。
        /// </summary>
        Warn = 2,

        /// <summary>
        /// 错误级别（默认应输出）。
        /// </summary>
        Error = 3,
    }
}
