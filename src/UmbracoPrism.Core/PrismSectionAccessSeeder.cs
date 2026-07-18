using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoPrism.Core.Auth;

namespace UmbracoPrism.Core;

/// <summary>
/// Grants the Prism backoffice section (<c>Prism.Section</c>) to every user group configured as
/// a Prism admin group. Installing an extension manifest never grants a custom section's
/// visibility to any user group automatically — not even to Administrators/superusers, who see
/// only the sections their groups are explicitly allowed — so without this, a feature scoped to
/// <c>Prism.Section</c> (e.g. the CMS Workflow editor) is invisible in the backoffice nav no
/// matter who's logged in, even a correctly-provisioned admin. Runs idempotently — skips a group
/// that already has the section.
/// </summary>
/// <remarks>
/// Deliberately reads the same <see cref="PrismAdminOptions.GroupAliases"/> config
/// <see cref="PrismAdminHandler"/> already uses for API authorization, rather than a
/// separately-configured allow-list or a client-side "is admin" condition — one source of truth
/// keeps backoffice UI visibility and API enforcement aligned instead of two knobs that could
/// silently drift apart.
/// </remarks>
public class PrismSectionAccessSeeder(
    IUserGroupService userGroupService,
    IOptions<PrismAdminOptions> adminOptions,
    IRuntimeState runtimeState,
    ILogger<PrismSectionAccessSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string PrismSectionAlias = "Prism.Section";

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level < RuntimeLevel.Run) return;

        foreach (var groupAlias in adminOptions.Value.GroupAliases ?? [])
        {
            try
            {
                await EnsureSectionGrantedAsync(groupAlias);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PRISM: Failed to grant Prism section to group '{GroupAlias}'; skipping", groupAlias);
            }
        }
    }

    private async Task EnsureSectionGrantedAsync(string groupAlias)
    {
        var group = await userGroupService.GetAsync(groupAlias);
        if (group is null)
        {
            logger.LogDebug("PRISM: User group '{GroupAlias}' not found; skipping section grant", groupAlias);
            return;
        }

        if (group.AllowedSections.Contains(PrismSectionAlias))
        {
            return;
        }

        group.AddAllowedSection(PrismSectionAlias);
        var result = await userGroupService.UpdateAsync(group, Constants.Security.SuperUserKey);

        if (result.Success)
        {
            logger.LogInformation("PRISM: Granted Prism section access to user group '{GroupAlias}'", groupAlias);
        }
        else
        {
            logger.LogWarning("PRISM: Failed to save section grant for user group '{GroupAlias}' — {Status}", groupAlias, result.Status);
        }
    }
}
