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
    /// 获取或设置是否启用 Lua 全局 <c>static</c> 表的未注册静态类自动解析。
    /// 默认启用；关闭后访问未显式注册的 <c>static.Xxx</c> 将直接返回 <c>nil</c>。
    /// </summary>
    public bool EnableStaticAutoRegister { get; set; } = true;

    /// <summary>
    /// 获取或设置是否在工厂函数（默认 <c>create</c>）中启用未注册类型的全程序集自动解析。
    /// 默认启用；关闭后只能创建通过 <see cref="LuaScriptHost.RegisterFactoryType(System.Type, string?)"/> 显式登记或可被 <see cref="System.Type.GetType(string)"/> 解析到的类型。
    /// </summary>
    public bool EnableFactoryAutoResolve { get; set; } = true;

    /// <summary>
    /// 获取或设置 Lua 全局工厂函数的名称，默认为 <c>create</c>。
    /// 设置为空白将抑制工厂函数注册。
    /// </summary>
    public string FactoryFunctionName { get; set; } = "create";

    /// <summary>
    /// 获取或设置 Lua 平台描述，可用于替换文件系统等底层行为。
    /// </summary>
    public LuaPlatform? Platform { get; set; }

    /// <summary>
    /// 获取或设置自定义模块加载器集合，需要自定义 require 行为时可以一次注入多个加载器。
    /// </summary>
    public IReadOnlyList<ILuaModuleLoader>? ModuleLoader { get; set; }
}
