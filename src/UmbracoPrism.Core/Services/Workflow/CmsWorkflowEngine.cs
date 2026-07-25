using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UmbracoPrism.Shared.Models.Workflow;
using UmbracoPrism.Shared.Services.Sanitization;
using UmbracoPrism.WorkflowRuntime.Abstractions;
using UmbracoPrism.WorkflowRuntime.Models;
using UmbracoPrism.WorkflowRuntime.Services;

namespace UmbracoPrism.Core.Services.Workflow;

/// <summary>
/// The CMS Workflow implementation's own <see cref="IWorkflowRuntimeEngine"/> — a distinctly
/// named singleton so it's discoverable in DI registration/debugging, kept separate from any
/// business-app-hosted engine a host might also run (registered under the "cms" DI key). No
/// override logic lives here: <c>serviceInputsResolver</c> (the toolkit's existing extension
/// point for <c>source: "service"</c> calculation fields — see <see cref="WorkflowRuntimeEngine.ResolveServiceInputs"/>)
/// is supplied as a plain delegate at registration time, so a demo host (e.g. TestSite's
/// juggling-society membership lookup) needs no subclass of its own.
/// </summary>
public sealed class CmsWorkflowEngine(
    ILogger<CmsWorkflowEngine> logger,
    IWorkflowDefinitionStore definitionStore,
    IWorkflowContentSanitizer sanitizer,
    IWorkflowInstanceStore instanceStore,
    IHttpContextAccessor httpContextAccessor,
    Func<WorkflowInstanceState, WorkflowDefinitionFile, StepDefinition, IReadOnlyDictionary<string, object?>?>? serviceInputsResolver = null)
    : WorkflowRuntimeEngine(logger, definitionStore, sanitizer, serviceInputsResolver, instanceStore)
{
    /// <summary>
    /// A new instance is authenticated when the request creating it belongs to a signed-in
    /// user — mirrors the same <c>User.Identity.IsAuthenticated</c> check <c>PrismUserContext</c>
    /// makes, but read directly via <see cref="IHttpContextAccessor"/> rather than injecting
    /// the scoped <c>IPrismUserContext</c> into this singleton engine (which would capture a
    /// stale request the first time it resolved).
    /// </summary>
    protected override bool ResolveIsAuthenticated(string tenantId, string userId) =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Resolves a <c>file-upload</c> field's stored reference for a download endpoint — reuses
    /// the exact same ownership check (<see cref="WorkflowRuntimeEngine.CanAccessInstance"/>)
    /// every other instance access goes through, rather than a separate re-derivation. Returns
    /// <see langword="null"/> for an unknown instance, a requester who doesn't own it, or a
    /// field with no uploaded file — callers should treat all three identically (404), not
    /// distinguish "not found" from "not yours".
    /// </summary>
    public WorkflowFileReference? TryGetOwnedFileReference(
        string instanceId,
        string tenantId,
        string userId,
        WorkflowAccessProfile accessProfile,
        string fieldKey)
    {
        if (!TryGetInstance(instanceId, out var instance))
        {
            return null;
        }

        if (!CanAccessInstance(instance, tenantId, userId, accessProfile))
        {
            return null;
        }

        return instance.FieldValues.TryGetValue(fieldKey, out var raw)
            ? WorkflowFileReference.FromFieldValue(raw)
            : null;
    }

    /// <summary>
    /// The same ownership check <see cref="TryGetOwnedFileReference"/> performs, for a caller
    /// that needs to authorize against an instance before any file exists yet — the async
    /// upload endpoint, which must verify the requester owns the instance it's about to write a
    /// new file against.
    /// </summary>
    public bool IsOwnedInstance(
        string instanceId,
        string tenantId,
        string userId,
        WorkflowAccessProfile accessProfile)
    {
        return TryGetInstance(instanceId, out var instance)
            && CanAccessInstance(instance, tenantId, userId, accessProfile);
    }
}
