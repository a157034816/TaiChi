using System;
using System.Collections.Generic;
using System.Text;
using Lua;
using Lua.Platforms;

namespace TaiChi.LuaHost;

/// <summary>
/// 表示构建 <see cref="LuaScriptHost"/> 所需的可选配置。
/// </summary>
public sealed class LuaScriptHostOptions
{
    /// <summary>
    /// 获取或设置脚本的根目录，默认为应用程序根目录。
    /// </summary>
    public string ScriptRoot { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// 获取或设置读取脚本文件时使用的编码，默认为 UTF-8。
    /// </summary>
    public Encoding ScriptEncoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// 获取或设置在初始化 <see cref="LuaScriptHost"/> 时是否加载 Lua 标准库。
    /// </summary>
    public bool LoadStandardLibraries { get; set; } = true;

    /// <summary>
    /// 获取或设置 Lua 平台描述，可用于替换文件系统等底层行为。
    /// </summary>
    public LuaPlatform? Platform { get; set; }

    /// <summary>
    /// 获取或设置自定义模块加载器集合，需要自定义 require 行为时可以一次注入多个加载器。
    /// </summary>
    public IReadOnlyList<ILuaModuleLoader>? ModuleLoader { get; set; }
}
