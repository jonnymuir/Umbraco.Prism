using UmbracoPrism.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

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

        // Workflow Page demo — runs after PrismContentTypeSeeder has created the workflowPage doc type
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WorkflowPageSeeder>();
        
        // Content published notification handler for push notifications
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedHandler>();
    }
}
