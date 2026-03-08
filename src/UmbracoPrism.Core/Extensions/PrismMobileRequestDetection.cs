using Microsoft.AspNetCore.Http;

namespace UmbracoPrism.Core.Extensions;

public static class PrismMobileRequestDetection
{
    public const string QueryParameterName = "prismMobile";
    public const string CookieName = "prism.mobile";
    public const string PlatformHeaderName = "X-Prism-Platform";

    public static bool IsPrismMobileRequest(HttpContext context)
    {
        return ResolveMobileSignal(context).isMobile;
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
