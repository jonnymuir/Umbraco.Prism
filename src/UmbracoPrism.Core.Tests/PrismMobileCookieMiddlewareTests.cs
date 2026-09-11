using FluentAssertions;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Middleware;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Regression tests for the (renamed, and now much smaller) middleware left after
/// SEC-PT2-004's CSP follow-up moved branding/mobile-shell/biometric content out of inline
/// HTML injection and into externally-referenced resources (see
/// <c>PrismBrandingAssetsController</c> and <c>wwwroot/mobile-shell/</c>). All that remains is
/// the <c>?prismMobile=</c> query-flag-to-cookie persistence — everything else the old
/// <c>PrismBrandingMiddlewareTests</c> covered (branding CSS ordering, mobile-shell-guard
/// markup, biometric script injection) now belongs to whatever renders/references those
/// resources, not to this middleware.
/// </summary>
public class PrismMobileCookieMiddlewareTests
{
    private static PrismMobileCookieMiddleware BuildMiddleware() =>
        new(_ => Task.CompletedTask);

    [Fact]
    public async Task SetsMobileCookie_WhenQueryFlagIsOn()
    {
        var middleware = BuildMiddleware();
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?prismMobile=1");
        context.Request.IsHttps = true;

        await middleware.InvokeAsync(context);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain($"{PrismMobileRequestDetection.CookieName}=1");
        setCookie.Should().Contain("secure", "the request was HTTPS");
    }

    [Fact]
    public async Task ClearsMobileCookie_WhenQueryFlagIsOff()
    {
        var middleware = BuildMiddleware();
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?prismMobile=0");

        await middleware.InvokeAsync(context);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain($"{PrismMobileRequestDetection.CookieName}=");
        setCookie.Should().Contain("expires=Thu, 01 Jan 1970", "clearing a cookie sets it to already expired");
    }

    [Fact]
    public async Task DoesNothing_WhenNoQueryFlagIsPresent()
    {
        var middleware = BuildMiddleware();
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task AlwaysCallsNext()
    {
        var called = false;
        var middleware = new PrismMobileCookieMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        called.Should().BeTrue("this middleware never short-circuits the pipeline");
    }
}
