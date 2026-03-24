using System.Net;
using CentralService.Internal;
using Microsoft.AspNetCore.Http;

namespace CentralService.Tests;

public sealed class CentralServiceHttpsRedirectionPolicyTests
{
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

    [Fact]
    public void ShouldApply_HttpsRequest_ShouldReturnFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.10");

        var result = CentralServiceHttpsRedirectionPolicy.ShouldApply(context);

        Assert.False(result);
    }

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
