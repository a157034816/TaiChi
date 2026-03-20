using System.Net;
using Microsoft.AspNetCore.Http;

namespace CentralService.Internal;

internal static class CentralServiceHttpsRedirectionPolicy
{
    /// <summary>
    /// 判断当前请求是否应该执行 HTTPS 重定向。
    /// </summary>
    public static bool ShouldApply(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.IsHttps)
        {
            return false;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null && IPAddress.IsLoopback(remoteIp))
        {
            return false;
        }

        return true;
    }
}
