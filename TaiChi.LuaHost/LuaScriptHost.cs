using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lua;
using Lua.Loaders;
using Lua.Standard;
using System.Linq;

namespace TaiChi.LuaHost;

/// <summary>
/// 为 TaiChi 体系提供 LuaCSharp 支持的轻量脚本宿主。
/// </summary>
public sealed class LuaScriptHost : IDisposable
{
    private readonly LuaScriptHostOptions _options;
    private static readonly MethodInfo? CompositeModuleLoaderArrayFactory =
        typeof(CompositeModuleLoader).GetMethod("Create", new[] { typeof(ILuaModuleLoader[]) });
    private bool _disposed;

    /// <summary>
    /// 初始化宿主实例。
    /// </summary>
    /// <param name="options">可选的配置参数。</param>
    public LuaScriptHost(LuaScriptHostOptions? options = null)
    {
        _options = options ?? new LuaScriptHostOptions();
        State = CreateState(_options);
    }

    /// <summary>
    /// 获取底层的 <see cref="LuaState"/>。
    /// </summary>
    public LuaState State { get; }

    /// <summary>
    /// 获取构造时使用的配置。
    /// </summary>
    public LuaScriptHostOptions Options => _options;

    /// <summary>
    /// 异步执行 Lua 源代码。
    /// </summary>
    /// <param name="script">Lua 源码。</param>
    /// <param name="chunkName">可选的代码块名称。</param>
    /// <param name="cancellationToken">取消标记。</param>
    public async Task<LuaScriptExecutionResult> ExecuteAsync(string script, string? chunkName = null, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("脚本内容不能为空。", nameof(script));
        }

        try
        {
            var values = await State.DoStringAsync(script, chunkName ?? "chunk", cancellationToken).ConfigureAwait(false);
            return new LuaScriptExecutionResult(values);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LuaScriptHostException("执行 Lua 脚本失败。", ex);
        }
    }

    /// <summary>
    /// 异步执行指定路径的 Lua 文件。
    /// </summary>
    /// <param name="path">脚本文件相对或绝对路径。</param>
    /// <param name="cancellationToken">取消标记。</param>
    public async Task<LuaScriptExecutionResult> ExecuteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("脚本路径不能为空。", nameof(path));
        }

        var fullPath = ResolveScriptPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"找不到脚本文件：{fullPath}", fullPath);
        }

        try
        {
            var content = await File.ReadAllTextAsync(fullPath, ResolveEncoding(), cancellationToken).ConfigureAwait(false);
            return await ExecuteAsync(content, fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LuaScriptHostException($"读取脚本文件失败：{fullPath}", ex);
        }
    }

    /// <summary>
    /// 异步调用全局函数。
    /// </summary>
    /// <param name="functionName">Lua 全局函数名。</param>
    /// <param name="arguments">可选实参。</param>
    /// <param name="cancellationToken">取消标记。</param>
    public async Task<LuaScriptExecutionResult> InvokeAsync(string functionName, IReadOnlyList<LuaValue>? arguments = null, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        if (string.IsNullOrWhiteSpace(functionName))
        {
            throw new ArgumentException("函数名不能为空。", nameof(functionName));
        }

        var environment = State.Environment;
        if (!environment.TryGetValue(functionName, out var candidate) || candidate.Type is not LuaValueType.Function)
        {
            throw new LuaScriptHostException($"函数 {functionName} 未在 Lua 环境中注册。");
        }

        try
        {
            var args = NormalizeArguments(arguments);
            var values = await State.CallAsync(candidate, args, cancellationToken).ConfigureAwait(false);
            return new LuaScriptExecutionResult(values);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LuaScriptHostException($"调用 Lua 函数 {functionName} 失败。", ex);
        }
    }

    /// <summary>
    /// 将值注册到 Lua 全局环境。
    /// </summary>
    /// <param name="name">全局变量名。</param>
    /// <param name="value">值。</param>
    public void SetGlobal(string name, LuaValue value)
    {
        EnsureNotDisposed();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("全局变量名不能为空。", nameof(name));
        }

        State.Environment[name] = value;
    }

    /// <summary>
    /// 尝试从 Lua 全局环境读取指定变量。
    /// </summary>
    /// <param name="name">全局变量名。</param>
    /// <param name="value">若存在则输出值。</param>
    public bool TryGetGlobal(string name, out LuaValue value)
    {
        EnsureNotDisposed();

        if (string.IsNullOrWhiteSpace(name))
        {
            value = LuaValue.Nil;
            return false;
        }

        return State.Environment.TryGetValue(name, out value);
    }

    /// <summary>
    /// 以 C# 委托注册全局 Lua 函数。
    /// </summary>
    /// <param name="name">函数名称。</param>
    /// <param name="handler">处理逻辑。</param>
    public void RegisterFunction(string name, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> handler)
    {
        EnsureNotDisposed();

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var function = new LuaFunction(name, handler);
        SetGlobal(name, function);
    }

    /// <summary>
    /// 释放底层 Lua 资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        State.Dispose();
        if (_options.ModuleLoader is { Count: > 0 } moduleLoaders)
        {
            foreach (var loader in moduleLoaders)
            {
                if (loader is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 根据配置创建并初始化 LuaState。
    /// </summary>
    /// <param name="options">用户指定的宿主配置。</param>
    private static LuaState CreateState(LuaScriptHostOptions options)
    {
        var state = options.Platform is { } platform
            ? LuaState.Create(platform)
            : LuaState.Create();

        if (options.LoadStandardLibraries)
        {
            state.OpenStandardLibraries();
        }

        if (options.ModuleLoader is { Count: > 0 } moduleLoaders)
        {
            RegisterModuleLoaders(state, moduleLoaders);
        }

        return state;
    }

    /// <summary>
    /// 将可选的参数集合转换为数组，方便传入 Lua 调用。
    /// </summary>
    /// <param name="arguments">可能为空的参数列表。</param>
    private LuaValue[] NormalizeArguments(IReadOnlyList<LuaValue>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return Array.Empty<LuaValue>();
        }

        if (arguments is LuaValue[] array)
        {
            return array;
        }

        var buffer = new LuaValue[arguments.Count];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = arguments[i];
        }

        return buffer;
    }

    /// <summary>
    /// 基于配置的脚本根目录解析路径，支持相对与绝对路径。
    /// </summary>
    /// <param name="path">原始脚本路径。</param>
    private string ResolveScriptPath(string path)
    {
        var basePath = string.IsNullOrWhiteSpace(_options.ScriptRoot)
            ? AppContext.BaseDirectory
            : _options.ScriptRoot!;

        var normalized = Path.IsPathRooted(path)
            ? path
            : Path.Combine(basePath, path);

        return Path.GetFullPath(normalized);
    }

    /// <summary>
    /// 返回当前配置使用的脚本编码，默认 UTF-8。
    /// </summary>
    private Encoding ResolveEncoding()
    {
        return _options.ScriptEncoding ?? Encoding.UTF8;
    }

    /// <summary>
    /// 将配置中提供的模块加载器依次注册到 LuaState 中。
    /// </summary>
    /// <param name="state">目标 LuaState。</param>
    /// <param name="moduleLoaders">用户提供的模块加载器集合。</param>
    /// <exception cref="ArgumentException">当集合中存在空引用时抛出。</exception>
    private static void RegisterModuleLoaders(LuaState state, IReadOnlyList<ILuaModuleLoader> moduleLoaders)
    {
        if (moduleLoaders.Any(t => t is null))
        {
            throw new ArgumentException("模块加载器集合中存在空引用。", nameof(moduleLoaders));
        }

        state.ModuleLoader = CompositeModuleLoader.Create([.. moduleLoaders]);
    }

    /// <summary>
    /// 在执行外部操作前校验宿主是否已经释放，避免使用无效状态。
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LuaScriptHost));
        }
    }
}
