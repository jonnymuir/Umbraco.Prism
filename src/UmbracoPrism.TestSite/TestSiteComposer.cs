using UmbracoPrism.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Core.Services.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.TestSite.BackgroundServices;
using UmbracoPrism.TestSite.Services;
using UmbracoPrism.WorkflowRuntime.Abstractions;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Registers TestSite-specific notification handlers.
/// <see cref="MobileNavSchemaSetup"/> runs before <see cref="DemoMobileNavSeeder"/>
/// so the Block List element type exists before the seeder inspects the Settings node.
/// Vinyl Vault: <see cref="VinylVaultContentTypes"/> runs before <see cref="VinylVaultSeeder"/>
/// to ensure content types exist before seeding content.
/// <para>
/// <see cref="ComposeAfterAttribute"/> on <see cref="PrismComposer"/> guarantees that
/// <see cref="PrismContentTypeSeeder"/> is registered — and therefore runs — before any
/// handler registered here. <see cref="WorkflowPageSeeder"/> depends on <c>workflowPage</c>
/// and <c>workflowHub</c> content types created by <see cref="PrismContentTypeSeeder"/>.
/// Umbraco dispatches <see cref="UmbracoApplicationStartedNotification"/> handlers
/// sequentially in registration order, so composer ordering is the correct coordination mechanism.
/// </para>
/// </summary>
[ComposeAfter(typeof(PrismComposer))]
public class TestSiteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Mobile navigation demo
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, MobileNavSchemaSetup>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, DemoMobileNavSeeder>();

        // Localhost tenant (Keycloak) — dev only, idempotent
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, DemoTenantSeeder>();
        
        // Vinyl Vault demo (Phase 2: Notifications)
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, VinylVaultContentTypes>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, VinylVaultSeeder>();
        builder.Services.AddHostedService<LimitedEditionDropNotifier>();

        // Workflow Page demo — runs after PrismContentTypeSeeder has created the workflowPage doc type
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WorkflowPageSeeder>();

        // Prism CMS Workflow demos ("Apply for a juggling licence" and "Transfer a professional
        // juggling licence") — demonstrates the service-sourced field extension point for a
        // logged-in member, shared across both since they're the same fictional membership
        // scheme. Re-registering
        // CmsWorkflowEngine here (after AddPrismCmsWorkflow()'s own registration in
        // PrismComposer) supplies the serviceInputsResolver delegate; last registration wins
        // for single-instance resolution, and IWorkflowRuntimeEngine's factory (registered by
        // Core) resolves CmsWorkflowEngine lazily, so it picks up this one.
        builder.Services.AddSingleton<IJugglingSocietyMembershipClient, JugglingSocietyMembershipClient>();
        builder.Services.AddSingleton(sp =>
        {
            var membershipClient = sp.GetRequiredService<IJugglingSocietyMembershipClient>();
            return new CmsWorkflowEngine(
                sp.GetRequiredService<ILogger<CmsWorkflowEngine>>(),
                sp.GetRequiredService<IWorkflowDefinitionStore>(),
                sp.GetRequiredService<IWorkflowContentSanitizer>(),
                sp.GetRequiredService<IWorkflowInstanceStore>(),
                (instance, definition, _) =>
                {
                    var isJugglingLicenceWorkflow =
                        string.Equals(definition.DefinitionKey, TestSiteSeedContract.JugglingLicenceWorkflowKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(definition.DefinitionKey, TestSiteSeedContract.JugglingLicenceTransferWorkflowKey, StringComparison.OrdinalIgnoreCase);
                    if (!isJugglingLicenceWorkflow)
                    {
                        return null;
                    }

                    var membership = membershipClient.GetForUser(instance.UserId);
                    return new Dictionary<string, object?>
                    {
                        ["member"] = new Dictionary<string, object?> { ["tier"] = membership.Tier }
                    };
                });
        });
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, JugglingLicenceCmsWorkflowSeeder>();

        // Guidance articles for the (separately, live-authored) "Transfer a Professional
        // Juggling Licence" CMS Workflow demo — seeded ahead of time so that build has real
        // CMS content to link to rather than needing to author it on camera.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, GuidanceArticleContentTypes>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, GuidanceArticleSeeder>();
    }
}
