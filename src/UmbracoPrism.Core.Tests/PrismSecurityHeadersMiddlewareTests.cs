using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UmbracoPrism.Core.Configuration;
using UmbracoPrism.Core.Middleware;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Regression tests for SEC-PT2-004 — security response headers middleware.
/// </summary>
public class PrismSecurityHeadersMiddlewareTests
{
    private static PrismSecurityHeadersMiddleware BuildMiddleware(
        PrismSecurityHeadersOptions? options = null)
    {
        var opts = Options.Create(options ?? new PrismSecurityHeadersOptions());
        return new PrismSecurityHeadersMiddleware(_ => Task.CompletedTask, opts);
    }

    private static DefaultHttpContext BuildHttpsContext(string path = "/")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Request.IsHttps = true;
        return ctx;
    }

    private static DefaultHttpContext BuildHttpContext(string path = "/")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Request.IsHttps = false;
        return ctx;
    }

    [Fact]
    public async Task SecurityHeaders_AreApplied_OnDefaultHttpsRequest()
    {
        var middleware = BuildMiddleware();
        var ctx = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);

        ctx.Response.Headers.Should().ContainKey("X-Content-Type-Options");
        ctx.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        ctx.Response.Headers.Should().ContainKey("X-Frame-Options");
        ctx.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
        ctx.Response.Headers.Should().ContainKey("Referrer-Policy");
        ctx.Response.Headers.Should().ContainKey("Permissions-Policy");
        ctx.Response.Headers.Should().ContainKey("Strict-Transport-Security");
        ctx.Response.Headers.Should().ContainKey("Content-Security-Policy-Report-Only");
    }

    [Fact]
    public async Task HstsHeader_IsOmitted_OnHttpRequest()
    {
        var middleware = BuildMiddleware();
        var ctx = BuildHttpContext("/dashboard");

        await middleware.InvokeAsync(ctx);

        ctx.Response.Headers.Should().NotContainKey("Strict-Transport-Security",
            "HSTS must only be set on HTTPS responses");
    }

    [Fact]
    public async Task SecurityHeaders_AreSkipped_ForBackofficeRoutes()
    {
        var middleware = BuildMiddleware();
        var ctx = BuildHttpsContext("/umbraco/backoffice/api/something");

        await middleware.InvokeAsync(ctx);

        ctx.Response.Headers.Should().NotContainKey("X-Content-Type-Options",
            "backoffice routes are excluded from Prism security headers by default");
        ctx.Response.Headers.Should().NotContainKey("X-Frame-Options");
    }

    [Fact]
    public async Task SecurityHeaders_AreApplied_ForBackofficeRoutes_WhenExcludeBackofficeIsFalse()
    {
        var options = new PrismSecurityHeadersOptions { ExcludeBackoffice = false };
        var middleware = BuildMiddleware(options);
        var ctx = BuildHttpsContext("/umbraco/backoffice/api/something");

        await middleware.InvokeAsync(ctx);

        ctx.Response.Headers.Should().ContainKey("X-Content-Type-Options");
    }

    [Fact]
    public async Task SecurityHeaders_AreNotApplied_WhenMiddlewareIsDisabled()
    {
        var options = new PrismSecurityHeadersOptions { Enabled = false };
        var middleware = BuildMiddleware(options);
        var ctx = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);

        ctx.Response.Headers.Should().NotContainKey("X-Content-Type-Options",
            "middleware must be fully disabled when Enabled=false");
    }

    [Fact]
    public async Task ContentSecurityPolicy_IsReportOnlyByDefault()
    {
        var middleware = BuildMiddleware();
        var ctx = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);

        ctx.Response.Headers.Should().ContainKey("Content-Security-Policy-Report-Only",
            "CSP ships as Report-Only by default (SEC-PT2-004 follow-up: promote to enforced once tuned)");
        ctx.Response.Headers.Should().NotContainKey("Content-Security-Policy",
            "enforced CSP is not set by default — must be explicitly configured after tuning");
    }

    [Fact]
    public async Task HstsHeader_HasCorrectValue()
    {
        var middleware = BuildMiddleware();
        var ctx = BuildHttpsContext("/dashboard");

        await middleware.InvokeAsync(ctx);

        ctx.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=31536000; includeSubDomains");
    }
}
