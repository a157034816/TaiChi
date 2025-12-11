using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lua;

namespace TaiChi.LuaHost;

/// <summary>
/// 支持本地优先、远程回退并带缓存的 Lua 模块加载器。
/// </summary>
public sealed class RemoteLuaModuleLoader : ILuaModuleLoader, IDisposable
{
    private const string RemoteStringKeyHeaderName = "X-Lua-StringKey";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _localRoot;
    private readonly string _cacheRoot;
    private readonly Encoding _encoding;
    private readonly Uri? _remoteBaseUri;
    private readonly HttpClient? _httpClient;
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="RemoteLuaModuleLoader"/> 的新实例。
    /// </summary>
    public RemoteLuaModuleLoader(RemoteLuaModuleLoaderOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _localRoot = EnsureDirectory(options.LocalScriptRoot);
        _cacheRoot = EnsureDirectory(options.CacheDirectory);
        _encoding = options.ScriptEncoding ?? Encoding.UTF8;

        if (!string.IsNullOrWhiteSpace(options.RemoteBaseUrl))
        {
            _remoteBaseUri = new Uri(AppendSlash(options.RemoteBaseUrl), UriKind.Absolute);
            _httpClient = new HttpClient
            {
                BaseAddress = _remoteBaseUri,
                Timeout = options.HttpTimeout <= TimeSpan.Zero
                    ? TimeSpan.FromSeconds(30)
                    : options.HttpTimeout
            };

            if (!string.IsNullOrWhiteSpace(options.RemoteStringKey))
            {
                var stringKey = options.RemoteStringKey.Trim();
                _httpClient.DefaultRequestHeaders.Remove(RemoteStringKeyHeaderName);
                _httpClient.DefaultRequestHeaders.Add(RemoteStringKeyHeaderName, stringKey);
            }
        }
    }

    /// <inheritdoc />
    public bool Exists(string moduleName)
    {
        EnsureNotDisposed();
        var relativePath = NormalizeModulePath(moduleName);
        if (TryGetLocalPath(relativePath, out _))
        {
            return true;
        }

        if (_httpClient == null)
        {
            return TryGetCachedPath(relativePath, out _);
        }

        if (TryGetCachedPath(relativePath, out _))
        {
            return true;
        }

        var metadata = GetRemoteMetadataAsync(relativePath, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        return metadata != null;
    }

    /// <inheritdoc />
    public ValueTask<LuaModule> LoadAsync(string moduleName, CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        var relativePath = NormalizeModulePath(moduleName);

        if (TryGetLocalPath(relativePath, out var localPath))
        {
            return new ValueTask<LuaModule>(CreateModuleFromFileAsync(moduleName, localPath, cancellationToken));
        }

        return new ValueTask<LuaModule>(LoadRemoteAsync(moduleName, relativePath, cancellationToken));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// 从远程服务加载脚本并缓存。
    /// </summary>
    private async Task<LuaModule> LoadRemoteAsync(string moduleName, string relativePath, CancellationToken cancellationToken)
    {
        if (_httpClient == null)
        {
            throw new FileNotFoundException($"未找到 Lua 模块：{relativePath}", relativePath);
        }

        var metadata = await GetRemoteMetadataAsync(relativePath, cancellationToken).ConfigureAwait(false);
        if (metadata == null)
        {
            throw new FileNotFoundException($"远程服务器不存在模块：{relativePath}", relativePath);
        }

        var cachePath = GetCachePath(relativePath);
        if (File.Exists(cachePath))
        {
            var cachedHash = await Md5Helper.ComputeFileHashAsync(cachePath, cancellationToken).ConfigureAwait(false);
            if (cachedHash.Equals(metadata.Md5, StringComparison.OrdinalIgnoreCase))
            {
                return await CreateModuleFromFileAsync(moduleName, cachePath, cancellationToken).ConfigureAwait(false);
            }
        }

        await DownloadRemoteAsync(relativePath, cachePath, cancellationToken).ConfigureAwait(false);
        return await CreateModuleFromFileAsync(moduleName, cachePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 读取磁盘内容构建 Lua 模块。
    /// </summary>
    private async Task<LuaModule> CreateModuleFromFileAsync(string moduleName, string fullPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, _encoding, true);
        var buffer = new char[4096];
        var builder = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);
        }

        var content = builder.ToString();
        return new LuaModule(moduleName, content);
    }

    /// <summary>
    /// 将远程脚本下载到本地缓存。
    /// </summary>
    private async Task DownloadRemoteAsync(string relativePath, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var requestUri = $"api/scripts/content?path={Uri.EscapeDataString(relativePath)}";
        using var response = await _httpClient!.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 请求远程脚本的元数据信息。
    /// </summary>
    private async Task<RemoteScriptMetadata?> GetRemoteMetadataAsync(string relativePath, CancellationToken cancellationToken)
    {
        var requestUri = $"api/scripts/metadata?path={Uri.EscapeDataString(relativePath)}";
        using var response = await _httpClient!.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<RemoteScriptMetadata>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 尝试解析本地脚本路径。
    /// </summary>
    private bool TryGetLocalPath(string relativePath, out string? fullPath)
    {
        var combined = Path.Combine(_localRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        fullPath = Path.GetFullPath(combined);
        if (!fullPath.StartsWith(_localRoot, StringComparison.OrdinalIgnoreCase))
        {
            fullPath = null;
            return false;
        }

        if (!File.Exists(fullPath))
        {
            fullPath = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 尝试解析缓存脚本路径。
    /// </summary>
    private bool TryGetCachedPath(string relativePath, out string? fullPath)
    {
        var combined = GetCachePath(relativePath);
        if (!File.Exists(combined))
        {
            fullPath = null;
            return false;
        }

        fullPath = combined;
        return true;
    }

    /// <summary>
    /// 根据模块路径计算缓存文件路径。
    /// </summary>
    private string GetCachePath(string relativePath)
    {
        var combined = Path.Combine(_cacheRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var fullPath = Path.GetFullPath(combined);
        if (!fullPath.StartsWith(_cacheRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("缓存路径越界。");
        }

        return fullPath;
    }

    /// <summary>
    /// 规范化模块名称为相对路径。
    /// </summary>
    private static string NormalizeModulePath(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("模块名称不能为空。", nameof(moduleName));
        }

        var normalized = moduleName.Trim()
            .Replace('\\', '/')
            .Replace("./", "/");

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('/');
        if (normalized.Length == 0)
        {
            throw new ArgumentException("模块名称无效。", nameof(moduleName));
        }

        if (!HasExplicitExtension(normalized))
        {
            normalized += ".lua";
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                throw new InvalidOperationException("模块名称包含非法上级目录引用。");
            }

            result.Add(segment);
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("模块名称无效。");
        }

        return string.Join('/', result);
    }

    /// <summary>
    /// 判断路径的最终段是否显式携带扩展名。
    /// </summary>
    private static bool HasExplicitExtension(string path)
    {
        var lastSeparator = path.LastIndexOf('/');
        var fileName = lastSeparator >= 0 ? path[(lastSeparator + 1)..] : path;
        return fileName.Contains('.', StringComparison.Ordinal);
    }

    /// <summary>
    /// 确保目录存在并返回其绝对路径。
    /// </summary>
    private static string EnsureDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = AppContext.BaseDirectory;
        }

        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        return fullPath;
    }

    /// <summary>
    /// 保证 url 以斜杠结尾。
    /// </summary>
    private static string AppendSlash(string url)
    {
        return url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
    }

    /// <summary>
    /// 确认对象尚未被释放。
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RemoteLuaModuleLoader));
        }
    }

    /// <summary>
    /// 远程 API 返回的脚本元数据。
    /// </summary>
    private sealed class RemoteScriptMetadata
    {
        public string RelativePath { get; set; } = string.Empty;

        public string Md5 { get; set; } = string.Empty;

        public long Length { get; set; }
    }
}

internal static class Md5Helper
{
    /// <summary>
    /// 计算文件的 MD5 哈希。
    /// </summary>
    public static async Task<string> ComputeFileHashAsync(string fullPath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var md5 = System.Security.Cryptography.MD5.Create();
        var buffer = new byte[8192];
        md5.Initialize();
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            md5.TransformBlock(buffer, 0, read, null, 0);
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return ToHex(md5.Hash!);
    }

    /// <summary>
    /// 转换 md5 字节为小写十六进制字符串。
    /// </summary>
    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length * 2];
        const string hex = "0123456789abcdef";
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = hex[bytes[i] >> 4];
            chars[i * 2 + 1] = hex[bytes[i] & 0xF];
        }

        return new string(chars);
    }
}
