using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Web.Common.Authorization;
using UmbracoPrism.Core.Services;
using UmbracoPrism.Core.Services.Workflow;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Extensions;
using UmbracoPrism.WorkflowRuntime.Mcp;

namespace UmbracoPrism.Core.Extensions;

/// <summary>
/// Registers Prism CMS Workflow — the Umbraco-only workflow implementation. Unlike
/// <see cref="WorkflowBuilderExtensions.AddPrismWorkflowEngine"/> (opt-in, since talking to a
/// remote Business App is host-specific policy), this is a first-class, always-on Core package
/// feature: a host gets a working backoffice-authored, uSync-portable CMS workflow surface
/// with no wiring of its own beyond creating a <c>cmsWorkflowPage</c> content node.
/// </summary>
public static class CmsWorkflowBuilderExtensions
{
    public static IServiceCollection AddPrismCmsWorkflow(this IServiceCollection services)
    {
        // Boot-time definition loader (CmsWorkflowEngine's constructor-time seed) — deliberately
        // has no dependency on the engine itself; see UmbracoCmsWorkflowDefinitionBootStore's
        // own remarks for why a combined store would create a DI cycle.
        services.AddSingleton<IWorkflowDefinitionStore, UmbracoCmsWorkflowDefinitionBootStore>();

        // Durable, session-scoped instance storage (the toolkit's IWorkflowInstanceStore seam).
        services.AddSingleton<IWorkflowInstanceStore, UmbracoCmsWorkflowInstanceStore>();

        services.AddSingleton<CmsWorkflowEngine>();
        services.AddSingleton<IWorkflowRuntimeEngine>(sp => sp.GetRequiredService<CmsWorkflowEngine>());

        // Authoring-side store — a save reaches the live engine immediately (see
        // UmbracoCmsWorkflowDefinitionStore's own remarks).
        services.AddSingleton<IWorkflowSourceStore, UmbracoCmsWorkflowDefinitionStore>();

        // CMS-Workflow-specific authoring constraint (single queue only) — the shared
        // WorkflowAuthoringService runs every registered IWorkflowStructuralValidator alongside
        // its own generic validation, so the toolkit itself stays unaware this rule exists.
        services.AddSingleton<IWorkflowStructuralValidator, CmsWorkflowSingleQueueValidator>();

        services.AddPrismWorkflowAuthoring();

        // AI-agent authoring surface for CMS Workflow — mapped and gated in the host's
        // Program.cs (see MapPrismCmsWorkflowAuthoringMcp()), same live WorkflowAuthoringService
        // as the backoffice editor and CmsWorkflowAuthoringController.
        services.AddPrismWorkflowAuthoringMcp();

        // Keyed so the default (business-app, HTTP) IBusinessAppWorkflowClient registration from
        // AddPrismWorkflowEngine() is untouched — CmsWorkflowPageController resolves this one
        // explicitly via [FromKeyedServices("cms")]. Scoped, not singleton — it depends on the
        // scoped IPrismUserContext (and resolves identity per-request via IHttpContextAccessor
        // anyway), matching BusinessAppWorkflowClient's own lifetime for the same reason.
        services.AddKeyedScoped<IBusinessAppWorkflowClient, InProcessCmsWorkflowClient>("cms");

        services.AddHostedService<PrismCmsWorkflowInstanceSweepService>();

        return services;
    }

    /// <summary>
    /// Maps the CMS Workflow AI-authoring MCP endpoint, gated with the exact same auth stack as
    /// <see cref="Controllers.CmsWorkflowAuthoringController"/> and the backoffice editor itself
    /// — <see cref="AuthorizationPolicies.BackOfficeAccess"/> (any validly-authenticated backoffice
    /// principal, human or machine — Umbraco's client-credentials grant on
    /// <c>/umbraco/management/api/v1/security/back-office/token</c> resolves a real <c>IUser</c>
    /// the same way interactive login does) plus <c>"PrismAdmins"</c> (the same
    /// <c>Prism:AdminGroups:GroupAliases</c> group check). An MCP agent therefore needs a real
    /// backoffice service-account user in an admin group — "the same security as doing it
    /// manually," not a parallel scheme. Call from the host's <c>Program.cs</c>; a package (RCL)
    /// like this one has no access to the host's own <see cref="IEndpointRouteBuilder"/>.
    /// </summary>
    public static IEndpointConventionBuilder MapPrismCmsWorkflowAuthoringMcp(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPrismWorkflowAuthoringMcp()
            .RequireAuthorization(AuthorizationPolicies.BackOfficeAccess, "PrismAdmins");
}
