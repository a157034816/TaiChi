using System;
using System.IO;
using System.Text;

namespace TaiChi.LuaHost;

/// <summary>
/// 定义远程 Lua 模块加载器的配置。
/// </summary>
public sealed class RemoteLuaModuleLoaderOptions
{
    /// <summary>
    /// 获取或设置本地脚本根目录。
    /// </summary>
    public string LocalScriptRoot { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// 获取或设置远程缓存目录。
    /// </summary>
    public string CacheDirectory { get; set; } =
        Path.Combine(Path.GetTempPath(), "TaiChi", "LuaCache");

    /// <summary>
    /// 获取或设置远程 API 的基础地址。
    /// </summary>
    public string? RemoteBaseUrl { get; set; }

    /// <summary>
    /// 获取或设置脚本读取所使用的编码。
    /// </summary>
    public Encoding ScriptEncoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// 获取或设置 HTTP 请求的超时时间。
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 获取或设置要追加到所有远程请求 Header 的字符串 Key。
    /// </summary>
    public string? RemoteStringKey { get; set; }
}
