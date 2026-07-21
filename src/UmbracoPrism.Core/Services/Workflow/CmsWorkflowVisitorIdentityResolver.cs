using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// Resolves "who this CMS Workflow request is from" — the authenticated Prism Member's stable
/// identity when logged in, otherwise an anonymous session identity held in a dedicated
/// correlation cookie (not ASP.NET Core's own Session middleware — that state store isn't
/// durable by default, which would silently orphan the visitor's identity, and with it their
/// in-progress instance, on exactly the app-pool recycle this design is meant to survive). The
/// cookie's sliding expiry is refreshed on every request, giving "no longer than the user's
/// session" without depending on any server-side session store surviving between requests.
/// </summary>
/// <remarks>
/// Extracted so every CMS-Workflow-facing endpoint that needs to resolve "whose instance is
/// this" — <see cref="InProcessCmsWorkflowClient"/>'s GET/POST/advance flow, and a file
/// download endpoint that never goes through that client — shares one identity resolution
/// implementation rather than each re-deriving the cookie logic independently.
/// </remarks>
public sealed class CmsWorkflowVisitorIdentityResolver(
    IPrismUserContext userContext,
    IHttpContextAccessor httpContextAccessor)
{
    private const string AnonymousVisitorCookieName = "PrismCmsWorkflowVisitor";
    private static readonly TimeSpan AnonymousVisitorTtl = TimeSpan.FromMinutes(30);

    public (string TenantId, string UserId) Resolve()
    {
        var tenantId = userContext.CurrentTenant?.Hostname ?? "default";

        if (userContext.IsAuthenticated && !string.IsNullOrWhiteSpace(userContext.Email))
        {
            return (tenantId, userContext.Email);
        }

        return (tenantId, ResolveAnonymousVisitorId());
    }

    private string ResolveAnonymousVisitorId()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            // No request in flight (e.g. a background caller) — nothing to correlate against.
            return Guid.NewGuid().ToString();
        }

        if (httpContext.Request.Cookies.TryGetValue(AnonymousVisitorCookieName, out var existing)
            && !string.IsNullOrWhiteSpace(existing))
        {
            httpContext.Response.Cookies.Append(AnonymousVisitorCookieName, existing, BuildCookieOptions());
            return existing;
        }

        var visitorId = Guid.NewGuid().ToString();
        httpContext.Response.Cookies.Append(AnonymousVisitorCookieName, visitorId, BuildCookieOptions());
        return visitorId;
    }

    private static CookieOptions BuildCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.Add(AnonymousVisitorTtl),
        IsEssential = true
    };
}
