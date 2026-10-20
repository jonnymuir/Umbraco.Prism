using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Extensions;

public static class PrismMobileRequestDetection
{
    public const string QueryParameterName = "prismMobile";
    public const string CookieName = "prism.mobile";
    public const string PlatformHeaderName = "X-Prism-Platform";

    /// <summary>
    /// Appended to the user agent by generated apps whose tabs are native, so a page knows not to
    /// draw its own web tab bar.
    /// </summary>
    public const string NativeTabsUserAgentToken = "PrismNativeTabs";

    /// <summary>
    /// Appended to the user agent by generated apps that sign in through the system
    /// authentication session, so a sign-in challenge is sent to <c>/auth/login</c> for the app to
    /// intercept rather than to the identity provider.
    /// </summary>
    public const string SystemSignInUserAgentToken = "PrismSystemSignIn";

    public static bool IsPrismMobileRequest(HttpContext context)
    {
        return ResolveMobileSignal(context).isMobile;
    }

    /// <summary>
    /// True only when the raw <c>User-Agent</c> header itself carries the native marker —
    /// unlike <see cref="IsPrismMobileRequest"/>, deliberately ignores the query param, cookie,
    /// and platform header overrides, since those three are exactly the demo/testing mechanisms
    /// a plain desktop browser can set (the demo toggle widget's own JS can spoof
    /// <c>navigator.userAgent</c> client-side for its own purposes, but that never touches the
    /// actual HTTP header the server reads here). A real generated app's <c>appendUserAgent</c>
    /// Capacitor config bakes the marker into every request it ever makes, for the life of the
    /// app — not reproducible from a normal browser without deliberately overriding its own
    /// User-Agent in devtools.
    ///
    /// Exists specifically to gate the mobile-UA demo toggle widget itself: it needs to be
    /// reachable on any browser, in any environment, including when a cookie currently has
    /// mobile faked on (that's exactly when you need it most, to turn the fake back off) — the
    /// only case it must never appear is inside the genuine compiled app. Checking the plain
    /// negation of <see cref="IsPrismMobileRequest"/> would be wrong here: a real generated
    /// app's very first page load carries <c>?prismMobile=1</c> in its own start URL (see
    /// <c>MobileBundleService.AddPrismMobileQueryFlag</c>), which resolves via the query
    /// override and would incorrectly read as "not native" for that one request.
    /// </summary>
    public static bool IsNativeMobileRequest(HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString();
        return userAgent.Contains("PrismMobile", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasNativeTabs(HttpContext context)
    {
        return context.Request.Headers.UserAgent.ToString().Contains(NativeTabsUserAgentToken, StringComparison.Ordinal);
    }

    public static bool HasSystemSignIn(HttpContext context)
    {
        return context.Request.Headers.UserAgent.ToString().Contains(SystemSignInUserAgentToken, StringComparison.Ordinal);
    }

    /// <summary>
    /// True for a request from a generated app that signs in through the system authentication
    /// session, on a platform whose app the tenant's site vouches for. Such an app is given
    /// Prism's own sign-in page at <c>/auth/login</c> rather than the identity provider.
    /// </summary>
    public static bool SignsInNatively(HttpContext context, PrismTenant? tenant)
    {
        if (tenant?.HasSignInConfigured() != true || !HasSystemSignIn(context))
        {
            return false;
        }

        var android = context.Request.Headers.UserAgent.ToString().Contains("Android", StringComparison.OrdinalIgnoreCase);
        return android ? tenant.MobileAppLinks.HasAndroid : tenant.MobileAppLinks.HasIos;
    }

    public static string GetPrismMobileDetectionSource(HttpContext context)
    {
        return ResolveMobileSignal(context).source;
    }

    public static bool? GetPrismMobileQueryFlag(HttpContext context)
    {
        if (!context.Request.Query.TryGetValue(QueryParameterName, out var queryValue))
        {
            return null;
        }

        return IsTruthy(queryValue.ToString());
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static (bool isMobile, string source) ResolveMobileSignal(HttpContext context)
    {
        var queryFlag = GetPrismMobileQueryFlag(context);
        if (queryFlag.HasValue)
        {
            return (queryFlag.Value, queryFlag.Value ? "query" : "query (off)");
        }

        if (context.Request.Cookies.TryGetValue(CookieName, out var cookieValue) && IsTruthy(cookieValue))
        {
            return (true, "cookie");
        }

        var platformHeader = context.Request.Headers[PlatformHeaderName].ToString();
        if (platformHeader.Equals("mobile", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "header");
        }

        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (userAgent.Contains("PrismMobile", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "user-agent");
        }

        return (false, "none");
    }
}
