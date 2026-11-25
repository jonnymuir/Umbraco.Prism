using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Extensions;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Persists the <c>?prismMobile=</c> query flag as a durable cookie, so mobile detection
/// survives once the query string is gone from subsequent in-app navigation (the Capacitor
/// bootstrap shell appends <c>?prismMobile=1</c> only to its own initial Start URL — see
/// <see cref="Services.MobileBundleService"/>).
///
/// It only touches the request's cookies, unconditionally and cheaply, on every request. Tenant
/// branding CSS is served by <see cref="Controllers.PrismBrandingAssetsController"/> and the
/// mobile shell's scripts are static files under <c>wwwroot/mobile-shell/</c>, which a host
/// references in its own layout (see docs/walkthroughs/building-a-mobile-app.md). Externally
/// referenced content needs no CSP inline-content exception.
/// </summary>
public class PrismMobileCookieMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        PersistMobileQueryFlagAsCookie(context);

        await next(context);
    }

    private static void PersistMobileQueryFlagAsCookie(HttpContext context)
    {
        var queryFlag = PrismMobileRequestDetection.GetPrismMobileQueryFlag(context);
        if (!queryFlag.HasValue)
        {
            return;
        }

        if (queryFlag.Value)
        {
            context.Response.Cookies.Append(
                PrismMobileRequestDetection.CookieName,
                "1",
                new CookieOptions
                {
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = context.Request.IsHttps,
                    Path = "/"
                });
            return;
        }

        context.Response.Cookies.Delete(
            PrismMobileRequestDetection.CookieName,
            new CookieOptions
            {
                Path = "/"
            });
    }
}
