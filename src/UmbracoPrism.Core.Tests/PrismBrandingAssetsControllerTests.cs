using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Extensions;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Tests;

/// <summary>
/// Tests for the branding CSS endpoint that replaced inline HTML injection (SEC-PT2-004 CSP
/// follow-up): hosts reference this with a plain
/// <c>&lt;link rel="stylesheet" href="/umbraco/prism/branding.css"&gt;</c> instead of Prism
/// splicing a &lt;style&gt; tag into every response. HTTP-shape concerns only — the
/// CSS-building precedence itself is covered by <c>PrismBrandingCssBuilderTests</c>.
/// </summary>
public class PrismBrandingAssetsControllerTests
{
    [Fact]
    public void BrandingCss_ReturnsTextCss_ForATenantWithOverrides()
    {
        var controller = BuildController(new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string> { ["--prism-primary"] = "#0055ff" }
            }
        });

        var result = controller.BrandingCss();

        result.ContentType.Should().Be("text/css");
        result.Content.Should().Be(":root{--prism-primary:#0055ff;}");
    }

    [Fact]
    public void BrandingCss_ReturnsEmptyCss_ForATenantWithNoOverrides()
    {
        var controller = BuildController(new TestPrismContext { CurrentTenant = new PrismTenant() });

        var result = controller.BrandingCss();

        result.ContentType.Should().Be("text/css");
        result.Content.Should().BeEmpty();
    }

    [Fact]
    public void BrandingCss_ReturnsEmptyCss_WhenNoTenantIsResolved()
    {
        var controller = BuildController(new TestPrismContext { CurrentTenant = null });

        var result = controller.BrandingCss();

        result.ContentType.Should().Be("text/css");
        result.Content.Should().BeEmpty();
    }

    [Fact]
    public void BrandingCss_LayersMobileOverrides_WhenRequestIsDetectedAsMobile()
    {
        var controller = BuildController(new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string> { ["--prism-primary"] = "#0055ff" },
                MobileBrandingOverrides = new Dictionary<string, string> { ["--prism-primary"] = "#003399" }
            }
        }, queryString: "?prismMobile=1");

        var result = controller.BrandingCss();

        result.Content.Should().Be(":root{--prism-primary:#0055ff;--prism-primary:#003399;}");
    }

    [Fact]
    public void BrandingCss_ExcludesMobileOverrides_WhenRequestIsNotDetectedAsMobile()
    {
        var controller = BuildController(new TestPrismContext
        {
            CurrentTenant = new PrismTenant
            {
                BrandingOverrides = new Dictionary<string, string> { ["--prism-primary"] = "#0055ff" },
                MobileBrandingOverrides = new Dictionary<string, string> { ["--prism-primary"] = "#003399" }
            }
        });

        var result = controller.BrandingCss();

        result.Content.Should().Be(":root{--prism-primary:#0055ff;}");
    }

    private static PrismBrandingAssetsController BuildController(
        IPrismContext prismContext, string? queryString = null)
    {
        var httpContext = new DefaultHttpContext();
        if (queryString is not null)
        {
            httpContext.Request.QueryString = new QueryString(queryString);
        }

        return new PrismBrandingAssetsController(prismContext)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private sealed class TestPrismContext : IPrismContext
    {
        public PrismTenant? CurrentTenant { get; set; }
        public string? LastAuthorizationFailureReason => null;

        public Task<System.Net.Http.Headers.AuthenticationHeaderValue?> GetAuthorizationHeaderAsync(bool forceRefresh = false)
        {
            return Task.FromResult<System.Net.Http.Headers.AuthenticationHeaderValue?>(null);
        }
    }
}
