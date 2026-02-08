using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Security;

namespace UmbracoPrism.Core.Auth;

public class PrismAdminHandler(
    IBackOfficeSecurityAccessor securityAccessor,
    IOptions<PrismAdminOptions> options) : AuthorizationHandler<PrismAdminRequirement>
{
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
