using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Extensions;

namespace UmbracoPrism.Core.Middleware;

/// <summary>
/// Persists the <c>?prismMobile=</c> query flag as a durable cookie, so mobile detection
/// survives once the query string is gone from subsequent in-app navigation (the Capacitor
/// bootstrap shell appends <c>?prismMobile=1</c> only to its own initial Start URL — see
/// <see cref="Services.MobileBundleService"/>).
///
/// Formerly named <c>PrismBrandingMiddleware</c> and responsible for a great deal more: it used
/// to buffer every HTML response and splice tenant branding CSS, mobile-shell CSS/JS, and
/// biometric enroll/autologin scripts inline into the page (SEC-PT2-004 CSP follow-up). All of
/// that content is now served as ordinary externally-referenced resources instead —
/// <see cref="Controllers.PrismBrandingAssetsController"/> for the tenant-specific branding CSS,
/// and plain static files under <c>wwwroot/mobile-shell/</c> for the rest — which a host
/// references explicitly in its own layout (see docs/walkthroughs/building-a-mobile-app.md),
/// the same way Prism's other front-end assets already work (e.g. <c>prism-mobile-nav.js</c>).
/// Externally-referenced content needs no CSP inline-content exception regardless of how dynamic
/// it is, and this middleware no longer needs to buffer/rewrite the response body at all — it
/// only ever touches the request's cookies now, unconditionally and cheaply, on every request.
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
