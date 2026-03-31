using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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
        // Ensure RequestServices is available for AuthenticateAsync used in middleware
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        // Provide a mocked IAuthenticationService to satisfy context.AuthenticateAsync calls in middleware tests
        var authMock = new Moq.Mock<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        // Default AuthenticateAsync behavior: return AuthenticateResult.NoResult() wrapped in Task
        authMock.Setup(s => s.AuthenticateAsync(Moq.It.IsAny<HttpContext>(), Moq.It.IsAny<string>()))
                .ReturnsAsync(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        services.AddSingleton(authMock.Object);
        context.RequestServices = services.BuildServiceProvider();
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
        // Ensure RequestServices available for AuthenticateAsync
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var authMock = new Moq.Mock<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        authMock.Setup(s => s.AuthenticateAsync(Moq.It.IsAny<HttpContext>(), Moq.It.IsAny<string>()))
                .ReturnsAsync(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        services.AddSingleton(authMock.Object);
        context.RequestServices = services.BuildServiceProvider();
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
        // Ensure RequestServices available for AuthenticateAsync
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var authMock = new Moq.Mock<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        authMock.Setup(s => s.AuthenticateAsync(Moq.It.IsAny<HttpContext>(), Moq.It.IsAny<string>()))
                .ReturnsAsync(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        services.AddSingleton(authMock.Object);
        context.RequestServices = services.BuildServiceProvider();
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
        // Ensure RequestServices available for AuthenticateAsync
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var authMock = new Moq.Mock<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        authMock.Setup(s => s.AuthenticateAsync(Moq.It.IsAny<HttpContext>(), Moq.It.IsAny<string>()))
                .ReturnsAsync(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        services.AddSingleton(authMock.Object);
        context.RequestServices = services.BuildServiceProvider();
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
        // Ensure RequestServices available for AuthenticateAsync
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var authMock = new Moq.Mock<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        authMock.Setup(s => s.AuthenticateAsync(Moq.It.IsAny<HttpContext>(), Moq.It.IsAny<string>()))
                .ReturnsAsync(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        services.AddSingleton(authMock.Object);
        context.RequestServices = services.BuildServiceProvider();
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
        // Ensure RequestServices available for AuthenticateAsync
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var authMock = new Moq.Mock<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        authMock.Setup(s => s.AuthenticateAsync(Moq.It.IsAny<HttpContext>(), Moq.It.IsAny<string>()))
                .ReturnsAsync(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        services.AddSingleton(authMock.Object);
        context.RequestServices = services.BuildServiceProvider();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, prismContext);

        var html = await ReadResponseBodyAsync(context.Response);
        html.Should().Contain("id=\"prism-mobile-shell-base\"");
        html.Should().Contain("id=\"prism-mobile-shell-guard\"");
        html.Should().Contain("classList.add('prism-mobile')");
        html.Should().Contain("viewport-fit=cover");
    }

    [Fact]
    public async Task InvokeAsync_DoesNotLeakBrandingBetweenTenants_OnSequentialRequests()
    {
        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");

        var tenantAContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#aa0000"
                }
            }
        };

        var tenantBContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#00aa00"
                }
            }
        };

        var requestA = CreateGetHtmlContext();
        await middleware.InvokeAsync(requestA, tenantAContext);
        var htmlA = await ReadResponseBodyAsync(requestA.Response);

        var requestB = CreateGetHtmlContext();
        await middleware.InvokeAsync(requestB, tenantBContext);
        var htmlB = await ReadResponseBodyAsync(requestB.Response);

        htmlA.Should().Contain("--prism-primary:#aa0000;");
        htmlA.Should().NotContain("--prism-primary:#00aa00;");

        htmlB.Should().Contain("--prism-primary:#00aa00;");
        htmlB.Should().NotContain("--prism-primary:#aa0000;");
    }

    [Fact]
    public async Task InvokeAsync_UsesUpdatedBrandingOverrides_ForSameTenantOnLaterRequest()
    {
        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#111111"
                }
            }
        };

        var firstRequest = CreateGetHtmlContext();
        await middleware.InvokeAsync(firstRequest, prismContext);
        var firstHtml = await ReadResponseBodyAsync(firstRequest.Response);

        prismContext.CurrentTenant!.BrandingOverrides["--prism-primary"] = "#222222";

        var secondRequest = CreateGetHtmlContext();
        await middleware.InvokeAsync(secondRequest, prismContext);
        var secondHtml = await ReadResponseBodyAsync(secondRequest.Response);

        firstHtml.Should().Contain("--prism-primary:#111111;");
        secondHtml.Should().Contain("--prism-primary:#222222;");
        secondHtml.Should().NotContain("--prism-primary:#111111;");
    }

    [Fact]
    public async Task InvokeAsync_UsesUpdatedMobileOverrides_ForSameTenantOnLaterMobileRequest()
    {
        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#111111"
                },
                MobileBrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#333333"
                }
            }
        };

        var firstMobileRequest = CreateGetHtmlContext();
        firstMobileRequest.Request.QueryString = new QueryString("?prismMobile=1");
        await middleware.InvokeAsync(firstMobileRequest, prismContext);
        var firstHtml = await ReadResponseBodyAsync(firstMobileRequest.Response);

        prismContext.CurrentTenant!.MobileBrandingOverrides["--prism-primary"] = "#444444";

        var secondMobileRequest = CreateGetHtmlContext();
        secondMobileRequest.Request.QueryString = new QueryString("?prismMobile=1");
        await middleware.InvokeAsync(secondMobileRequest, prismContext);
        var secondHtml = await ReadResponseBodyAsync(secondMobileRequest.Response);

        firstHtml.Should().Contain("--prism-primary:#333333;");
        secondHtml.Should().Contain("--prism-primary:#444444;");
        secondHtml.Should().NotContain("--prism-primary:#333333;");
    }

    [Fact]
    public async Task InvokeAsync_PrefersPrecomputedCssDeclarations_WhenAvailable()
    {
        var middleware = CreateMiddlewareWithHtmlResponse("<html><head></head><body>Demo</body></html>");
        var prismContext = new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#old"
                },
                MobileBrandingOverrides = new Dictionary<string, string>
                {
                    ["--prism-primary"] = "#old-mobile"
                },
                BrandingCssDeclarations = "--prism-primary:#new;",
                MobileBrandingCssDeclarations = "--prism-primary:#new-mobile;"
            }
        };

        var context = CreateGetHtmlContext();
        context.Request.QueryString = new QueryString("?prismMobile=1");

        await middleware.InvokeAsync(context, prismContext);

        var html = await ReadResponseBodyAsync(context.Response);
        html.Should().Contain("--prism-primary:#new;");
        html.Should().Contain("--prism-primary:#new-mobile;");
        html.Should().NotContain("--prism-primary:#old;");
        html.Should().NotContain("--prism-primary:#old-mobile;");
    }

    private static DefaultHttpContext CreateGetHtmlContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        // Provide a RequestServices with a mocked IAuthenticationService so AuthenticateAsync won't throw
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var authMock = new Moq.Mock<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        authMock.Setup(s => s.AuthenticateAsync(Moq.It.IsAny<HttpContext>(), Moq.It.IsAny<string>()))
                .ReturnsAsync(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        services.AddSingleton(authMock.Object);
        context.RequestServices = services.BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static PrismBrandingMiddleware CreateMiddlewareWithHtmlResponse(string html)
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<PrismBrandingMiddleware>();
        return new PrismBrandingMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        }, logger);
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
