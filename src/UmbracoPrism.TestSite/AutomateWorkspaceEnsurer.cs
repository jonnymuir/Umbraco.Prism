using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Automate has no default workspace on a fresh install, an automation must belong to one, and
/// publishing needs the workspace to have a service-account user with the sections its trigger and
/// actions need. Reuses the first workspace if one exists, otherwise stands up a plain one.
/// Automate requires the service account to be an API user, so this seeds its own, in the
/// Administrators group so it has every section: a deliberate shortcut for a single-tenant demo,
/// not a pattern for a real multi-user host.
/// </summary>
internal static class AutomateWorkspaceEnsurer
{
    // Both demo seeders start together on a fresh install and would each create "default",
    // so the loser would fail on the alias's unique constraint: take turns instead.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private const string ServiceAccountEmail = "automate-service@prism.local";

    /// <summary>The workspace, or null when Automate is not ready yet.</summary>
    internal static async Task<Workspace?> EnsureAsync(
        IWorkspaceService workspaceService,
        IUserService userService,
        CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var (workspaces, _) = await workspaceService.GetWorkspacesPagedAsync(take: 1, cancellationToken: ct);
            var workspace = workspaces.FirstOrDefault()
                ?? await workspaceService.CreateWorkspaceAsync(
                    new Workspace { Alias = "default", Name = "Default" }, cancellationToken: ct);

            var serviceAccountKey = await EnsureServiceAccountAsync(userService);
            if (serviceAccountKey is null)
            {
                return null;
            }

            if (workspace.ServiceAccountKey != serviceAccountKey)
            {
                workspace.ServiceAccountKey = serviceAccountKey.Value;
                await workspaceService.UpdateWorkspaceAsync(workspace, cancellationToken: ct);
            }

            return workspace;
        }
        catch
        {
            // Automate's own schema/services not ready yet: the caller tries again next tick.
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<Guid?> EnsureServiceAccountAsync(IUserService userService)
    {
        if (userService.GetByEmail(ServiceAccountEmail) is { } existing)
        {
            return existing.Key;
        }

        var result = await userService.CreateAsync(
            Constants.Security.SuperUserKey,
            new UserCreateModel
            {
                Email = ServiceAccountEmail,
                UserName = ServiceAccountEmail,
                Name = "Automate service account",
                Kind = UserKind.Api,
                UserGroupKeys = new HashSet<Guid> { Constants.Security.AdminGroupKey },
            },
            approveUser: true);

        return result.Success ? result.Result.CreatedUser?.Key : null;
    }
}
