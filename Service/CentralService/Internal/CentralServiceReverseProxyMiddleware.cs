using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CentralService.Internal;

/// <summary>
/// 中心服务反向代理适配中间件。
/// <para>
/// 用于在反向代理（例如 Caddy / Nginx）转发到 HTTP 上游时，安全地恢复原始请求的 Scheme/客户端 IP。
/// </para>
/// <para>
/// 安全策略：仅当请求携带正确的“代理密钥”头时，才会采信 <c>X-Forwarded-Proto</c> / <c>X-Forwarded-For</c>。
/// </para>
/// </summary>
internal sealed class CentralServiceReverseProxyMiddleware
{
    private const string DefaultSecretHeaderName = "X-CentralService-Proxy-Secret";
    private const string ForwardedProtoHeaderName = "X-Forwarded-Proto";
    private const string ForwardedForHeaderName = "X-Forwarded-For";

    private readonly RequestDelegate _next;
    private readonly ILogger<CentralServiceReverseProxyMiddleware> _logger;
    private readonly string? _secret;
    private readonly string _secretHeaderName;

    /// <summary>
    /// 创建中间件实例。
    /// </summary>
    /// <param name="next">下一个中间件。</param>
    /// <param name="configuration">配置（用于读取代理密钥）。</param>
    /// <param name="logger">日志记录器。</param>
    public CentralServiceReverseProxyMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<CentralServiceReverseProxyMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _logger = logger;

        var secret = configuration.GetValue<string?>("CentralServiceReverseProxy:Secret")?.Trim();
        _secret = string.IsNullOrWhiteSpace(secret) ? null : secret;

        var headerName = configuration.GetValue<string?>("CentralServiceReverseProxy:SecretHeaderName")?.Trim();
        _secretHeaderName = string.IsNullOrWhiteSpace(headerName) ? DefaultSecretHeaderName : headerName;
    }

    /// <summary>
    /// 处理 HTTP 请求。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        TryApplyForwardedHeaders(context);
        return _next(context);
    }

    private void TryApplyForwardedHeaders(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_secret))
        {
            return;
        }

        if (!context.Request.Headers.TryGetValue(_secretHeaderName, out var providedSecrets))
        {
            return;
        }

        var matched = providedSecrets.Any(value =>
            string.Equals(value?.Trim(), _secret, StringComparison.Ordinal));

        if (!matched)
        {
            return;
        }

        // 仅当携带正确代理密钥时，才采信 X-Forwarded-*，避免公网直连伪造头导致 Scheme/IP 被篡改。
        if (context.Request.Headers.TryGetValue(ForwardedProtoHeaderName, out var forwardedProto))
        {
            var protoValue = forwardedProto.ToString().Trim();
            if (string.Equals(protoValue, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Scheme = Uri.UriSchemeHttps;
            }
            else if (string.Equals(protoValue, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Scheme = Uri.UriSchemeHttp;
            }
            else if (!string.IsNullOrWhiteSpace(protoValue))
            {
                _logger.LogDebug("忽略未知的 {Header} 值: {Value}", ForwardedProtoHeaderName, protoValue);
            }
        }

        if (context.Request.Headers.TryGetValue(ForwardedForHeaderName, out var forwardedFor))
        {
            var first = forwardedFor.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(first))
            {
                return;
            }

            if (IPAddress.TryParse(first, out var ip))
            {
                context.Connection.RemoteIpAddress = ip;
                return;
            }

            _logger.LogDebug("解析 {Header} 失败: {Value}", ForwardedForHeaderName, first);
        }
    }
}

