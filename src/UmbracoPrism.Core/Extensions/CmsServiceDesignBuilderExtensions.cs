using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.ServiceDesign;
using Wayfinder.Models.ServiceDesign.Components;
using Wayfinder.Engine.Abstractions;
using Wayfinder.Engine.Extensions;
using Wayfinder.Engine.Mcp;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Registers Prism CMS Service Blueprint — the Umbraco-only service blueprint implementation. Unlike
/// <see cref="ServiceDesignBuilderExtensions.AddPrismProcessManager"/> (opt-in, since talking to a
/// remote Business App is host-specific policy), this is a first-class, always-on Core package
/// feature: a host gets a working backoffice-authored, uSync-portable CMS service blueprint surface
/// with no wiring of its own beyond creating a <c>cmsServiceRequestPage</c> content node.
/// </summary>
public static class CmsServiceDesignBuilderExtensions
{
    public static IServiceCollection AddPrismCmsServiceBlueprint(this IServiceCollection services)
    {
        // Boot-time definition loader (CmsProcessManager's constructor-time seed) — deliberately
        // has no dependency on the engine itself; see UmbracoCmsServiceBlueprintBootStore's
        // own remarks for why a combined store would create a DI cycle.
        services.AddSingleton<IServiceBlueprintStore, UmbracoCmsServiceBlueprintBootStore>();

        // Durable, session-scoped instance storage (the toolkit's IServiceRequestStore seam).
        services.AddSingleton<IServiceRequestStore, UmbracoCmsServiceRequestStore>();

        services.AddSingleton<CmsProcessManager>();
        services.AddSingleton<IProcessManager>(sp => sp.GetRequiredService<CmsProcessManager>());

        // Authoring-side store — a save reaches the live engine immediately (see
        // UmbracoCmsServiceBlueprintStore's own remarks).
        services.AddSingleton<IServiceBlueprintSourceStore, UmbracoCmsServiceBlueprintStore>();

        // CMS-Service-Blueprint-specific authoring constraint (single queue only) — the shared
        // ServiceBlueprintAuthoringService runs every registered IServiceBlueprintStructuralValidator alongside
        // its own generic validation, so the toolkit itself stays unaware this rule exists.
        services.AddSingleton<IServiceBlueprintStructuralValidator, CmsSingleQueueValidator>();

        // Explicit capability contract for cms-visitor — matches today's de-facto behaviour
        // (nothing was registered before, so capability enforcement was silently skipped) but
        // now makes it honest: an agent authoring via list_queue_capabilities can see exactly
        // what this host's stock rendering pipeline supports, including file-upload and
        // guidance-checklist, instead of the check being quietly absent.
        services.AddSingleton<IQueueCapabilitiesProvider>(new StaticQueueCapabilitiesProvider(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [CmsQueue.Key] = PrismComponentTypeCatalog.AllDiscriminators
            }));

        services.AddPrismServiceBlueprintAuthoring();

        // AI-agent authoring surface for CMS Service Blueprint — mapped and gated in the host's
        // Program.cs (see MapPrismCmsServiceBlueprintAuthoringMcp()), same live ServiceBlueprintAuthoringService
        // as the backoffice editor and CmsServiceBlueprintAuthoringController. Host-specific facts (single
        // queue, always cms-visitor) go in via ServerInstructions rather than requiring every
        // human-written brief to repeat them — the generic toolkit itself stays unaware this
        // constraint exists at all.
        services.AddPrismServiceBlueprintAuthoringMcp(
            "This host is Prism's CMS Service Blueprint — a single-actor, backoffice-hosted service blueprint " +
            "surface. Every definition here has exactly one queue, always named \"cms-visitor\" " +
            "(no reviewer/admin side is possible). Call list_queue_capabilities before drafting " +
            "any component — this host's stock rendering pipeline is the source of truth for " +
            "which component types are actually available, not assumption from other hosts or " +
            "other Prism service blueprint systems.");

        // Shared visitor-identity resolution (cookie for anonymous, member email when signed
        // in) — used by InProcessCmsProcessManagerClient and the file download endpoint alike, so
        // both resolve "whose instance is this" identically. Scoped: depends on the scoped
        // IPrismUserContext (and resolves identity per-request via IHttpContextAccessor anyway).
        services.AddScoped<CmsServiceRequestVisitorIdentityResolver>();

        // Keyed so the default (business-app, HTTP) IBusinessAppProcessManagerClient registration from
        // AddPrismProcessManager() is untouched — CmsServiceRequestPageController resolves this one
        // explicitly via [FromKeyedServices("cms")]. Scoped, not singleton — it depends on the
        // scoped IPrismUserContext (and resolves identity per-request via IHttpContextAccessor
        // anyway), matching BusinessAppProcessManagerClient's own lifetime for the same reason.
        services.AddKeyedScoped<IBusinessAppProcessManagerClient, InProcessCmsProcessManagerClient>("cms");

        // File-upload storage for the "file-upload" component type — disk-backed by default;
        // a host can replace this registration with its own (blob storage, etc.).
        services.AddSingleton<IServiceRequestFileStorage, DiskServiceRequestFileStorage>();

        // Binds an async-uploaded file to the opaque token the client carries until the stage's
        // real POST — same IDistributedCache mechanism as the nonce service.
        services.AddSingleton<IUploadTokenService, UploadTokenService>();

        services.AddHostedService<PrismCmsServiceRequestSweepService>();

        return services;
    }

    /// <summary>
    /// Maps the CMS Service Blueprint AI-authoring MCP endpoint, gated with the exact same auth stack as
    /// <see cref="Controllers.CmsServiceBlueprintAuthoringController"/> and the backoffice editor itself
    /// — <see cref="AuthorizationPolicies.BackOfficeAccess"/> (any validly-authenticated backoffice
    /// principal, human or machine — Umbraco's client-credentials grant on
    /// <c>/umbraco/management/api/v1/security/back-office/token</c> resolves a real <c>IUser</c>
    /// the same way interactive login does) plus <c>"PrismAdmins"</c> (the same
    /// <c>Prism:AdminGroups:GroupAliases</c> group check). An MCP agent therefore needs a real
    /// backoffice service-account user in an admin group — "the same security as doing it
    /// manually," not a parallel scheme. Call from the host's <c>Program.cs</c>; a package (RCL)
    /// like this one has no access to the host's own <see cref="IEndpointRouteBuilder"/>.
    /// </summary>
    public static IEndpointConventionBuilder MapPrismCmsServiceBlueprintAuthoringMcp(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPrismServiceBlueprintAuthoringMcp()
            .RequireAuthorization(AuthorizationPolicies.BackOfficeAccess, "PrismAdmins");
}
