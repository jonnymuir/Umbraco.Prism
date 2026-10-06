using Microsoft.Extensions.Configuration;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Services;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Automate has no default workspace on a fresh install, an automation must belong to one, and
/// publishing needs the workspace to have a service-account user with the sections its trigger and
/// actions need. Reuses the first workspace if one exists, otherwise stands up a plain one whose
/// service account is TestSite's own unattended admin (an Administrator, so it has every section:
/// a deliberate shortcut for a single-tenant demo, not a pattern for a real multi-user host).
/// </summary>
internal static class AutomateWorkspaceEnsurer
{
    // Both demo seeders start together on a fresh install and would each create "default",
    // so the loser would fail on the alias's unique constraint: take turns instead.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>The workspace, or null when Automate or the admin user is not ready yet.</summary>
    internal static async Task<Workspace?> EnsureAsync(
        IWorkspaceService workspaceService,
        IUserService userService,
        IConfiguration configuration,
        CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var (workspaces, _) = await workspaceService.GetWorkspacesPagedAsync(take: 1, cancellationToken: ct);
            var workspace = workspaces.FirstOrDefault()
                ?? await workspaceService.CreateWorkspaceAsync(
                    new Workspace { Alias = "default", Name = "Default" }, cancellationToken: ct);

            if (workspace.ServiceAccountKey == Guid.Empty)
            {
                var adminEmail = configuration["Umbraco:CMS:Unattended:UnattendedUserEmail"] ?? "admin@prism.local";
                var admin = userService.GetByEmail(adminEmail);
                if (admin is null)
                {
                    return null;
                }

                workspace.ServiceAccountKey = admin.Key;
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
}
