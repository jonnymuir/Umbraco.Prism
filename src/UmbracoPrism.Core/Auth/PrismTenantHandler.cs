using Microsoft.AspNetCore.Authorization;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Enforces tenant isolation by requiring the authenticated user's Entra tenant to match the resolved Prism tenant.
/// </summary>
/// <param name="httpContextAccessor">Provides access to the current request context.</param>
public class PrismTenantHandler(IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<PrismTenantRequirement>
{
    /// <summary>
    /// Evaluates the tenant requirement against the current request tenant context.
    /// </summary>
    /// <param name="context">Authorization context containing the authenticated principal.</param>
    /// <param name="requirement">The Prism tenant isolation requirement.</param>
    /// <returns>A completed task after tenant match evaluation.</returns>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PrismTenantRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null) return Task.CompletedTask;

        var prismUser = httpContext.RequestServices.GetRequiredService<IPrismUserContext>();
        
        // 1. If not authenticated, we let the [Authorize] attribute handle it (Challenge)
        if (!prismUser.IsAuthenticated) return Task.CompletedTask;

        // 2. Get the user's Tenant ID from their JWT token and the current active Tenant
        var userTenantId = prismUser.EntraTenantId;
        var currentTenantId = prismUser.CurrentTenant?.EntraTenantId;

        // 3. STRICT ISOLATION CHECK:
        // If the user's token belongs to a different Azure Tenant than the current domain,
        // we block access entirely.
        if (userTenantId != null && userTenantId == currentTenantId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}