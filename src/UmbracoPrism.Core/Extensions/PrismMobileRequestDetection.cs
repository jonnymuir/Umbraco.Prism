using Microsoft.AspNetCore.Http;

namespace UmbracoPrism.Core.Extensions;

public static class PrismMobileRequestDetection
{
    public const string QueryParameterName = "prismMobile";
    public const string CookieName = "prism.mobile";
    public const string PlatformHeaderName = "X-Prism-Platform";

    public static bool IsPrismMobileRequest(HttpContext context)
    {
        if (context.Request.Query.TryGetValue(QueryParameterName, out var queryValue) && IsTruthy(queryValue.ToString()))
        {
            return true;
        }

        if (context.Request.Cookies.TryGetValue(CookieName, out var cookieValue) && IsTruthy(cookieValue))
        {
            return true;
        }

        var platformHeader = context.Request.Headers[PlatformHeaderName].ToString();
        if (platformHeader.Equals("mobile", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var userAgent = context.Request.Headers.UserAgent.ToString();
        return userAgent.Contains("PrismMobile", StringComparison.OrdinalIgnoreCase);
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
}
