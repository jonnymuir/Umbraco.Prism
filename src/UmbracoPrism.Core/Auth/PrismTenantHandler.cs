using Microsoft.AspNetCore.Authorization;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Enforces tenant isolation by requiring the authenticated user's own token claims to match the
/// resolved Prism tenant. This is an explicit, opt-in backstop — the same check runs automatically
/// for every <c>PrismMemberCookie</c> request via <see cref="Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents.OnValidatePrincipal"/>;
/// declaring the <c>PrismStrictIsolation</c> policy on an endpoint gets a hard 403 instead of an
/// anonymous fallback, and survives even if a future auth scheme forgets to wire the cookie hook.
/// </summary>
/// <param name="prismContext">Provides the tenant resolved for the current request.</param>
/// <param name="tenantBindingValidator">The single shared implementation of the tenant-binding check.</param>
public class PrismTenantHandler(IPrismContext prismContext, IPrismTenantBindingValidator tenantBindingValidator)
    : AuthorizationHandler<PrismTenantRequirement>
{
    /// <summary>
    /// Evaluates the tenant requirement against the current request tenant context.
    /// </summary>
    /// <param name="context">Authorization context containing the authenticated principal.</param>
    /// <param name="requirement">The Prism tenant isolation requirement.</param>
    /// <returns>A completed task after tenant match evaluation.</returns>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PrismTenantRequirement requirement)
    {
        // If not authenticated, we let the [Authorize] attribute handle it (Challenge).
        if (context.User.Identity?.IsAuthenticated != true) return Task.CompletedTask;

        var tenant = prismContext.CurrentTenant;
        if (tenant != null && tenantBindingValidator.IsBound(context.User, tenant))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
