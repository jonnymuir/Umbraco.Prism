using FluentAssertions;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Extensions;

namespace UmbracoPrism.Core.Tests;

public class PrismMobileRequestDetectionTests
{
    [Fact]
    public void IsNativeMobileRequest_IsTrue_WhenUserAgentCarriesTheMarker()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = "Mozilla/5.0 (iPhone) AppleWebKit/605.1.15 PrismMobile";

        PrismMobileRequestDetection.IsNativeMobileRequest(context).Should().BeTrue();
    }

    [Fact]
    public void IsNativeMobileRequest_IsFalse_ForAPlainBrowserUserAgent()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15";

        PrismMobileRequestDetection.IsNativeMobileRequest(context).Should().BeFalse();
    }

    [Fact]
    public void IsNativeMobileRequest_IgnoresTheQueryOverride_UnlikeIsPrismMobileRequest()
    {
        // The real generated app's own first page load carries ?prismMobile=1 in its start URL
        // (MobileBundleService.AddPrismMobileQueryFlag) — a plain desktop browser can set the
        // exact same query param. IsNativeMobileRequest must not be fooled by it either way.
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?prismMobile=1");
        context.Request.Headers.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15";

        PrismMobileRequestDetection.IsPrismMobileRequest(context).Should().BeTrue(
            "the query override alone is enough for the general rendering-intent check");
        PrismMobileRequestDetection.IsNativeMobileRequest(context).Should().BeFalse(
            "a query param proves nothing about the raw User-Agent");
    }

    [Fact]
    public void IsNativeMobileRequest_IgnoresTheCookieOverride()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{PrismMobileRequestDetection.CookieName}=1";
        context.Request.Headers.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15";

        PrismMobileRequestDetection.IsPrismMobileRequest(context).Should().BeTrue();
        PrismMobileRequestDetection.IsNativeMobileRequest(context).Should().BeFalse(
            "a lingering demo cookie must not be mistaken for the genuine app — this is exactly " +
            "the stuck-cookie scenario the method exists to get right");
    }

    [Fact]
    public void IsNativeMobileRequest_IgnoresThePlatformHeaderOverride()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[PrismMobileRequestDetection.PlatformHeaderName] = "mobile";
        context.Request.Headers.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15";

        PrismMobileRequestDetection.IsPrismMobileRequest(context).Should().BeTrue();
        PrismMobileRequestDetection.IsNativeMobileRequest(context).Should().BeFalse();
    }

    [Fact]
    public void IsNativeMobileRequest_IsTrue_OnTheRealApps_OwnFirstPageLoad()
    {
        // The generated app's WebView carries the UA marker on every single request it ever
        // makes (Capacitor's appendUserAgent config) — including its very first, which also
        // happens to carry ?prismMobile=1. Both signals agree here; this pins that down.
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?prismMobile=1");
        context.Request.Headers.UserAgent = "Mozilla/5.0 (iPhone) AppleWebKit/605.1.15 PrismMobile";

        PrismMobileRequestDetection.IsNativeMobileRequest(context).Should().BeTrue();
    }
}
