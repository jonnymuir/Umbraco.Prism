using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Security;

namespace UmbracoPrism.Core.Auth;

/// <summary>
/// Authorizes Prism administrative operations for users in configured backoffice groups.
/// </summary>
/// <param name="securityAccessor">Provides access to the current Umbraco backoffice user.</param>
/// <param name="options">Supplies allowed group aliases for Prism admin access.</param>
public class PrismAdminHandler(
    IBackOfficeSecurityAccessor securityAccessor,
    IOptions<PrismAdminOptions> options) : AuthorizationHandler<PrismAdminRequirement>
{
    /// <summary>
    /// Evaluates whether the current backoffice user belongs to a configured Prism admin group.
    /// </summary>
    /// <param name="context">Authorization context containing the current principal.</param>
    /// <param name="requirement">The Prism admin requirement to evaluate.</param>
    /// <returns>A completed task after evaluating and optionally succeeding the requirement.</returns>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PrismAdminRequirement requirement)
    {
        var security = securityAccessor.BackOfficeSecurity;
        var currentUser = security?.CurrentUser;

        if (currentUser == null)
        {
            return Task.CompletedTask;
        }

        var allowedAliases = options.Value.GroupAliases ?? [];

        if (allowedAliases.Length == 0)
        {
            return Task.CompletedTask;
        }

        var isAdmin = currentUser.Groups?.Any(group =>
            allowedAliases.Contains(group.Alias, StringComparer.OrdinalIgnoreCase)) == true;

        if (isAdmin)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
