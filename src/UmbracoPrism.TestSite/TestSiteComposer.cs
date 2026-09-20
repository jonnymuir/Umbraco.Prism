using System.Security.Claims;
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

            // Three personas for this demo, not two: an anonymous/plain-member citizen
            // (public-visitor), an NJF Contributions Team caseworker (NjfContributionsTeam —
            // only njf-caseworker@prism.local in keycloak/realm-export.json, see that class's own
            // roster remarks), and a signed-in Prism member who is neither (NoAccessProfile —
            // demo@prism.local is deliberately this, so signing in never silently grants NJF
            // access just from being authenticated). See NjfContributionsTeam's own remarks for
            // why an NJF caseworker never also gets PublicVisitorQueue access in the same profile
            // (RestrictToInstanceOwner is a single flag for the whole ActorProfile in
            // Wayfinder.Engine 0.7.2 — no per-queue mechanism exists yet to mix an
            // instance-owner-restricted queue with a team-wide one). Every persona can still reach
            // the juggling licence journey, though (an NJF caseworker is also a citizen; a plain
            // member gets their membership-tier fee discount — see
            // apply-for-a-juggling-licence.json's serviceInputsResolver wiring): this resolver
            // picks the persona from the blueprint the current call is scoped to, not just from
            // who's signed in.
            //
            // Keyed directly off Wayfinder.Umbraco's own resolved blueprintKey parameter
            // (Wayfinder.Umbraco 2.0+, jonnymuir/Wayfinder.Umbraco#104) rather than guessing it
            // from the request's own path/form/query shape — a prior version of this resolver did
            // exactly that (StartsWithSegments against the page path and the stage-advance POST's
            // own form field) and missed ServiceRequestPollController's poll GET entirely (its own
            // blueprintKey travels as a query parameter, a third shape nothing here recognised): a
            // signed-in applicant's own join-gateway wait-screen poll silently resolved
            // NjfContributionsTeam.NoAccessProfile instead of PublicVisitorQueue.AccessProfile and
            // 404'd on every single attempt. Reading the already-resolved key removes that whole
            // bug class — there's no longer any of Wayfinder.Umbraco's own routing to keep in sync
            // with by hand.
            options.ResolveAccessProfile = ResolveAccessProfile;
        });

        // ServiceRequestPollController (the join-gateway waiting screen's own poll endpoint)
        // requires this policy but deliberately ships with it unregistered — see
        // WayfinderUmbracoAuthorizationPolicies.ServiceRequestPolling's own remarks. Previously
        // RequireAuthenticatedUser() here, correct while the bulk-contributions demo's Join
        // gateway (always an authenticated NJF Contributions Team member, see
        // NjfContributionsTeam) was the only one that ever showed a waiting screen. The juggling
        // licence demo's own "application-decided" Join (apply-for-a-juggling-licence.json) can
        // now be reached anonymously too — the same visitor PublicVisitorQueue.AccessProfile
        // already lets read/act on their own instance everywhere else in this flow, so this
        // endpoint must allow them as well. Not a broadened access surface: GetCurrent's own
        // ActorProfile/RestrictToInstanceOwner scoping already restricts what an anonymous poll
        // can actually see, the same protection the rest of this anonymous-first journey relies
        // on — this policy was only ever an extra belt-and-braces layer for a case that no longer
        // covers every caller.
        builder.Services.Configure<AuthorizationOptions>(options =>
        {
            options.AddPolicy(WayfinderUmbracoAuthorizationPolicies.ServiceRequestPolling, policy =>
                policy.RequireAssertion(_ => true));

            // Vinyl Vault demo: broadcasting a back-in-stock notification is a staff action,
            // not something every authenticated member should be able to trigger. The
            // "vinyl-admin" realm role is mapped into a flat "role" claim by the client's own
            // protocol mapper in keycloak/realm-export.json (Keycloak's default "roles" client
            // scope only produces nested realm_access.roles, which ASP.NET Core's role-claim
            // checks don't unpack) — see vinyl-admin@prism.local in that same file for the demo
            // account holding this role.
            options.AddPolicy("RequireVinylAdmin", policy =>
                policy.RequireRole("vinyl-admin"));
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
                [MoneyModellerAccess.MemberQueueKey] = ComponentTypeRegistry.AllDiscriminators
            }));

        // Demonstrates the service-sourced field extension point for a logged-in member.
        // Re-registering UmbracoProcessManagerEngine here (after AddWayfinderUmbraco()'s own
        // registration above) supplies the serviceInputsResolver delegate; last registration wins
        // for single-instance resolution, and IProcessManager's factory (also registered by
        // AddWayfinderUmbraco) resolves UmbracoProcessManagerEngine lazily, so it picks up this one.
        builder.Services.AddSingleton<IJugglingSocietyMembershipClient, JugglingSocietyMembershipClient>();
        builder.Services.AddSingleton<IMemberSavingsRecordService, MemberSavingsRecordService>();

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
            var memberRecordService = sp.GetRequiredService<IMemberSavingsRecordService>();
            var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
            return new UmbracoProcessManagerEngine(
                sp.GetRequiredService<ILogger<UmbracoProcessManagerEngine>>(),
                sp.GetRequiredService<IServiceBlueprintStore>(),
                sp.GetRequiredService<IServiceContentSanitizer>(),
                sp.GetRequiredService<IServiceRequestStore>(),
                httpContextAccessor,
                (instance, definition, _) =>
                {
                    if (string.Equals(definition.DefinitionKey, TestSiteSeedContract.JugglingLicenceBlueprintSlug, StringComparison.OrdinalIgnoreCase))
                    {
                        var membership = membershipClient.GetForUser(instance.UserId);
                        // Same claim types PrismUserContext itself reads — resolved directly
                        // here rather than through that (request-)Scoped service, since this
                        // resolver is captured once into a Singleton at startup. Gated on
                        // IsAuthenticated because instance.UserId is only the applicant's real
                        // email for a signed-in member (PublicVisitorIdentityResolver's scheme);
                        // for an anonymous visitor it's an opaque correlation-cookie GUID, which
                        // must never be suggested back to them as their own email address. Both
                        // fall back to null for an anonymous visitor or a non-request caller
                        // (e.g. an automation callback resuming the instance with no live
                        // HttpContext), which defaultFrom already treats as "no suggestion".
                        var user = httpContextAccessor.HttpContext?.User;
                        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
                        return new Dictionary<string, object?>
                        {
                            ["member"] = new Dictionary<string, object?>
                            {
                                ["tier"] = membership.Tier,
                                ["name"] = isAuthenticated ? user!.FindFirstValue("name") : null,
                                ["email"] = isAuthenticated ? instance.UserId : null
                            }
                        };
                    }

                    if (string.Equals(definition.DefinitionKey, TestSiteSeedContract.MoneyModellerBlueprintSlug, StringComparison.OrdinalIgnoreCase))
                    {
                        var record = memberRecordService.GetForUser(instance.UserId);
                        return new Dictionary<string, object?>
                        {
                            ["member"] = new Dictionary<string, object?>
                            {
                                ["name"] = record.Name,
                                ["active"] = record.Active,
                                ["age"] = record.Age,
                                ["salary"] = record.Salary,
                                ["accruedPension"] = record.AccruedPension,
                                ["accruedLump"] = record.AccruedLump,
                                ["dcPot"] = record.DcPot
                            }
                        };
                    }

                    // Generic fallback for every other blueprint's own source: "service" calc
                    // fields — mirrors the core Wayfinder repo's own Wayfinder.ReferenceApp
                    // resolver exactly. Most of these (e.g. bulk-contributions.json's
                    // contributionsErrorCount/WarningCount/AcceptedCount/DirtyCount) aren't a true
                    // external lookup a host needs to fetch at all — they're written into
                    // FieldValues by the engine's own bulk-dataset-ingest action; the calc
                    // evaluator still requires *something* supplied for every declared service
                    // field or it throws CalculationException, so this passes through whatever's
                    // already there (null before the action first runs, the real value after).
                    return (definition.Calculations?.Fields ?? new Dictionary<string, Wayfinder.Models.ServiceDesign.Calculations.ServiceBlueprintCalculationField>())
                        .Where(field => string.Equals(field.Value.Source, "service", StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(field => field.Key, field => instance.FieldValues.GetValueOrDefault(field.Key));
                },
                sp.GetServices<ISupportSystemClient>(),
                sp.GetRequiredService<IBulkDatasetStore>());
        });

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WayfinderServicePageContentType>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WayfinderServicePageSeeder>();

        // Juggling licence "leap across" demo: an Umbraco Automate automation decides the
        // application and pushes a Prism notification back — see JugglingLicenceDecisionAutomationSeeder
        // and this method's own MockBusinessAppContributions.Register() call above for the
        // equivalent bespoke-client pattern; this one is config-only (Wayfinder:SupportSystems),
        // so there's no ISupportSystemClient to register here at all.
        builder.Services.AddHostedService<JugglingLicenceDecisionAutomationSeeder>();
    }

    /// <summary>
    /// Wired as <see cref="Wayfinder.Umbraco.Configuration.WayfinderServiceDesignOptions.ResolveAccessProfile"/>
    /// above — extracted to its own testable method rather than an inline lambda, same reasoning
    /// as everywhere else this file uses that pattern.
    /// </summary>
    internal static ActorProfile ResolveAccessProfile(HttpContext ctx, string? blueprintKey)
    {
        if (string.Equals(blueprintKey, TestSiteSeedContract.JugglingLicenceBlueprintSlug, StringComparison.OrdinalIgnoreCase))
        {
            return PublicVisitorQueue.AccessProfile;
        }

        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            return PublicVisitorQueue.AccessProfile;
        }

        if (string.Equals(blueprintKey, TestSiteSeedContract.MoneyModellerBlueprintSlug, StringComparison.OrdinalIgnoreCase))
        {
            return MoneyModellerAccess.AccessProfile;
        }

        var email = ctx.RequestServices.GetRequiredService<IPrismUserContext>().Email;
        return NjfContributionsTeam.IsMember(email)
            ? NjfContributionsTeam.AccessProfile
            : NjfContributionsTeam.NoAccessProfile;
    }
}
