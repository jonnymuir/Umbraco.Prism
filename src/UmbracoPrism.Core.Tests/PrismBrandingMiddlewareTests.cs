using FluentAssertions;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Middleware;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Tests;

public class PrismBrandingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_IncludesDesktopThenMobileOverrides_WhenPrismMobileUserAgent()
    {
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#0055ff",
                    ["--prism-radius"] = "10px"
                },
                MobileBrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#003399",
                    ["--prism-page-gutter"] = "12px"
                }
            }
        };

        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.UserAgent = "Mozilla/5.0 PrismMobile";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, prismContext);

        var html = await ReadResponseBodyAsync(context.Response);
        html.Should().Contain("<style id=\"prism-branding-overrides\">:root{");

        var styleStart = html.IndexOf(":root{", StringComparison.Ordinal);
        var desktopPrimaryIndex = html.IndexOf("--prism-primary:#0055ff;", StringComparison.Ordinal);
        var mobilePrimaryIndex = html.IndexOf("--prism-primary:#003399;", StringComparison.Ordinal);

        styleStart.Should().BeGreaterThan(-1);
        desktopPrimaryIndex.Should().BeGreaterThan(styleStart);
        mobilePrimaryIndex.Should().BeGreaterThan(desktopPrimaryIndex);
        html.Should().Contain("--prism-page-gutter:12px;");
    }

    [Fact]
    public async Task InvokeAsync_ExcludesMobileOverrides_WhenUserAgentIsNotPrismMobile()
    {
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#0055ff"
                },
                MobileBrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#003399"
                }
            }
        };

        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, prismContext);

        var html = await ReadResponseBodyAsync(context.Response);
        html.Should().Contain("--prism-primary:#0055ff;");
        html.Should().NotContain("--prism-primary:#003399;");
    }

    [Fact]
    public async Task InvokeAsync_IncludesMobileOverrides_WhenPrismMobileCookieIsSet()
    {
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#0055ff"
                },
                MobileBrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#003399"
                }
            }
        };

        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.Request.Headers.Cookie = "prism.mobile=1";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, prismContext);

        var html = await ReadResponseBodyAsync(context.Response);
        html.Should().Contain("--prism-primary:#003399;");
    }

    [Fact]
    public async Task InvokeAsync_IncludesMobileOverrides_WhenPrismMobileQueryFlagIsSet()
    {
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#0055ff"
                },
                MobileBrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#003399"
                }
            }
        };

        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.Request.QueryString = new QueryString("?prismMobile=1");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, prismContext);

        var html = await ReadResponseBodyAsync(context.Response);
        html.Should().Contain("--prism-primary:#003399;");
        context.Response.Headers.SetCookie.ToString().Should().Contain("prism.mobile=1");
    }

    [Fact]
    public async Task InvokeAsync_QueryFlagOff_ClearsPrismMobileCookieAndExcludesMobileOverrides()
    {
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#0055ff"
                },
                MobileBrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#003399"
                }
            }
        };

        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.Request.Headers.Cookie = "prism.mobile=1";
        context.Request.QueryString = new QueryString("?prismMobile=0");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, prismContext);

        var html = await ReadResponseBodyAsync(context.Response);
        html.Should().Contain("--prism-primary:#0055ff;");
        html.Should().NotContain("--prism-primary:#003399;");
        context.Response.Headers.SetCookie.ToString().Should().Contain("prism.mobile=");
    }

    [Fact]
    public async Task InvokeAsync_IncludesMobileOverrides_WhenPlatformHeaderIsMobile()
    {
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#0055ff"
                },
                MobileBrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#003399"
                }
            }
        };

        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.Request.Headers["X-Prism-Platform"] = "mobile";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, prismContext);

        var html = await ReadResponseBodyAsync(context.Response);
        html.Should().Contain("--prism-primary:#003399;");
    }

    [Fact]
    public async Task InvokeAsync_IncludesMobileShellGuards_WhenPrismMobileRequestWithoutOverrides()
    {
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant()
        };

        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString("?prismMobile=1");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, prismContext);

        var html = await ReadResponseBodyAsync(context.Response);
        html.Should().Contain("id=\"prism-mobile-shell-base\"");
        html.Should().Contain("id=\"prism-mobile-shell-guard\"");
        html.Should().Contain("classList.add('prism-mobile')");
        html.Should().Contain("viewport-fit=cover");
    }

    private static PrismBrandingMiddleware CreateMiddlewareWithHtmlResponse(string html)
    {
        return new PrismBrandingMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        });
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var result = await reader.ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);
        return result;
    }

    private sealed class TestPrismContext : IPrismContext
    {
        public PrismTenant? CurrentTenant { get; set; }

        public Task<System.Net.Http.Headers.AuthenticationHeaderValue?> GetAuthorizationHeaderAsync()
        {
            return Task.FromResult<System.Net.Http.Headers.AuthenticationHeaderValue?>(null);
        }
    }
}
