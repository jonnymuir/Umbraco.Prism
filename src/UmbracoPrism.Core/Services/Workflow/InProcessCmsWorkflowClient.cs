using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core.Models.Workflow;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// <see cref="IBusinessAppWorkflowClient"/> for CMS Workflow — the seam that lets
/// <c>PrismWorkflowPageController{TViewModel}</c>'s entire existing
/// GET/POST/antiforgery/nonce/PRG stack run a CMS Workflow with zero new controller or
/// rendering code. Where <c>BusinessAppWorkflowClient</c> makes an HTTP call to a remote
/// Business App, this calls <see cref="CmsWorkflowEngine"/> directly in-process.
/// </summary>
/// <remarks>
/// Registered as a keyed service (key <c>"cms"</c>) so the default, unkeyed
/// <see cref="IBusinessAppWorkflowClient"/> registration used by every business-workflow demo
/// page is untouched.
/// </remarks>
public sealed class InProcessCmsWorkflowClient(
    CmsWorkflowEngine engine,
    IPrismUserContext userContext,
    IHttpContextAccessor httpContextAccessor) : IBusinessAppWorkflowClient
{
    private const string AnonymousVisitorCookieName = "PrismCmsWorkflowVisitor";
    private static readonly TimeSpan AnonymousVisitorTtl = TimeSpan.FromMinutes(30);

    private static readonly WorkflowAccessProfile AccessProfile = new()
    {
        VisibleQueues = [CmsWorkflowQueue.Key],
        StartableQueues = [CmsWorkflowQueue.Key],
        ActionableQueues = [CmsWorkflowQueue.Key],
        RestrictToInstanceOwner = true
    };

    public Task<WorkflowResponseEnvelope> GetCurrentAsync(
        string workflowKey,
        string? instanceId = null,
        string? action = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = ResolveIdentity();
        return Task.FromResult(engine.GetCurrent(workflowKey, tenantId, userId, AccessProfile, instanceId, action));
    }

    public Task<WorkflowResponseEnvelope> AdvanceAsync(
        string workflowKey,
        string instanceId,
        string action,
        int stateVersion,
        Dictionary<string, object?>? fieldValues = null,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = ResolveIdentity();
        return Task.FromResult(
            engine.Advance(instanceId, tenantId, userId, AccessProfile, action, stateVersion, fieldValues));
    }

    public Task<WorkflowInstanceListEnvelope> GetInstancesAsync(
        bool allowRefreshRetry = true,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = ResolveIdentity();
        return Task.FromResult(engine.GetInstances(tenantId, userId));
    }

    /// <summary>
    /// <c>userId</c> = the authenticated Prism Member's stable identity when logged in,
    /// otherwise an anonymous session identity held in a dedicated correlation cookie (not
    /// ASP.NET Core's own Session middleware — that state store isn't durable by default, which
    /// would silently orphan the visitor's identity, and with it their in-progress instance, on
    /// exactly the app-pool recycle this design is meant to survive). The cookie's sliding
    /// expiry is refreshed on every request, giving "no longer than the user's session" without
    /// depending on any server-side session store surviving between requests.
    /// </summary>
    private (string TenantId, string UserId) ResolveIdentity()
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
