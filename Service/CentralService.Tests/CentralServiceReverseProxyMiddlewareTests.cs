using System.Net;
using CentralService.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralService.Tests;

public sealed class CentralServiceReverseProxyMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SecretMissing_ShouldNotApplyForwardedHeaders()
    {
        var middleware = CreateMiddleware(
            settings: null,
            onNext: context =>
            {
                Assert.Equal(Uri.UriSchemeHttp, context.Request.Scheme);
                Assert.Equal(IPAddress.Parse("10.0.0.1"), context.Connection.RemoteIpAddress);
            });

        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttp;
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.10";
        context.Request.Headers["X-CentralService-Proxy-Secret"] = "secret";

        await middleware.InvokeAsync(context);
    }

    [Fact]
    public async Task InvokeAsync_SecretMismatch_ShouldNotApplyForwardedHeaders()
    {
        var middleware = CreateMiddleware(
            settings: new Dictionary<string, string?>
            {
                ["CentralServiceReverseProxy:Secret"] = "expected",
            },
            onNext: context =>
            {
                Assert.Equal(Uri.UriSchemeHttp, context.Request.Scheme);
                Assert.Equal(IPAddress.Parse("10.0.0.1"), context.Connection.RemoteIpAddress);
            });

        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttp;
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.10";
        context.Request.Headers["X-CentralService-Proxy-Secret"] = "wrong";

        await middleware.InvokeAsync(context);
    }

    [Fact]
    public async Task InvokeAsync_SecretMatches_ShouldApplyForwardedHeaders()
    {
        var middleware = CreateMiddleware(
            settings: new Dictionary<string, string?>
            {
                ["CentralServiceReverseProxy:Secret"] = "expected",
            },
            onNext: context =>
            {
                Assert.Equal(Uri.UriSchemeHttps, context.Request.Scheme);
                Assert.Equal(IPAddress.Parse("203.0.113.10"), context.Connection.RemoteIpAddress);
            });

        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttp;
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.10, 198.51.100.2";
        context.Request.Headers["X-CentralService-Proxy-Secret"] = "expected";

        await middleware.InvokeAsync(context);
    }

    private static CentralServiceReverseProxyMiddleware CreateMiddleware(
        Dictionary<string, string?>? settings,
        Action<HttpContext> onNext)
    {
        var builder = new ConfigurationBuilder();
        if (settings != null)
        {
            builder.AddInMemoryCollection(settings);
        }

        var configuration = builder.Build();

        return new CentralServiceReverseProxyMiddleware(
            next: context =>
            {
                onNext(context);
                return Task.CompletedTask;
            },
            configuration: configuration,
            logger: NullLogger<CentralServiceReverseProxyMiddleware>.Instance);
    }
}

