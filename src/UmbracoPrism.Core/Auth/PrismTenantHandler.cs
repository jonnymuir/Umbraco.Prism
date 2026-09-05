using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using UmbracoPrism.Core.Models;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Enforces tenant isolation by requiring the authenticated user's own token claims to match the
/// resolved Prism tenant. This is an explicit, opt-in backstop — the same check runs automatically
/// for every <c>PrismMemberCookie</c> request via <see cref="Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents.OnValidatePrincipal"/>;
/// declaring the <c>PrismStrictIsolation</c> policy on an endpoint gets a hard 403 instead of an
/// anonymous fallback, and survives even if a future auth scheme forgets to wire the cookie hook.
/// </summary>
/// <param name="httpContextAccessor">
/// Resolves the current request's scoped <see cref="IPrismContext"/> per call — matches
/// <see cref="Models.PrismOidcConfiguration"/>'s own pattern for reaching a scoped service from a
/// singleton, and keeps this handler a Singleton like every other <c>IAuthorizationHandler</c>
/// registered alongside it (constructor-injecting <see cref="IPrismContext"/> directly, tried
/// briefly, is what a Scoped registration would otherwise require — but ASP.NET Core's own
/// built-in authorization/event-source singletons directly constructor-inject the Scoped
/// <c>IAuthorizationService</c> throughout the framework, and .NET's <c>ValidateOnBuild</c> graph
/// validator flags *any* Scoped <c>IAuthorizationHandler</c> in the same collection as suspect
/// once it starts walking that graph — a real, if surprising, interaction with unrelated
/// framework-registered handlers, not a bug in this class itself).
/// </param>
/// <param name="tenantBindingValidator">The single shared implementation of the tenant-binding check.</param>
public class PrismTenantHandler(IHttpContextAccessor httpContextAccessor, IPrismTenantBindingValidator tenantBindingValidator)
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

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return Task.CompletedTask;

        var prismContext = httpContext.RequestServices.GetRequiredService<IPrismContext>();
        var tenant = prismContext.CurrentTenant;
        if (tenant != null && tenantBindingValidator.IsBound(context.User, tenant))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
