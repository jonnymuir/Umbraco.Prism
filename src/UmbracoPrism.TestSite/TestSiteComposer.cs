using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core;
using UmbracoPrism.Core.Auth;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Sanitization;
using Wayfinder.Umbraco;
using Wayfinder.Umbraco.Services;
using UmbracoPrism.TestSite.BackgroundServices;
using UmbracoPrism.TestSite.Services;
using UmbracoPrism.TestSite.Services.ServiceDesign;
using Wayfinder.Engine.Abstractions;

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
/// handler registered here. <see cref="StagePageSeeder"/> depends on <c>stagePage</c>
/// and <c>serviceRequestHub</c> content types created by <see cref="PrismContentTypeSeeder"/>.
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

        // Stage Page demo — runs after PrismContentTypeSeeder has created the stagePage doc type
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, StagePageSeeder>();

        // TestSite's own public service request demo ("Apply for a juggling licence" and "Transfer
        // a professional juggling licence") — an anonymous-first, in-Umbraco-hosted service
        // blueprint running entirely in-process against Wayfinder.Umbraco's engine (no remote
        // Business App). Content type, identity resolution, keyed process-manager client, and
        // post-sign-in reattachment are all host-owned here (not a Core package feature) — see
        // PublicServiceRequestContentType / PublicVisitorIdentityResolver /
        // InProcessPublicVisitorProcessManagerClient / PublicServiceRequestPostSignInHandler.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, PublicServiceRequestContentType>();

        builder.Services.AddScoped<PublicVisitorIdentityResolver>();
        builder.Services.AddKeyedScoped<IBusinessAppProcessManagerClient, InProcessPublicVisitorProcessManagerClient>(WayfinderUmbracoServiceKeys.InProcessQueueClient);
        builder.Services.AddScoped<IPrismPostSignInHandler, PublicServiceRequestPostSignInHandler>();

        // Explicit capability contract for public-visitor — matches Wayfinder.Umbraco's own
        // generic component partials (see Wayfinder.Umbraco's _Component-*.cshtml set): an agent
        // authoring via list_queue_capabilities can see exactly what this host's stock rendering
        // pipeline supports, instead of the check being silently skipped.
        builder.Services.AddSingleton<IQueueCapabilitiesProvider>(new StaticQueueCapabilitiesProvider(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [PublicVisitorQueue.Key] = ComponentTypeCatalog.AllDiscriminators
            }));

        // Demonstrates the service-sourced field extension point for a logged-in member, shared
        // across both juggling-licence demos since they're the same fictional membership scheme.
        // Re-registering UmbracoProcessManagerEngine here (after AddWayfinderUmbraco()'s own
        // registration via PrismComposer's AddPrismProcessManager()) supplies the
        // serviceInputsResolver delegate; last registration wins for single-instance resolution,
        // and IProcessManager's factory (registered by Wayfinder.Umbraco) resolves
        // UmbracoProcessManagerEngine lazily, so it picks up this one.
        builder.Services.AddSingleton<IJugglingSocietyMembershipClient, JugglingSocietyMembershipClient>();
        builder.Services.AddSingleton(sp =>
        {
            var membershipClient = sp.GetRequiredService<IJugglingSocietyMembershipClient>();
            return new UmbracoProcessManagerEngine(
                sp.GetRequiredService<ILogger<UmbracoProcessManagerEngine>>(),
                sp.GetRequiredService<IServiceBlueprintStore>(),
                sp.GetRequiredService<IServiceContentSanitizer>(),
                sp.GetRequiredService<IServiceRequestStore>(),
                sp.GetRequiredService<IHttpContextAccessor>(),
                (instance, definition, _) =>
                {
                    var isJugglingLicenceServiceBlueprint =
                        string.Equals(definition.DefinitionKey, TestSiteSeedContract.JugglingLicenceBlueprintKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(definition.DefinitionKey, TestSiteSeedContract.JugglingLicenceTransferBlueprintKey, StringComparison.OrdinalIgnoreCase);
                    if (!isJugglingLicenceServiceBlueprint)
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
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, JugglingLicenceServiceRequestSeeder>();

        // Guidance articles for "Transfer a Professional Juggling Licence" — seeded ahead of
        // time so a live MCP build has real CMS content to link to rather than needing to author
        // it on camera. Must run before LicenceTransferCmsServiceBlueprintSeeder below only in the sense
        // that both are idempotent and order-independent; listed here because it's the same demo.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, GuidanceArticleContentTypes>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, GuidanceArticleSeeder>();

        // The "here's one we made earlier" reference copy of the definition the recording builds
        // live via MCP — see LicenceTransferServiceRequestSeeder's own remarks for what a future
        // re-recording needs to do first.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, LicenceTransferServiceRequestSeeder>();
    }
}
