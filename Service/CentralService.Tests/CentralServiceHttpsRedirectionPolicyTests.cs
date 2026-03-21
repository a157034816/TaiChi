using System.Net;
using CentralService.Internal;
using Microsoft.AspNetCore.Http;

namespace CentralService.Tests;

/// <summary>
/// <see cref="CentralServiceHttpsRedirectionPolicy"/> 的单元测试：验证 HTTPS 重定向策略的适用范围判断。
/// </summary>
public sealed class CentralServiceHttpsRedirectionPolicyTests
{
    /// <summary>
    /// 验证：当请求来自回环地址（127.0.0.1/::1）且为 HTTP 时，不应启用重定向。
    /// 该行为用于支持本机健康检查/本地开发访问。
    /// </summary>
    /// <param name="remoteIp">远端 IP（回环地址）。</param>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void ShouldApply_LoopbackHttpRequest_ShouldReturnFalse(string remoteIp)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttp;
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);

        var result = CentralServiceHttpsRedirectionPolicy.ShouldApply(context);

        Assert.False(result);
    }

    /// <summary>
    /// 验证：当请求本身已是 HTTPS 时，不应再次触发重定向。
    /// </summary>
    [Fact]
    public void ShouldApply_HttpsRequest_ShouldReturnFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.10");

        var result = CentralServiceHttpsRedirectionPolicy.ShouldApply(context);

        Assert.False(result);
    }

    /// <summary>
    /// 验证：当请求来自非回环地址且为 HTTP 时，应启用重定向（提升默认安全性）。
    /// </summary>
    [Fact]
    public void ShouldApply_RemoteHttpRequest_ShouldReturnTrue()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttp;
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.10");

        var result = CentralServiceHttpsRedirectionPolicy.ShouldApply(context);

        Assert.True(result);
    }
}
