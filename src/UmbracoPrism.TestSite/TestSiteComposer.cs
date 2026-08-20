using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using UmbracoPrism.Core;
using UmbracoPrism.Core.Auth;
using UmbracoPrism.Core.Services;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wayfinder.Models.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Services.Sanitization;
using Wayfinder.Umbraco;
using Wayfinder.Umbraco.Extensions;
using Wayfinder.Umbraco.Services;
using UmbracoPrism.TestSite.BackgroundServices;
using UmbracoPrism.TestSite.Services;
using UmbracoPrism.TestSite.Services.ServiceDesign;
using Wayfinder.Engine.Abstractions;

namespace UmbracoPrism.TestSite;

/// <summary>
/// Registers TestSite-specific notification handlers, plus TestSite's own identity/authorization
/// wiring for Wayfinder.Umbraco (a bare package reference supplies none of that — see
/// <see cref="Wayfinder.Umbraco.Configuration.WayfinderServiceDesignOptions"/>'s own remarks).
/// <see cref="MobileNavSchemaSetup"/> runs before <see cref="DemoMobileNavSeeder"/>
/// so the Block List element type exists before the seeder inspects the Settings node.
/// Vinyl Vault: <see cref="VinylVaultContentTypes"/> runs before <see cref="VinylVaultSeeder"/>
/// to ensure content types exist before seeding content.
/// <para>
/// <see cref="ComposeAfterAttribute"/> on <see cref="PrismComposer"/> guarantees that
/// <see cref="PrismContentTypeSeeder"/> is registered — and therefore runs — before any
/// handler registered here.
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

        ComposeWayfinderServiceDesign(builder);
    }

    /// <summary>
    /// Everything Wayfinder.Umbraco needs from a host: identity resolution
    /// (<see cref="PublicVisitorIdentityResolver"/>'s anonymous-cookie-or-Prism-member logic),
    /// the <c>ServiceRequestPolling</c> authorization policy the waiting-screen poll endpoint
    /// requires, and TestSite's two worked examples — see
    /// <c>docs/guides/support-systems.md</c> in the core Wayfinder repo for why service design
    /// itself lives entirely in Wayfinder.Umbraco now, not here.
    /// </summary>
    private static void ComposeWayfinderServiceDesign(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<PublicVisitorIdentityResolver>();
        builder.Services.AddScoped<IPrismPostSignInHandler, PublicServiceRequestPostSignInHandler>();

        builder.Services.AddWayfinderUmbraco(options =>
        {
            options.ResolveTenantId = ctx =>
                ctx.RequestServices.GetRequiredService<IPrismUserContext>().CurrentTenant?.Hostname ?? "default";

            options.ResolveUserId = ctx =>
                ctx.RequestServices.GetRequiredService<PublicVisitorIdentityResolver>().Resolve().UserId;

            // Two disjoint personas for this demo — see NjfContributionsTeam's own remarks for
            // why an authenticated member never also gets PublicVisitorQueue access in the same
            // profile (RestrictToInstanceOwner is a single flag for the whole ActorProfile in
            // Wayfinder.Engine 0.7.2 — no per-queue mechanism exists yet to mix an
            // instance-owner-restricted queue with a team-wide one). A signed-in Prism member can
            // still be BOTH personas at different times, though: the juggling licence journey is
            // reachable while signed in (to pick up the membership-tier fee discount — see
            // apply-for-a-juggling-licence.json's serviceInputsResolver wiring), so this resolver
            // picks the persona from *which page originated the request*, not just whether the
            // caller is authenticated. A real host would resolve team membership from its own
            // claims/role source instead of "any authenticated member".
            options.ResolveAccessProfile = ctx =>
                IsJugglingLicenceContext(ctx)
                    ? PublicVisitorQueue.AccessProfile
                    : ctx.User.Identity?.IsAuthenticated == true
                        ? NjfContributionsTeam.AccessProfile
                        : PublicVisitorQueue.AccessProfile;
        });

        // ServiceRequestPollController (the join-gateway waiting screen's own poll endpoint)
        // requires this policy but deliberately ships with it unregistered — see
        // WayfinderUmbracoAuthorizationPolicies.ServiceRequestPolling's own remarks. Only the
        // bulk-contributions demo's Join gateway ever shows a waiting screen, and that's always an
        // authenticated NJF Contributions Team member (see NjfContributionsTeam), so a plain
        // authenticated-user requirement is enough — no anonymous-visitor case to accommodate here.
        builder.Services.Configure<AuthorizationOptions>(options =>
        {
            options.AddPolicy(WayfinderUmbracoAuthorizationPolicies.ServiceRequestPolling, policy =>
                policy.RequireAuthenticatedUser());
        });

        // Explicit capability contract for both queues — matches Wayfinder.Umbraco's own generic
        // component partials (see Wayfinder.Umbraco's _Component-*.cshtml set): an agent authoring
        // via list_queue_capabilities can see exactly what this host's stock rendering pipeline
        // supports, instead of the check being silently skipped.
        builder.Services.AddSingleton<IQueueCapabilitiesProvider>(new StaticQueueCapabilitiesProvider(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [PublicVisitorQueue.Key] = ComponentTypeRegistry.AllDiscriminators,
                [NjfContributionsTeam.UploadKey] = ComponentTypeRegistry.AllDiscriminators,
                [NjfContributionsTeam.ReviewKey] = ComponentTypeRegistry.AllDiscriminators
            }));

        // Demonstrates the service-sourced field extension point for a logged-in member.
        // Re-registering UmbracoProcessManagerEngine here (after AddWayfinderUmbraco()'s own
        // registration above) supplies the serviceInputsResolver delegate; last registration wins
        // for single-instance resolution, and IProcessManager's factory (also registered by
        // AddWayfinderUmbraco) resolves UmbracoProcessManagerEngine lazily, so it picks up this one.
        builder.Services.AddSingleton<IJugglingSocietyMembershipClient, JugglingSocietyMembershipClient>();

        // Freezes on first read — must run before anything reads SupportSystemRegistry, which
        // this composer's own registrations below never do, but a blueprint load/save does (see
        // MockBusinessAppContributions.Register's own remarks).
        MockBusinessAppContributions.Register();

        // Mock Business App's own resource address — same config key DownstreamDemoController
        // already reads (PrismBusinessApp:ApiBaseUrl, set by UmbracoPrism.AppHost).
        var businessAppBaseUrl = builder.Config["PrismBusinessApp:ApiBaseUrl"];
        builder.Services.AddHttpClient(MockBusinessAppContributionsClient.HttpClientName, client =>
        {
            if (!string.IsNullOrWhiteSpace(businessAppBaseUrl))
            {
                client.BaseAddress = new Uri(businessAppBaseUrl);
            }
        });
        builder.Services.AddSingleton<ISupportSystemClient, MockBusinessAppContributionsClient>();
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
                    if (!string.Equals(definition.DefinitionKey, TestSiteSeedContract.JugglingLicenceBlueprintKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    var membership = membershipClient.GetForUser(instance.UserId);
                    return new Dictionary<string, object?>
                    {
                        ["member"] = new Dictionary<string, object?> { ["tier"] = membership.Tier }
                    };
                },
                sp.GetServices<ISupportSystemClient>());
        });

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WayfinderServicePageContentType>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WayfinderServicePageSeeder>();
    }

    /// <summary>
    /// True for the juggling licence page's own GET render, and for the requests it fans out to
    /// (<see cref="Wayfinder.Umbraco.Controllers.WayfinderStageSurfaceController"/>'s advance POST,
    /// and TestSite's own file upload/download controllers) — none of those carry the originating
    /// blueprint in their own route, only in the <c>Referer</c> header a same-origin form
    /// POST/fetch call always sends (TestSite sets no <c>Referrer-Policy</c> that would strip it).
    /// A heuristic, not a security boundary in itself: it only ever *widens* access from the NJF
    /// team profile down to the narrower, instance-owner-restricted public-visitor one, never the
    /// reverse, so a spoofed Referer could at most make a caller look like an anonymous citizen —
    /// exactly the access level they'd already have signed out.
    /// </summary>
    private static bool IsJugglingLicenceContext(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments(TestSiteSeedContract.JugglingLicencePageUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var referer = ctx.Request.Headers.Referer.ToString();
        return referer.Contains(TestSiteSeedContract.JugglingLicencePageUrl, StringComparison.OrdinalIgnoreCase);
    }
}
